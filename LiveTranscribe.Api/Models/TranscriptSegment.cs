namespace LiveTranscribe.Api.Models;

public sealed class TranscriptSegment
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public int SequenceNumber { get; set; }
    public long StartMs { get; set; }
    public long EndMs { get; set; }
    public string Text { get; set; } = "";
    public bool IsFinal { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public TranscriptionSession Session { get; set; } = null!;
}
