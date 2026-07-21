using LiveTranscribe.Api.Services.Audio;

namespace LiveTranscribe.Tests.Audio;

public class WavSlicerTests
{
    private static readonly WavFormat Mono16kHz16Bit = new(AudioFormat: 1, Channels: 1, SampleRate: 16_000, BitsPerSample: 16);

    [Fact]
    public void Parse_RoundTrip_PreservesFormatAndData()
    {
        byte[] pcm = GeneratePcm(Mono16kHz16Bit, TimeSpan.FromSeconds(1));
        byte[] wav = WavSlicer.WrapAsWav(pcm, Mono16kHz16Bit);

        var (format, data) = WavSlicer.Parse(wav);

        Assert.Equal(Mono16kHz16Bit, format);
        Assert.Equal(pcm, data);
    }

    [Fact]
    public void Slice_ReconstructedChunks_MatchOriginalDataExactly()
    {
        byte[] pcm = GeneratePcm(Mono16kHz16Bit, TimeSpan.FromSeconds(10));
        byte[] wav = WavSlicer.WrapAsWav(pcm, Mono16kHz16Bit);

        var chunks = WavSlicer.Slice(wav, TimeSpan.FromSeconds(4));

        byte[] reconstructed = chunks
            .SelectMany(c => WavSlicer.Parse(c.WavBytes).PcmData)
            .ToArray();

        Assert.Equal(pcm, reconstructed);
    }

    [Fact]
    public void Slice_TenSecondsInFourSecondWindows_ProducesThreeChunksWithShortTail()
    {
        byte[] pcm = GeneratePcm(Mono16kHz16Bit, TimeSpan.FromSeconds(10));
        byte[] wav = WavSlicer.WrapAsWav(pcm, Mono16kHz16Bit);

        var chunks = WavSlicer.Slice(wav, TimeSpan.FromSeconds(4));

        Assert.Equal(3, chunks.Count);
        Assert.Equal(4000, chunks[0].EndMs - chunks[0].StartMs);
        Assert.Equal(4000, chunks[1].EndMs - chunks[1].StartMs);
        Assert.Equal(2000, chunks[2].EndMs - chunks[2].StartMs); // sobra de 2s
    }

    [Fact]
    public void Slice_SequenceNumbers_AreContiguousStartingAtZero()
    {
        byte[] pcm = GeneratePcm(Mono16kHz16Bit, TimeSpan.FromSeconds(9));
        byte[] wav = WavSlicer.WrapAsWav(pcm, Mono16kHz16Bit);

        var chunks = WavSlicer.Slice(wav, TimeSpan.FromSeconds(2));

        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(c => c.SequenceNumber));
    }

    [Fact]
    public void Slice_EachChunk_IsIndependentlyParseableWithSameFormat()
    {
        byte[] pcm = GeneratePcm(Mono16kHz16Bit, TimeSpan.FromSeconds(6));
        byte[] wav = WavSlicer.WrapAsWav(pcm, Mono16kHz16Bit);

        var chunks = WavSlicer.Slice(wav, TimeSpan.FromSeconds(2));

        foreach (var chunk in chunks)
        {
            var (format, data) = WavSlicer.Parse(chunk.WavBytes);
            Assert.Equal(Mono16kHz16Bit, format);
            Assert.NotEmpty(data);
        }
    }

    [Fact]
    public void Slice_NonPcmFormat_Throws()
    {
        var ieeeFloatFormat = Mono16kHz16Bit with { AudioFormat = 3 };
        byte[] pcm = GeneratePcm(ieeeFloatFormat, TimeSpan.FromSeconds(1));
        byte[] wav = WavSlicer.WrapAsWav(pcm, ieeeFloatFormat);

        Assert.Throws<NotSupportedException>(() => WavSlicer.Slice(wav, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Parse_NonRiffFile_ThrowsInvalidDataException()
    {
        byte[] garbage = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07];

        Assert.Throws<InvalidDataException>(() => WavSlicer.Parse(garbage));
    }

    /// <summary>
    /// Gera PCM com padrão sequencial (não silêncio) para que qualquer corte,
    /// duplicação ou perda de byte no fatiamento seja detectável na comparação.
    /// </summary>
    private static byte[] GeneratePcm(WavFormat format, TimeSpan duration)
    {
        int length = (int)(format.ByteRate * duration.TotalSeconds);
        length -= length % format.BlockAlign;
        var pcm = new byte[length];
        for (int i = 0; i < pcm.Length; i++)
        {
            pcm[i] = (byte)(i % 256);
        }

        return pcm;
    }
}
