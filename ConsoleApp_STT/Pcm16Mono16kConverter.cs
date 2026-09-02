using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Windows.Media.MediaProperties;

namespace QSoft.Audio;

public sealed class Pcm16Mono16kConverter
{
    private const ushort WaveFormatPcm = 1;
    private const ushort WaveFormatIeeeFloat = 3;
    private const ushort WaveFormatExtensible = 0xfffe;

    private static readonly Guid PcmSubFormat = new("00000001-0000-0010-8000-00aa00389b71");
    private static readonly Guid IeeeFloatSubFormat = new("00000003-0000-0010-8000-00aa00389b71");

    private readonly List<float> _pendingMonoSamples = new();
    private int _sourceSampleRate;
    private int _sourceChannels;
    private int _sourceBitsPerSample;
    private int _sourceBlockAlign;
    private int _sourceBytesPerSample;
    private bool _sourceIsFloat;
    private double _sourcePosition;
    private bool _isConfigured;

    public const int TargetSampleRate = 16000;
    public const int TargetChannels = 1;
    public const int TargetBitsPerSample = 16;

    public static AudioEncodingProperties CreateOutputEncodingProperties()
    {
        return AudioEncodingProperties.CreatePcm(
            TargetSampleRate,
            TargetChannels,
            TargetBitsPerSample);
    }

    public byte[] Convert(in WAVEFORMATEXTENSIBLE format, ReadOnlySpan<byte> input)
    {
        Configure(format);

        if (input.Length == 0)
            return Array.Empty<byte>();

        if (input.Length % _sourceBlockAlign != 0)
        {
            throw new ArgumentException(
                "Audio data does not contain a whole number of source frames.",
                nameof(input));
        }

        int frameCount = input.Length / _sourceBlockAlign;
        for (int frame = 0; frame < frameCount; frame++)
        {
            int frameOffset = frame * _sourceBlockAlign;
            double channelSum = 0;

            for (int channel = 0; channel < _sourceChannels; channel++)
            {
                int sampleOffset = frameOffset + channel * _sourceBytesPerSample;
                channelSum += ReadSourceSample(input, sampleOffset);
            }

            _pendingMonoSamples.Add((float)(channelSum / _sourceChannels));
        }

        double sourceSamplesPerTargetSample =
            _sourceSampleRate / (double)TargetSampleRate;
        int estimatedOutputSamples = Math.Max(
            1,
            (int)Math.Ceiling(frameCount * TargetSampleRate / (double)_sourceSampleRate));
        var outputSamples = new List<short>(estimatedOutputSamples);

        while (_sourcePosition + 1 < _pendingMonoSamples.Count)
        {
            int sampleIndex = (int)_sourcePosition;
            float fraction = (float)(_sourcePosition - sampleIndex);
            float first = _pendingMonoSamples[sampleIndex];
            float second = _pendingMonoSamples[sampleIndex + 1];
            float sample = first + (second - first) * fraction;

            outputSamples.Add(ToPcm16(sample));
            _sourcePosition += sourceSamplesPerTargetSample;
        }

        // Keep the sample at the next interpolation position for the next packet.
        int removeCount = Math.Max(0, (int)Math.Floor(_sourcePosition));
        if (removeCount > 0)
        {
            _pendingMonoSamples.RemoveRange(0, removeCount);
            _sourcePosition -= removeCount;
        }

        var output = new byte[outputSamples.Count * sizeof(short)];
        for (int i = 0; i < outputSamples.Count; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(
                output.AsSpan(i * sizeof(short), sizeof(short)),
                outputSamples[i]);
        }

        return output;
    }

    public void Reset()
    {
        _pendingMonoSamples.Clear();
        _sourcePosition = 0;
        _isConfigured = false;
    }

    private void Configure(in WAVEFORMATEXTENSIBLE format)
    {
        ushort formatTag = format.Format.wFormatTag;
        bool sourceIsFloat;

        if (formatTag == WaveFormatExtensible)
        {
            if (format.SubFormat == IeeeFloatSubFormat)
                sourceIsFloat = true;
            else if (format.SubFormat == PcmSubFormat)
                sourceIsFloat = false;
            else
                throw new NotSupportedException($"Unsupported audio subtype: {format.SubFormat}.");
        }
        else if (formatTag == WaveFormatIeeeFloat)
        {
            sourceIsFloat = true;
        }
        else if (formatTag == WaveFormatPcm)
        {
            sourceIsFloat = false;
        }
        else
        {
            throw new NotSupportedException($"Unsupported WAVE format tag: {formatTag}.");
        }

        int sampleRate = checked((int)format.Format.nSamplesPerSec);
        int channels = format.Format.nChannels;
        int bitsPerSample = format.Format.wBitsPerSample;
        int bytesPerSample = checked((bitsPerSample + 7) / 8);
        int blockAlign = format.Format.nBlockAlign;

        if (sampleRate <= 0 || channels <= 0 || bytesPerSample <= 0)
            throw new InvalidDataException("The source audio format is invalid.");

        if (sourceIsFloat && bitsPerSample != 32)
            throw new NotSupportedException("Only 32-bit IEEE Float audio is supported.");

        if (!sourceIsFloat && bitsPerSample is not (8 or 16 or 24 or 32))
            throw new NotSupportedException($"Unsupported PCM bit depth: {bitsPerSample}.");

        int minimumBlockAlign = checked(channels * bytesPerSample);
        if (blockAlign < minimumBlockAlign)
            throw new InvalidDataException("The source block alignment is invalid.");

        if (_isConfigured)
        {
            if (_sourceSampleRate != sampleRate ||
                _sourceChannels != channels ||
                _sourceBitsPerSample != bitsPerSample ||
                _sourceBlockAlign != blockAlign ||
                _sourceIsFloat != sourceIsFloat)
            {
                throw new InvalidOperationException(
                    "The source audio format changed while conversion was in progress.");
            }

            return;
        }

        _sourceSampleRate = sampleRate;
        _sourceChannels = channels;
        _sourceBitsPerSample = bitsPerSample;
        _sourceBlockAlign = blockAlign;
        _sourceBytesPerSample = bytesPerSample;
        _sourceIsFloat = sourceIsFloat;
        _isConfigured = true;
    }

    private float ReadSourceSample(ReadOnlySpan<byte> input, int offset)
    {
        if (_sourceIsFloat)
        {
            int bits = BinaryPrimitives.ReadInt32LittleEndian(input.Slice(offset, sizeof(float)));
            return BitConverter.Int32BitsToSingle(bits);
        }

        return _sourceBitsPerSample switch
        {
            8 => (input[offset] - 128) / 128f,
            16 => BinaryPrimitives.ReadInt16LittleEndian(input.Slice(offset, 2)) / 32768f,
            24 => ReadPcm24(input, offset),
            32 => BinaryPrimitives.ReadInt32LittleEndian(input.Slice(offset, 4)) / 2147483648f,
            _ => throw new InvalidOperationException("Unsupported source PCM bit depth.")
        };
    }

    private static float ReadPcm24(ReadOnlySpan<byte> input, int offset)
    {
        int value = input[offset] |
                    (input[offset + 1] << 8) |
                    (input[offset + 2] << 16);

        if ((value & 0x00800000) != 0)
            value |= unchecked((int)0xff000000);

        return value / 8388608f;
    }

    private static short ToPcm16(float sample)
    {
        if (float.IsNaN(sample))
            return 0;

        sample = Math.Clamp(sample, -1f, 1f);
        if (sample <= -1f)
            return short.MinValue;

        return (short)Math.Round(sample * short.MaxValue);
    }
}
