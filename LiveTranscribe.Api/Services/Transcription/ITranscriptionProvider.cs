namespace LiveTranscribe.Api.Services.Transcription;

public sealed record TranscriptionResult(string Text, string? DetectedLanguage);

/// <summary>
/// Abstrai o motor de transcrição para permitir trocar Groq por um provider
/// local (ex.: faster-whisper via sidecar) sem tocar no pipeline de sessão.
/// </summary>
public interface ITranscriptionProvider
{
    Task<TranscriptionResult> TranscribeAsync(byte[] wavBytes, string languageCode, CancellationToken cancellationToken);
}
