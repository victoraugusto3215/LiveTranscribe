namespace LiveTranscribe.Api.Hubs;

/// <summary>
/// Contrato fortemente tipado dos métodos que o servidor invoca no cliente
/// (Hub&lt;T&gt; evita erros de nome de método em string solta).
/// </summary>
public interface ITranscriptionClient
{
    Task ReceiveSegment(TranscriptSegmentDto segment);

    Task SessionStatusChanged(string status);
}
