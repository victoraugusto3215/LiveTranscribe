namespace LiveTranscribe.Api.Models;

public enum SessionStatus
{
    Processing,
    Completed,
    Failed,
}

public sealed class TranscriptionSession
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = "";
    public string LanguageCode { get; set; } = "pt";
    public SessionStatus Status { get; set; } = SessionStatus.Processing;
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    public List<TranscriptSegment> Segments { get; set; } = [];
}
