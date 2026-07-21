using System.Threading.Channels;
using LiveTranscribe.Api.Data;
using LiveTranscribe.Api.Hubs;
using LiveTranscribe.Api.Models;
using LiveTranscribe.Api.Services.Audio;
using LiveTranscribe.Api.Services.Transcription;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LiveTranscribe.Api.Services.Processing;

/// <summary>
/// Consome os chunks de uma sessão de microfone ao vivo conforme chegam
/// pelo Hub — mesma lógica de normalização/transcrição/persistência do
/// upload de arquivo (<see cref="SessionProcessor"/>), só que alimentada
/// por um Channel em vez de uma lista pré-fatiada de um arquivo completo.
/// </summary>
public sealed class LiveSessionProcessor(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ITranscriptionProvider transcriptionProvider,
    AudioChunkStore chunkStore,
    AudioNormalizer normalizer,
    IHubContext<TranscriptionHub, ITranscriptionClient> hub,
    ILogger<LiveSessionProcessor> logger)
{
    public async Task RunAsync(Guid sessionId, ChannelReader<LiveAudioChunk> reader, string languageCode, CancellationToken cancellationToken)
    {
        var group = hub.Clients.Group(TranscriptionHub.GroupName(sessionId));

        try
        {
            await foreach (LiveAudioChunk chunk in reader.ReadAllAsync(cancellationToken))
            {
                await ProcessChunkAsync(sessionId, chunk, languageCode, group, cancellationToken);
            }

            await FinalizeAsync(sessionId, SessionStatus.Completed, errorMessage: null, group, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Erro na sessão ao vivo {SessionId}", sessionId);
            await FinalizeAsync(sessionId, SessionStatus.Failed, ex.Message, group, CancellationToken.None);
        }
    }

    private async Task ProcessChunkAsync(
        Guid sessionId, LiveAudioChunk chunk, string languageCode, ITranscriptionClient group, CancellationToken cancellationToken)
    {
        await chunkStore.SaveChunkAsync(sessionId, chunk.SequenceNumber, chunk.WavBytes, cancellationToken);

        string chunkPath = chunkStore.GetChunkPath(sessionId, chunk.SequenceNumber);
        string normalizedPath = Path.Combine(Path.GetTempPath(), $"livetranscribe-live-{sessionId:N}-{chunk.SequenceNumber:D6}.wav");

        // TranscriptionUnavailableException (chave ausente/inválida) sobe e
        // encerra a sessão inteira — não adianta tentar o próximo chunk.
        // Qualquer outra falha (429/5xx esgotados, chunk corrompido) só
        // marca esse trecho e segue ouvindo os próximos.
        string text;
        try
        {
            await normalizer.NormalizeToWavAsync(chunkPath, normalizedPath, cancellationToken);
            byte[] normalizedBytes = await File.ReadAllBytesAsync(normalizedPath, cancellationToken);
            TranscriptionResult result = await transcriptionProvider.TranscribeAsync(normalizedBytes, languageCode, cancellationToken);
            text = result.Text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TranscriptionUnavailableException)
        {
            logger.LogWarning(ex, "Falha ao transcrever chunk {Sequence} da sessão ao vivo {SessionId} — trecho pulado.", chunk.SequenceNumber, sessionId);
            text = "(falha ao transcrever este trecho)";
        }
        finally
        {
            if (File.Exists(normalizedPath))
            {
                File.Delete(normalizedPath);
            }
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var segment = new TranscriptSegment
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            SequenceNumber = chunk.SequenceNumber,
            StartMs = chunk.StartMs,
            EndMs = chunk.EndMs,
            Text = text,
            IsFinal = true,
        };
        db.Segments.Add(segment);
        await db.SaveChangesAsync(cancellationToken);

        await group.ReceiveSegment(TranscriptSegmentDto.From(segment));
    }

    private async Task FinalizeAsync(
        Guid sessionId, SessionStatus status, string? errorMessage, ITranscriptionClient group, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is not null)
        {
            session.Status = status;
            session.ErrorMessage = errorMessage;
            session.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        await group.SessionStatusChanged(status == SessionStatus.Completed ? "completed" : "error");
    }
}
