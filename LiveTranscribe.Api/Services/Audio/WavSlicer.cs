using System.Text;

namespace LiveTranscribe.Api.Services.Audio;

/// <summary>
/// Fatia um arquivo WAV (PCM não comprimido) em pedaços menores, cada um
/// re-empacotado como um WAV independente e válido — sem depender de ffmpeg.
/// Isso importa porque diferente de containers com estado (ex.: webm/opus),
/// um WAV recortado no meio do bloco "data" ainda é decodificável sozinho
/// desde que a gente reescreva o cabeçalho RIFF/fmt para o pedaço.
/// </summary>
public static class WavSlicer
{
    private const ushort PcmAudioFormat = 1;

    public static IReadOnlyList<WavChunk> Slice(byte[] wavFile, TimeSpan chunkDuration)
    {
        var (format, pcmData) = Parse(wavFile);

        if (format.AudioFormat != PcmAudioFormat)
        {
            throw new NotSupportedException(
                $"Apenas WAV PCM não comprimido (16 bits) é suportado nesta fase. AudioFormat recebido: {format.AudioFormat}.");
        }

        int bytesPerChunk = (int)(format.ByteRate * chunkDuration.TotalSeconds);
        bytesPerChunk -= bytesPerChunk % format.BlockAlign; // não corta uma amostra no meio
        if (bytesPerChunk <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkDuration), "Duração de chunk resulta em zero bytes.");
        }

        var chunks = new List<WavChunk>();
        int sequence = 0;
        for (int offset = 0; offset < pcmData.Length; offset += bytesPerChunk)
        {
            int length = Math.Min(bytesPerChunk, pcmData.Length - offset);
            var slice = new byte[length];
            Buffer.BlockCopy(pcmData, offset, slice, 0, length);

            long startMs = (long)(offset / (double)format.ByteRate * 1000);
            long endMs = (long)((offset + length) / (double)format.ByteRate * 1000);

            chunks.Add(new WavChunk(sequence, startMs, endMs, WrapAsWav(slice, format)));
            sequence++;
        }

        return chunks;
    }

    public static (WavFormat Format, byte[] PcmData) Parse(byte[] wavFile)
    {
        using var stream = new MemoryStream(wavFile);
        using var reader = new BinaryReader(stream);

        if (ReadTag(reader) != "RIFF")
        {
            throw new InvalidDataException("Arquivo não começa com a assinatura RIFF.");
        }

        reader.ReadUInt32(); // tamanho total do RIFF, não usado
        if (ReadTag(reader) != "WAVE")
        {
            throw new InvalidDataException("Arquivo RIFF não é do tipo WAVE.");
        }

        WavFormat? format = null;
        byte[]? data = null;

        while (stream.Position < stream.Length && (format is null || data is null))
        {
            if (stream.Length - stream.Position < 8)
            {
                break;
            }

            string tag = ReadTag(reader);
            uint size = reader.ReadUInt32();

            if (tag == "fmt ")
            {
                long chunkStart = stream.Position;
                ushort audioFormat = reader.ReadUInt16();
                ushort channels = reader.ReadUInt16();
                uint sampleRate = reader.ReadUInt32();
                reader.ReadUInt32(); // byte rate do arquivo original, recalculamos ao reempacotar
                reader.ReadUInt16(); // block align do arquivo original, idem
                ushort bitsPerSample = reader.ReadUInt16();
                format = new WavFormat(audioFormat, channels, sampleRate, bitsPerSample);

                long consumed = stream.Position - chunkStart;
                long remaining = size - consumed;
                if (remaining > 0)
                {
                    stream.Seek(remaining, SeekOrigin.Current);
                }
            }
            else if (tag == "data")
            {
                data = reader.ReadBytes((int)size);
            }
            else
            {
                stream.Seek(size, SeekOrigin.Current);
            }

            if (size % 2 == 1 && stream.Position < stream.Length)
            {
                stream.Seek(1, SeekOrigin.Current); // padding de alinhamento de word
            }
        }

        if (format is null)
        {
            throw new InvalidDataException("Chunk 'fmt ' não encontrado no WAV.");
        }

        if (data is null)
        {
            throw new InvalidDataException("Chunk 'data' não encontrado no WAV.");
        }

        return (format, data);
    }

    public static byte[] WrapAsWav(byte[] pcmData, WavFormat format)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8);
        writer.Write(36 + pcmData.Length);
        writer.Write("WAVE"u8);

        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write(format.AudioFormat);
        writer.Write(format.Channels);
        writer.Write(format.SampleRate);
        writer.Write(format.ByteRate);
        writer.Write((short)format.BlockAlign);
        writer.Write(format.BitsPerSample);

        writer.Write("data"u8);
        writer.Write(pcmData.Length);
        writer.Write(pcmData);

        return stream.ToArray();
    }

    private static string ReadTag(BinaryReader reader) =>
        Encoding.ASCII.GetString(reader.ReadBytes(4));
}
