using System.Threading.Channels;
using LiveTranscribe.Api.Data;
using LiveTranscribe.Api.Models;
using LiveTranscribe.Api.Services.Processing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LiveTranscribe.Api.Hubs;

/// <summary>
/// Cada sessão de transcrição tem seu próprio grupo, permitindo que mais de
/// um cliente acompanhe a mesma sessão (ex.: um supervisor assistindo a
/// transcrição de uma call em andamento). Além do modo "upload de arquivo"
/// (onde a sessão é criada via REST e o Hub só distribui os resultados),
/// este Hub também sustenta o modo "microfone ao vivo": a sessão nasce
/// aqui, e os chunks chegam via <see cref="SendAudioChunk"/> conforme o
/// usuário fala.
/// </summary>
public sealed class TranscriptionHub(
    IDbContextFactory<AppDbContext> dbContextFactory,
    LiveSessionRegistry liveSessions,
    IServiceScopeFactory scopeFactory,
    ILogger<TranscriptionHub> logger) : Hub<ITranscriptionClient>
{
    private const int MaxQueuedChunksPerLiveSession = 6;

    public Task JoinSession(Guid sessionId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));

    public Task LeaveSession(Guid sessionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(sessionId));

    public async Task<Guid> StartLiveSession(string languageCode)
    {
        var session = new TranscriptionSession
        {
            Id = Guid.NewGuid(),
            OriginalFileName = "microfone ao vivo",
            LanguageCode = string.IsNullOrWhiteSpace(languageCode) ? "pt" : languageCode,
            Status = SessionStatus.Processing,
        };

        await using (var db = await dbContextFactory.CreateDbContextAsync(Context.ConnectionAborted))
        {
            db.Sessions.Add(session);
            await db.SaveChangesAsync(Context.ConnectionAborted);
        }

        // bounded: se a transcrição atrasar, SendAudioChunk (abaixo) fica
        // esperando escrever no channel — é o backpressure chegando até o
        // cliente, que só manda o próximo pedaço quando o invoke anterior retornar
        var channel = Channel.CreateBounded<LiveAudioChunk>(new BoundedChannelOptions(MaxQueuedChunksPerLiveSession)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
        });

        liveSessions.Register(session.Id, Context.ConnectionId, channel);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(session.Id));

        _ = RunLiveProcessorSafelyAsync(session.Id, channel.Reader, session.LanguageCode);

        return session.Id;
    }

    public Task SendAudioChunk(Guid sessionId, byte[] wavBytes, int sequenceNumber, long startMs, long endMs)
    {
        if (liveSessions.TryGetChannel(sessionId, out var channel))
        {
            return channel.Writer.WriteAsync(
                new LiveAudioChunk(wavBytes, sequenceNumber, startMs, endMs), Context.ConnectionAborted).AsTask();
        }

        return Task.CompletedTask;
    }

    public Task EndLiveSession(Guid sessionId)
    {
        if (liveSessions.TryGetChannel(sessionId, out var channel))
        {
            channel.Writer.TryComplete();
        }

        return Task.CompletedTask;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        // se a aba fechar ou a conexão cair sem um EndLiveSession explícito,
        // fecha o channel mesmo assim — sem isso o consumer ficaria
        // esperando pra sempre por chunks que nunca mais vão chegar
        foreach (Guid sessionId in liveSessions.GetSessionsForConnection(Context.ConnectionId))
        {
            if (liveSessions.TryGetChannel(sessionId, out var channel))
            {
                channel.Writer.TryComplete();
            }
        }

        return base.OnDisconnectedAsync(exception);
    }

    private async Task RunLiveProcessorSafelyAsync(Guid sessionId, ChannelReader<LiveAudioChunk> reader, string languageCode)
    {
        // escopo próprio, fora do escopo (curto) desta chamada de Hub — o
        // processamento continua rodando bem depois do StartLiveSession
        // já ter retornado pro cliente
        using var scope = scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<LiveSessionProcessor>();

        try
        {
            await processor.RunAsync(sessionId, reader, languageCode, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha não tratada na sessão ao vivo {SessionId}", sessionId);
        }
        finally
        {
            liveSessions.Remove(sessionId);
        }
    }

    public static string GroupName(Guid sessionId) => $"session:{sessionId}";
}
