using System.Globalization;
using System.Text;

namespace ConsoleAppSLMT.Gguf;

internal static class GgufReader
{
    private const uint Magic = 0x46554747;
    private const int MaximumMetadataCount = 1_000_000;
    private const int MaximumTensorCount = 1_000_000;
    private const int MaximumArrayElements = 10_000_000;
    private const int MaximumStringByteCount = 256 * 1024 * 1024;
    private const uint DefaultAlignment = 32;
    private const uint MaximumAlignment = 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static GgufDocument Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        using var reader = new BinaryReader(stream, Utf8, leaveOpen: true);

        try
        {
            if (stream.Length < 24)
            {
                throw new InvalidDataException("檔案小於 GGUF v2/v3 header 的 24 bytes。");
            }

            var magic = reader.ReadUInt32();
            if (magic != Magic)
            {
                throw new InvalidDataException($"GGUF magic 不正確：0x{magic:X8}。");
            }

            var version = reader.ReadUInt32();
            if (version is not (2 or 3))
            {
                throw new NotSupportedException($"目前只支援 GGUF v2/v3，檔案版本為 v{version}。");
            }

            var tensorCount = ReadCount(reader.ReadUInt64(), MaximumTensorCount, "tensor");
            var metadataCount = ReadCount(reader.ReadUInt64(), MaximumMetadataCount, "metadata");
            var metadata = ReadMetadata(reader, metadataCount);
            var tensors = ReadTensorDirectory(reader, tensorCount);
            var alignment = ReadAlignment(metadata);
            var tensorDataOffset = Align(stream.Position, alignment);

            ValidateTensorData(stream.Length, tensorDataOffset, alignment, tensors);

            return new GgufDocument(
                fullPath,
                stream.Length,
                version,
                metadata,
                tensors,
                alignment,
                tensorDataOffset);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("GGUF 在結構解析完成前已到達檔案結尾。", exception);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("GGUF 包含無效的 UTF-8 字串。", exception);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException("GGUF 包含超出支援範圍的數值。", exception);
        }
    }

    private static Dictionary<string, object?> ReadMetadata(BinaryReader reader, int count)
    {
        var metadata = new Dictionary<string, object?>(count, StringComparer.Ordinal);

        for (var index = 0; index < count; index++)
        {
            var key = ReadString(reader);
            if (string.IsNullOrEmpty(key))
            {
                throw new InvalidDataException($"Metadata #{index} 的 key 不可為空。");
            }

            var valueType = ReadValueType(reader);
            var value = ReadValue(reader, valueType);
            if (!metadata.TryAdd(key, value))
            {
                throw new InvalidDataException($"Metadata key 重複：{key}。");
            }
        }

        return metadata;
    }

    private static List<GgufTensorInfo> ReadTensorDirectory(BinaryReader reader, int count)
    {
        var tensors = new List<GgufTensorInfo>(count);
        var names = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < count; index++)
        {
            var name = ReadString(reader);
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidDataException($"Tensor #{index} 的名稱不可為空。");
            }

            if (!names.Add(name))
            {
                throw new InvalidDataException($"Tensor 名稱重複：{name}。");
            }

            var dimensionCount = ReadCount(reader.ReadUInt32(), 4, $"tensor {name} dimension");
            if (dimensionCount == 0)
            {
                throw new InvalidDataException($"Tensor {name} 必須至少有一個維度。");
            }

            var dimensions = new ulong[dimensionCount];
            for (var dimensionIndex = 0; dimensionIndex < dimensions.Length; dimensionIndex++)
            {
                dimensions[dimensionIndex] = reader.ReadUInt64();
                if (dimensions[dimensionIndex] == 0)
                {
                    throw new InvalidDataException($"Tensor {name} 的維度不可為 0。");
                }
            }

            tensors.Add(new GgufTensorInfo(
                name,
                dimensions,
                reader.ReadUInt32(),
                reader.ReadUInt64()));
        }

        return tensors;
    }

    private static object? ReadValue(BinaryReader reader, GgufValueType valueType) => valueType switch
    {
        GgufValueType.UInt8 => reader.ReadByte(),
        GgufValueType.Int8 => reader.ReadSByte(),
        GgufValueType.UInt16 => reader.ReadUInt16(),
        GgufValueType.Int16 => reader.ReadInt16(),
        GgufValueType.UInt32 => reader.ReadUInt32(),
        GgufValueType.Int32 => reader.ReadInt32(),
        GgufValueType.Float32 => reader.ReadSingle(),
        GgufValueType.Bool => ReadBoolean(reader),
        GgufValueType.String => ReadString(reader),
        GgufValueType.Array => ReadArray(reader),
        GgufValueType.UInt64 => reader.ReadUInt64(),
        GgufValueType.Int64 => reader.ReadInt64(),
        GgufValueType.Float64 => reader.ReadDouble(),
        _ => throw new InvalidDataException($"不支援的 GGUF value type：{(uint)valueType}。")
    };

    private static GgufArray ReadArray(BinaryReader reader)
    {
        var elementType = ReadValueType(reader);
        if (elementType == GgufValueType.Array)
        {
            throw new InvalidDataException("GGUF array 不可包含 array。");
        }

        var count = ReadCount(reader.ReadUInt64(), MaximumArrayElements, "array element");
        var values = new object?[count];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = ReadValue(reader, elementType);
        }

        return new GgufArray(elementType, values);
    }

    private static GgufValueType ReadValueType(BinaryReader reader)
    {
        var rawValue = reader.ReadUInt32();
        if (rawValue > (uint)GgufValueType.Float64)
        {
            throw new InvalidDataException($"無效的 GGUF value type：{rawValue}。");
        }

        return (GgufValueType)rawValue;
    }

    private static bool ReadBoolean(BinaryReader reader) => reader.ReadByte() switch
    {
        0 => false,
        1 => true,
        var value => throw new InvalidDataException($"無效的 GGUF bool 值：{value}。")
    };

    private static string ReadString(BinaryReader reader)
    {
        var byteCount = reader.ReadUInt64();
        if (byteCount > MaximumStringByteCount)
        {
            throw new InvalidDataException($"GGUF 字串過大：{byteCount:N0} bytes。");
        }

        var bytes = GC.AllocateUninitializedArray<byte>(checked((int)byteCount));
        reader.ReadExactly(bytes);
        return Utf8.GetString(bytes);
    }

    private static uint ReadAlignment(IReadOnlyDictionary<string, object?> metadata)
    {
        if (!metadata.TryGetValue("general.alignment", out var value))
        {
            return DefaultAlignment;
        }

        uint alignment;
        try
        {
            alignment = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
        {
            throw new InvalidDataException("general.alignment 必須是有效的無號整數。", exception);
        }

        if (alignment is 0 or > MaximumAlignment || (alignment & (alignment - 1)) != 0)
        {
            throw new InvalidDataException($"general.alignment 必須是 1 到 {MaximumAlignment} 的 2 次方，目前為 {alignment}。");
        }

        return alignment;
    }

    private static void ValidateTensorData(
        long fileSize,
        long tensorDataOffset,
        uint alignment,
        IReadOnlyList<GgufTensorInfo> tensors)
    {
        if (tensorDataOffset > fileSize)
        {
            throw new InvalidDataException("Tensor data 起始位置超出檔案範圍。");
        }

        var tensorDataLength = checked((ulong)(fileSize - tensorDataOffset));
        foreach (var tensor in tensors)
        {
            _ = tensor.ElementCount;

            if (tensor.RelativeOffset % alignment != 0)
            {
                throw new InvalidDataException($"Tensor {tensor.Name} 的 offset 未依 {alignment} bytes 對齊。");
            }

            if (tensor.RelativeOffset >= tensorDataLength)
            {
                throw new InvalidDataException($"Tensor {tensor.Name} 的 offset 超出 tensor data 範圍。");
            }

            if (GgmlTypeNames.TryGetElementByteSize(tensor.GgmlType, out var elementByteSize))
            {
                var byteCount = checked(tensor.ElementCount * elementByteSize);
                if (byteCount > tensorDataLength - tensor.RelativeOffset)
                {
                    throw new InvalidDataException($"Tensor {tensor.Name} 的資料超出檔案範圍。");
                }
            }
        }
    }

    private static int ReadCount(ulong value, int maximum, string name)
    {
        if (value > (ulong)maximum)
        {
            throw new InvalidDataException($"{name} 數量不合理：{value:N0}，上限為 {maximum:N0}。");
        }

        return checked((int)value);
    }

    private static long Align(long position, uint alignment) =>
        checked((position + alignment - 1) / alignment * alignment);
}
