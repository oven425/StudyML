using System.Buffers.Binary;

namespace SherpaOnnx;

internal sealed class WaveReader
{
    public WaveReader(string fileName)
    {
        using var stream = File.OpenRead(fileName);
        using var reader = new BinaryReader(stream);

        if (reader.ReadUInt32() != 0x46464952 || reader.ReadUInt32() < 4 || reader.ReadUInt32() != 0x45564157)
        {
            throw new InvalidDataException("不是有效的 RIFF/WAVE 檔案。");
        }

        short audioFormat = 0;
        short channels = 0;
        short bitsPerSample = 0;
        byte[]? audioData = null;

        while (stream.Position + 8 <= stream.Length)
        {
            var chunkId = reader.ReadUInt32();
            var chunkSize = reader.ReadUInt32();
            var nextPosition = stream.Position + chunkSize + (chunkSize & 1);

            switch (chunkId)
            {
                case 0x20746D66: // fmt
                    audioFormat = reader.ReadInt16();
                    channels = reader.ReadInt16();
                    SampleRate = reader.ReadInt32();
                    reader.ReadInt32();
                    reader.ReadInt16();
                    bitsPerSample = reader.ReadInt16();
                    break;
                case 0x61746164: // data
                    audioData = reader.ReadBytes(checked((int)chunkSize));
                    break;
            }

            stream.Position = Math.Min(nextPosition, stream.Length);
        }

        if (audioFormat != 1 || channels != 1 || bitsPerSample != 16 || audioData is null)
        {
            throw new InvalidDataException("只支援單聲道、16-bit PCM WAV 檔案。");
        }

        Samples = new float[audioData.Length / 2];
        for (var i = 0; i < Samples.Length; i++)
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(audioData.AsSpan(i * 2, 2));
            Samples[i] = sample / 32768f;
        }
    }

    public int SampleRate { get; }

    public float[] Samples { get; }
}
