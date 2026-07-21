namespace LiveTranscribe.Api.Services.Processing;

/// <summary>
/// Consome a fila de sessões pendentes e processa até
/// <see cref="MaxConcurrentSessions"/> sessões em paralelo — protege contra
/// sobrecarregar a API do Groq (ou a máquina, no provider local) se várias
/// sessões chegarem ao mesmo tempo.
/// </summary>
public sealed class SessionProcessingWorker(
    SessionProcessingQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<SessionProcessingWorker> logger) : BackgroundService
{
    private const int MaxConcurrentSessions = 3;

    private readonly SemaphoreSlim _concurrencyLimiter = new(MaxConcurrentSessions);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (Guid sessionId in queue.ReadAllAsync(stoppingToken))
        {
            await _concurrencyLimiter.WaitAsync(stoppingToken);

            _ = ProcessSessionAsync(sessionId, stoppingToken);
        }
    }

    private async Task ProcessSessionAsync(Guid sessionId, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<SessionProcessor>();
            await processor.ProcessAsync(sessionId, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha não tratada processando sessão {SessionId}", sessionId);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }
}
