namespace ConsoleAppSLMT.Gguf;

internal enum GgufValueType : uint
{
    UInt8 = 0,
    Int8 = 1,
    UInt16 = 2,
    Int16 = 3,
    UInt32 = 4,
    Int32 = 5,
    Float32 = 6,
    Bool = 7,
    String = 8,
    Array = 9,
    UInt64 = 10,
    Int64 = 11,
    Float64 = 12
}

internal sealed record GgufArray(
    GgufValueType ElementType,
    IReadOnlyList<object?> Values);

internal sealed record GgufTensorInfo(
    string Name,
    IReadOnlyList<ulong> Dimensions,
    uint GgmlType,
    ulong RelativeOffset)
{
    public string GgmlTypeName => GgmlTypeNames.GetName(GgmlType);

    public ulong ElementCount
    {
        get
        {
            var count = 1UL;
            foreach (var dimension in Dimensions)
            {
                count = checked(count * dimension);
            }

            return count;
        }
    }
}

internal sealed record GgufDocument(
    string FilePath,
    long FileSize,
    uint Version,
    IReadOnlyDictionary<string, object?> Metadata,
    IReadOnlyList<GgufTensorInfo> Tensors,
    uint Alignment,
    long TensorDataOffset);

internal static class GgmlTypeNames
{
    public static string GetName(uint value) => value switch
    {
        0 => "F32",
        1 => "F16",
        2 => "Q4_0",
        3 => "Q4_1",
        6 => "Q5_0",
        7 => "Q5_1",
        8 => "Q8_0",
        9 => "Q8_1",
        10 => "Q2_K",
        11 => "Q3_K",
        12 => "Q4_K",
        13 => "Q5_K",
        14 => "Q6_K",
        15 => "Q8_K",
        16 => "IQ2_XXS",
        17 => "IQ2_XS",
        18 => "IQ3_XXS",
        19 => "IQ1_S",
        20 => "IQ4_NL",
        21 => "IQ3_S",
        22 => "IQ2_S",
        23 => "IQ4_XS",
        24 => "I8",
        25 => "I16",
        26 => "I32",
        27 => "I64",
        28 => "F64",
        29 => "IQ1_M",
        30 => "BF16",
        _ => $"UNKNOWN({value})"
    };

    public static bool TryGetElementByteSize(uint value, out uint byteSize)
    {
        byteSize = value switch
        {
            0 or 26 => 4,
            1 or 25 or 30 => 2,
            24 => 1,
            27 or 28 => 8,
            _ => 0
        };

        return byteSize != 0;
    }
}
