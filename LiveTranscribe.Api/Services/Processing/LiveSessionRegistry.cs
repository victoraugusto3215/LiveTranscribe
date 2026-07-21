using System.Collections.Concurrent;
using System.Threading.Channels;

namespace LiveTranscribe.Api.Services.Processing;

/// <summary>
/// Estado em memória das sessões de microfone ao vivo em andamento — liga
/// um sessionId ao channel que recebe os chunks e à conexão SignalR dona
/// dela, pra limpar tudo se a conexão cair sem um EndLiveSession explícito.
/// </summary>
public sealed class LiveSessionRegistry
{
    private readonly ConcurrentDictionary<Guid, (Channel<LiveAudioChunk> Channel, string ConnectionId)> _sessions = new();

    public void Register(Guid sessionId, string connectionId, Channel<LiveAudioChunk> channel) =>
        _sessions[sessionId] = (channel, connectionId);

    public bool TryGetChannel(Guid sessionId, out Channel<LiveAudioChunk> channel)
    {
        if (_sessions.TryGetValue(sessionId, out var entry))
        {
            channel = entry.Channel;
            return true;
        }

        channel = null!;
        return false;
    }

    public IReadOnlyList<Guid> GetSessionsForConnection(string connectionId) =>
        _sessions.Where(kv => kv.Value.ConnectionId == connectionId).Select(kv => kv.Key).ToList();

    public void Remove(Guid sessionId) => _sessions.TryRemove(sessionId, out _);
}
