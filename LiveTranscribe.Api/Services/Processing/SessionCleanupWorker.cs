using LiveTranscribe.Api.Data;
using LiveTranscribe.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LiveTranscribe.Api.Services.Processing;

/// <summary>
/// Varre periodicamente as sessões mais antigas que a janela de retenção e
/// apaga o áudio delas do disco (o texto transcrito continua no banco).
/// Sem isso, cada upload fica pra sempre em App_Data/sessions e o disco
/// cresce sem limite.
/// </summary>
public sealed class SessionCleanupWorker(
    IServiceScopeFactory scopeFactory,
    AudioChunkStore chunkStore,
    IOptions<SessionCleanupOptions> options,
    ILogger<SessionCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        using var timer = new PeriodicTimer(opts.Interval);

        do
        {
            try
            {
                await CleanupExpiredSessionsAsync(opts.RetentionPeriod, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha na varredura de limpeza de sessões.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CleanupExpiredSessionsAsync(TimeSpan retention, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        DateTimeOffset cutoff = DateTimeOffset.UtcNow - retention;

        // filtro inteiro em memória de propósito: o provider SQLite do EF
        // Core não traduz comparações ("<", ">") sobre DateTimeOffset pra
        // SQL (limitação conhecida do provider, não um erro de digitação).
        // Pra escala de sessões desse projeto, trazer tudo pra memória e
        // filtrar em C# é simples e não tem custo real.
        List<Guid> expiredSessionIds = (await db.Sessions
                .Select(s => new { s.Id, s.CreatedAt, s.Status })
                .ToListAsync(cancellationToken))
            .Where(s => s.CreatedAt < cutoff && s.Status != SessionStatus.Processing)
            .Select(s => s.Id)
            .ToList();

        if (expiredSessionIds.Count == 0)
        {
            return;
        }

        foreach (Guid sessionId in expiredSessionIds)
        {
            try
            {
                chunkStore.DeleteSessionFiles(sessionId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao excluir arquivos da sessão {SessionId}.", sessionId);
            }
        }

        logger.LogInformation(
            "Limpeza: áudio removido de {Count} sessão(ões) com mais de {RetentionHours}h.",
            expiredSessionIds.Count, retention.TotalHours);
    }
}
