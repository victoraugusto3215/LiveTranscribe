namespace LiveTranscribe.Api.Services.Audio;

/// <summary>
/// Formatos aceitos no upload — os mais comuns em gravações reais
/// (reunião, celular, WhatsApp). Tudo que não é WAV passa pelo ffmpeg
/// antes de entrar no pipeline de chunking.
/// </summary>
public static class AudioFormats
{
    public static readonly IReadOnlySet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".wav",
        ".mp3",
        ".m4a",
        ".ogg",
        ".flac",
        ".aac",
    };

    public static bool IsAllowed(string fileName) =>
        AllowedExtensions.Contains(Path.GetExtension(fileName));
}
