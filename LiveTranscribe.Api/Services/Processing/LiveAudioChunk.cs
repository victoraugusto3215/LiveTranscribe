namespace LiveTranscribe.Api.Services.Processing;

public sealed record LiveAudioChunk(byte[] WavBytes, int SequenceNumber, long StartMs, long EndMs);
