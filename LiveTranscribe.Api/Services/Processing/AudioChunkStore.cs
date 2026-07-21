using Microsoft.Extensions.Options;

namespace LiveTranscribe.Api.Services.Processing;

public sealed class AudioChunkStoreOptions
{
    public const string SectionName = "AudioStorage";

    public string RootPath { get; set; } = "App_Data/sessions";
}

/// <summary>
/// Persiste o áudio original (no formato que o usuário enviou), a versão
/// normalizada em WAV e cada chunk fatiado em disco antes de processar —
/// se o processo cair no meio, o áudio bruto não se perde e pode ser
/// reprocessado.
/// </summary>
public sealed class AudioChunkStore
{
    private readonly string _rootPath;

    public AudioChunkStore(IOptions<AudioChunkStoreOptions> options, IHostEnvironment env)
    {
        _rootPath = Path.IsPathRooted(options.Value.RootPath)
            ? options.Value.RootPath
            : Path.Combine(env.ContentRootPath, options.Value.RootPath);
    }

    public async Task<string> SaveUploadAsync(Guid sessionId, string extension, byte[] bytes, CancellationToken cancellationToken)
    {
        string dir = SessionDir(sessionId);
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"original{extension}");
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        return path;
    }

    public string GetUploadPath(Guid sessionId, string extension) =>
        Path.Combine(SessionDir(sessionId), $"original{extension}");

    public string GetNormalizedWavPath(Guid sessionId) =>
        Path.Combine(SessionDir(sessionId), "normalized.wav");

    public Task<byte[]> ReadNormalizedWavAsync(Guid sessionId, CancellationToken cancellationToken) =>
        File.ReadAllBytesAsync(GetNormalizedWavPath(sessionId), cancellationToken);

    public async Task SaveChunkAsync(Guid sessionId, int sequenceNumber, byte[] wavBytes, CancellationToken cancellationToken)
    {
        string dir = Path.Combine(SessionDir(sessionId), "chunks");
        Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(GetChunkPath(sessionId, sequenceNumber), wavBytes, cancellationToken);
    }

    public string GetChunkPath(Guid sessionId, int sequenceNumber) =>
        Path.Combine(SessionDir(sessionId), "chunks", $"{sequenceNumber:D6}.wav");

    /// <summary>
    /// Remove o áudio (original, normalizado e chunks) de uma sessão do
    /// disco. Não mexe no banco — a transcrição em si (texto) continua
    /// disponível mesmo depois do áudio bruto ser descartado.
    /// </summary>
    public void DeleteSessionFiles(Guid sessionId)
    {
        string dir = SessionDir(sessionId);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private string SessionDir(Guid sessionId) => Path.Combine(_rootPath, sessionId.ToString());
}
