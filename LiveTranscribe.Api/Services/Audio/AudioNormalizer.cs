using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace LiveTranscribe.Api.Services.Audio;

/// <summary>
/// Ponte entre "qualquer formato de áudio comum" e o pipeline de chunking,
/// que só entende WAV PCM. Todo upload passa por aqui antes do
/// <see cref="WavSlicer"/> — normaliza pra 16kHz mono (o que o Whisper
/// espera de qualquer forma, então isso também reduz o tamanho de cada
/// chunk enviado pra Groq).
/// </summary>
public sealed class AudioNormalizer(ILogger<AudioNormalizer> logger)
{
    /// <summary>
    /// Baixa o binário do ffmpeg pra dentro de <paramref name="binariesPath"/>
    /// se ainda não estiver lá. Idempotente — chamado uma vez no startup.
    /// </summary>
    public static async Task EnsureFFmpegAvailableAsync(string binariesPath, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(binariesPath);
        FFmpeg.SetExecutablesPath(binariesPath);

        bool alreadyInstalled = Directory.EnumerateFiles(binariesPath, "ffmpeg*").Any();
        if (!alreadyInstalled)
        {
            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, binariesPath);
        }
    }

    public async Task<bool> IsReadableAudioAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            IMediaInfo info = await FFmpeg.GetMediaInfo(filePath, cancellationToken);
            return info.AudioStreams.Any();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao ler metadados de áudio de {FilePath}", filePath);
            return false;
        }
    }

    public async Task NormalizeToWavAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        IConversion conversion = FFmpeg.Conversions.New()
            .AddParameter($"-i \"{inputPath}\"")
            .AddParameter("-vn") // descarta capa/thumbnail embutida em alguns m4a/mp3
            .AddParameter("-ac 1 -ar 16000 -sample_fmt s16 -f wav")
            .SetOutput(outputPath)
            .SetOverwriteOutput(true);

        await conversion.Start(cancellationToken);
    }
}
