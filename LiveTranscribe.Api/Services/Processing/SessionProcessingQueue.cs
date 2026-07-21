using System.Threading.Channels;

namespace LiveTranscribe.Api.Services.Processing;

/// <summary>
/// Fila limitada entre o endpoint de upload e o worker de processamento.
/// Bounded (não ilimitada) para que, se muitas sessões chegarem de uma vez,
/// o próprio upload aplique backpressure em vez de acumular memória sem limite.
/// </summary>
public sealed class SessionProcessingQueue
{
    private readonly Channel<Guid> _channel;

    public SessionProcessingQueue()
    {
        _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(capacity: 20)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });
    }

    public ValueTask EnqueueAsync(Guid sessionId, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(sessionId, cancellationToken);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
