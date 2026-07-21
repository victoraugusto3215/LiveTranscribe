using LiveTranscribe.Api.Data;
using LiveTranscribe.Api.Hubs;
using LiveTranscribe.Api.Models;
using LiveTranscribe.Api.Services.Audio;
using LiveTranscribe.Api.Services.Transcription;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LiveTranscribe.Api.Services.Processing;

/// <summary>
/// Processa uma sessão de ponta a ponta: fatia o áudio original em chunks,
/// transcreve cada um em ordem e empurra o resultado pro grupo SignalR da
/// sessão conforme fica pronto — é aqui que a "entrega progressiva" acontece.
/// </summary>
public sealed class SessionProcessor(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ITranscriptionProvider transcriptionProvider,
    AudioChunkStore chunkStore,
    AudioNormalizer normalizer,
    IHubContext<TranscriptionHub, ITranscriptionClient> hub,
    ILogger<SessionProcessor> logger)
{
    private static readonly TimeSpan ChunkDuration = TimeSpan.FromSeconds(4);

    public async Task ProcessAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null)
        {
            logger.LogWarning("Sessão {SessionId} não encontrada para processamento.", sessionId);
            return;
        }

        var group = hub.Clients.Group(TranscriptionHub.GroupName(sessionId));
        await group.SessionStatusChanged("processing");

        try
        {
            string extension = Path.GetExtension(session.OriginalFileName);
            string uploadPath = chunkStore.GetUploadPath(sessionId, extension);
            string normalizedPath = chunkStore.GetNormalizedWavPath(sessionId);

            // sempre normaliza (mesmo um .wav enviado pode não estar em
            // 16kHz mono PCM16) — assim o WavSlicer sempre recebe um
            // formato previsível, não importa o que o usuário mandou
            await normalizer.NormalizeToWavAsync(uploadPath, normalizedPath, cancellationToken);

            byte[] normalizedWav = await chunkStore.ReadNormalizedWavAsync(sessionId, cancellationToken);
            IReadOnlyList<WavChunk> chunks = WavSlicer.Slice(normalizedWav, ChunkDuration);

            foreach (var chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await chunkStore.SaveChunkAsync(sessionId, chunk.SequenceNumber, chunk.WavBytes, cancellationToken);

                // TranscriptionUnavailableException (chave ausente/inválida) sobe e
                // encerra a sessão — não adianta tentar o próximo chunk, vai falhar
                // igual. Qualquer outra falha (429/5xx esgotados, chunk corrompido)
                // só marca esse trecho e segue pros próximos, sem perder o resto.
                string text = await TranscribeChunkTextAsync(chunk.WavBytes, session.LanguageCode, sessionId, chunk.SequenceNumber, cancellationToken);

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

            session.Status = SessionStatus.Completed;
            session.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await group.SessionStatusChanged("completed");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Erro processando sessão {SessionId}", sessionId);
            session.Status = SessionStatus.Failed;
            session.ErrorMessage = ex.Message;
            await db.SaveChangesAsync(cancellationToken);
            await group.SessionStatusChanged("error");
        }
    }

    private async Task<string> TranscribeChunkTextAsync(
        byte[] wavBytes, string languageCode, Guid sessionId, int sequenceNumber, CancellationToken cancellationToken)
    {
        try
        {
            TranscriptionResult result = await transcriptionProvider.TranscribeAsync(wavBytes, languageCode, cancellationToken);
            return result.Text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TranscriptionUnavailableException)
        {
            logger.LogWarning(ex, "Falha ao transcrever chunk {Sequence} da sessão {SessionId} — trecho pulado.", sequenceNumber, sessionId);
            return "(falha ao transcrever este trecho)";
        }
    }
}
