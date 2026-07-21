using LiveTranscribe.Api.Models;

namespace LiveTranscribe.Api.Hubs;

public sealed record TranscriptSegmentDto(Guid Id, int SequenceNumber, long StartMs, long EndMs, string Text, bool IsFinal)
{
    public static TranscriptSegmentDto From(TranscriptSegment segment) =>
        new(segment.Id, segment.SequenceNumber, segment.StartMs, segment.EndMs, segment.Text, segment.IsFinal);
}
