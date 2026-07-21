namespace LiveTranscribe.Api.Services.Audio;

public sealed record WavFormat(ushort AudioFormat, ushort Channels, uint SampleRate, ushort BitsPerSample)
{
    public int BlockAlign => Channels * (BitsPerSample / 8);
    public int ByteRate => (int)SampleRate * BlockAlign;
}

public sealed record WavChunk(int SequenceNumber, long StartMs, long EndMs, byte[] WavBytes);
