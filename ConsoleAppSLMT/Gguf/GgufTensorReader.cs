using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace ConsoleAppSLMT.Gguf;

/// <summary>
/// 一個已載入的 tensor：附帶 <see cref="GgufTensorInfo"/> 描述資訊與展開後的 F32 權重值。
/// </summary>
internal sealed record GgufTensor(GgufTensorInfo Info, float[] Data)
{
    public string Name => Info.Name;

    public IReadOnlyList<ulong> Dimensions => Info.Dimensions;
}

/// <summary>
/// 對 GGUF 檔案的 tensor data 區段提供隨機存取，並將原始 bytes 轉換為 <see cref="float"/>。
/// 以 <see cref="MemoryMappedFile"/> 開檔，避免一次把整個模型讀進記憶體；
/// 每次讀取單一 tensor 時才把該 tensor 的 bytes 複製出來並就地轉型/轉換精度。
/// </summary>
internal sealed class GgufTensorReader : IDisposable
{
    private readonly MemoryMappedFile _file;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly long _tensorDataLength;
    private bool _disposed;

    public GgufTensorReader(GgufDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        _tensorDataLength = document.FileSize - document.TensorDataOffset;
        if (_tensorDataLength <= 0)
        {
            throw new InvalidDataException("GGUF 檔案沒有 tensor data 區段。");
        }

        _file = MemoryMappedFile.CreateFromFile(
            document.FilePath,
            FileMode.Open,
            mapName: null,
            capacity: 0,
            MemoryMappedFileAccess.Read);

        try
        {
            _accessor = _file.CreateViewAccessor(
                document.TensorDataOffset,
                _tensorDataLength,
                MemoryMappedFileAccess.Read);
        }
        catch
        {
            _file.Dispose();
            throw;
        }
    }

    /// <summary>讀取單一 tensor 的原始 bytes（未做任何精度轉換）。</summary>
    public byte[] ReadRawBytes(GgufTensorInfo tensor)
    {
        ArgumentNullException.ThrowIfNull(tensor);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!GgmlTypeNames.TryGetElementByteSize(tensor.GgmlType, out var elementByteSize))
        {
            throw new NotSupportedException(
                $"Tensor {tensor.Name} 使用尚未支援讀取的型別 {tensor.GgmlTypeName}（可能是量化格式）。");
        }

        var byteCount = checked(tensor.ElementCount * elementByteSize);
        if (checked(tensor.RelativeOffset + byteCount) > (ulong)_tensorDataLength)
        {
            throw new InvalidDataException($"Tensor {tensor.Name} 的資料超出 tensor data 範圍。");
        }

        var buffer = GC.AllocateUninitializedArray<byte>(checked((int)byteCount));
        _accessor.ReadArray(checked((long)tensor.RelativeOffset), buffer, 0, buffer.Length);
        return buffer;
    }

    /// <summary>讀取 tensor 並轉換為 F32；目前支援 F32/F16/BF16，其餘（量化）型別會丟出例外。</summary>
    public float[] ReadAsFloat32(GgufTensorInfo tensor)
    {
        var raw = ReadRawBytes(tensor);
        var elementCount = checked((int)tensor.ElementCount);
        var result = new float[elementCount];

        switch (tensor.GgmlTypeName)
        {
            case "F32":
                MemoryMarshal.Cast<byte, float>(raw).CopyTo(result);
                break;

            case "F16":
                var halves = MemoryMarshal.Cast<byte, Half>(raw);
                for (var i = 0; i < result.Length; i++)
                {
                    result[i] = (float)halves[i];
                }

                break;

            case "BF16":
                // BF16 是 F32 的高 16 bits；左移補 0 即為對應的 F32 位元組樣式。
                for (var i = 0; i < result.Length; i++)
                {
                    var bits = (uint)(raw[i * 2] | (raw[i * 2 + 1] << 8));
                    result[i] = BitConverter.UInt32BitsToSingle(bits << 16);
                }

                break;

            default:
                throw new NotSupportedException(
                    $"Tensor {tensor.Name} 使用尚未支援轉換為 F32 的型別 {tensor.GgmlTypeName}（可能是量化格式）。");
        }

        return result;
    }

    /// <summary>讀取並包裝成 <see cref="GgufTensor"/>。</summary>
    public GgufTensor Load(GgufTensorInfo tensor) => new(tensor, ReadAsFloat32(tensor));

    /// <summary>依名稱在 <paramref name="document"/> 中尋找 tensor 並讀取。</summary>
    public GgufTensor Load(GgufDocument document, string tensorName)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(tensorName);

        foreach (var tensor in document.Tensors)
        {
            if (string.Equals(tensor.Name, tensorName, StringComparison.Ordinal))
            {
                return Load(tensor);
            }
        }

        throw new KeyNotFoundException($"找不到名稱為 \"{tensorName}\" 的 tensor。");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _accessor.Dispose();
        _file.Dispose();
    }
}
