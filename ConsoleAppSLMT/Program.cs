using System.Globalization;
using ConsoleAppSLMT.Gguf;

// Download: https://huggingface.co/bartowski/SmolLM2-135M-Instruct-GGUF/resolve/main/SmolLM2-135M-Instruct-f16.gguf
const string defaultModelFileName = "SmolLM2-135M-Instruct-f16.gguf";

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase))
{
    PrintUsage();
    return;
}

var showAllTensors = args.Contains("--all", StringComparer.OrdinalIgnoreCase);
var dumpTensorName = GetOptionValue(args, "--dump-tensor");
var suppliedPath = FindModelPathArgument(args);
var modelPath = suppliedPath is null ? FindDefaultModelPath() : Path.GetFullPath(suppliedPath);

if (modelPath is null || !File.Exists(modelPath))
{
    Console.Error.WriteLine("找不到 GGUF 模型。");
    Console.Error.WriteLine($"預設檔名：{defaultModelFileName}");
    PrintUsage();
    Environment.ExitCode = 1;
    return;
}

try
{
    var document = GgufReader.Read(modelPath);
    PrintDocument(document, showAllTensors);

    if (dumpTensorName is not null)
    {
        DumpTensor(document, dumpTensorName);
    }
}
catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException)
{
    Console.Error.WriteLine($"GGUF 解析失敗：{exception.Message}");
    Environment.ExitCode = 1;
}

static string? FindDefaultModelPath()
{
    string[] candidates =
    [
        Path.Combine(Environment.CurrentDirectory, "models", defaultModelFileName),
        Path.Combine(Environment.CurrentDirectory, "ConsoleAppSLMT", "models", defaultModelFileName),
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "models", defaultModelFileName))
    ];

    return candidates.FirstOrDefault(File.Exists);
}

static string? GetOptionValue(string[] arguments, string optionName)
{
    for (var index = 0; index < arguments.Length; index++)
    {
        if (string.Equals(arguments[index], optionName, StringComparison.OrdinalIgnoreCase))
        {
            return index + 1 < arguments.Length ? arguments[index + 1] : null;
        }
    }

    return null;
}

static string? FindModelPathArgument(string[] arguments)
{
    for (var index = 0; index < arguments.Length; index++)
    {
        var argument = arguments[index];
        if (string.Equals(argument, "--dump-tensor", StringComparison.OrdinalIgnoreCase))
        {
            index++; // 跳過緊接在後面的 tensor 名稱，避免被誤認為模型路徑。
            continue;
        }

        if (argument.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        return argument;
    }

    return null;
}

static void PrintDocument(GgufDocument document, bool showAllTensors)
{
    Console.WriteLine("GGUF 模型摘要");
    Console.WriteLine(new string('=', 72));
    Console.WriteLine($"檔案          : {document.FilePath}");
    Console.WriteLine($"檔案大小      : {FormatByteCount(document.FileSize)}");
    Console.WriteLine($"GGUF 版本     : {document.Version}");
    Console.WriteLine($"Metadata 數量 : {document.Metadata.Count:N0}");
    Console.WriteLine($"Tensor 數量   : {document.Tensors.Count:N0}");
    Console.WriteLine($"資料對齊      : {document.Alignment:N0} bytes");
    Console.WriteLine($"Tensor data   : 0x{document.TensorDataOffset:X}");

    Console.WriteLine();
    Console.WriteLine("Metadata");
    Console.WriteLine(new string('-', 72));
    foreach (var (key, value) in document.Metadata.OrderBy(static item => item.Key, StringComparer.Ordinal))
    {
        Console.WriteLine($"{key} = {FormatMetadataValue(value)}");
    }

    Console.WriteLine();
    Console.WriteLine("Tensor 型別統計");
    Console.WriteLine(new string('-', 72));
    foreach (var group in document.Tensors
        .GroupBy(static tensor => tensor.GgmlTypeName)
        .OrderBy(static group => group.Key, StringComparer.Ordinal))
    {
        Console.WriteLine($"{group.Key,-14} {group.Count(),6:N0}");
    }

    Console.WriteLine();
    Console.WriteLine(showAllTensors ? "全部 Tensors" : "Tensors（前 30 筆；加上 --all 顯示全部）");
    Console.WriteLine(new string('-', 72));

    var tensors = showAllTensors ? document.Tensors : document.Tensors.Take(30);
    foreach (var tensor in tensors)
    {
        var dimensions = string.Join(" × ", tensor.Dimensions);
        var absoluteOffset = checked(document.TensorDataOffset + (long)tensor.RelativeOffset);
        Console.WriteLine(
            $"{tensor.Name,-44} [{dimensions}] {tensor.GgmlTypeName,-8} @0x{absoluteOffset:X}");
    }
}

static string FormatMetadataValue(object? value) => value switch
{
    null => "null",
    string text => FormatString(text),
    bool boolean => boolean ? "true" : "false",
    GgufArray array => FormatArray(array),
    IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
    _ => value.ToString() ?? string.Empty
};

static string FormatArray(GgufArray array)
{
    var preview = string.Join(
        ", ",
        array.Values.Take(5).Select(FormatMetadataValue));
    var suffix = array.Values.Count > 5 ? ", …" : string.Empty;
    return $"{array.ElementType}[{array.Values.Count:N0}] {{ {preview}{suffix} }}";
}

static string FormatString(string value)
{
    var singleLine = value.Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal);
    var preview = singleLine.Length <= 160 ? singleLine : singleLine[..160] + "…";
    return $"\"{preview}\"";
}

static string FormatByteCount(long value)
{
    string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
    var size = (double)value;
    var unit = 0;
    while (size >= 1024 && unit < units.Length - 1)
    {
        size /= 1024;
        unit++;
    }

    return $"{size:N2} {units[unit]} ({value:N0} bytes)";
}

static void DumpTensor(GgufDocument document, string tensorName)
{
    using var reader = new GgufTensorReader(document);

    GgufTensor tensor;
    try
    {
        tensor = reader.Load(document, tensorName);
    }
    catch (Exception exception) when (exception is KeyNotFoundException or NotSupportedException or InvalidDataException)
    {
        Console.Error.WriteLine(exception.Message);
        return;
    }

    var data = tensor.Data;
    var min = float.PositiveInfinity;
    var max = float.NegativeInfinity;
    double sum = 0;
    var nanCount = 0;
    var infinityCount = 0;

    foreach (var value in data)
    {
        if (float.IsNaN(value))
        {
            nanCount++;
            continue;
        }

        if (float.IsInfinity(value))
        {
            infinityCount++;
            continue;
        }

        min = Math.Min(min, value);
        max = Math.Max(max, value);
        sum += value;
    }

    var mean = sum / data.Length;
    var dimensions = string.Join(" × ", tensor.Dimensions);
    var previewCount = Math.Min(8, data.Length);
    var preview = string.Join(
        ", ",
        data.Take(previewCount).Select(static value => value.ToString("G6", CultureInfo.InvariantCulture)));

    Console.WriteLine();
    Console.WriteLine($"Tensor dump   : {tensor.Name}");
    Console.WriteLine(new string('-', 72));
    Console.WriteLine($"形狀          : [{dimensions}] ({tensor.Info.GgmlTypeName})");
    Console.WriteLine($"元素數量      : {data.Length:N0}");
    Console.WriteLine($"min / max     : {min:G6} / {max:G6}");
    Console.WriteLine($"mean          : {mean:G6}");
    Console.WriteLine($"NaN / Inf 數量: {nanCount:N0} / {infinityCount:N0}");
    Console.WriteLine($"前 {previewCount} 個值    : [{preview}]");
}

static void PrintUsage()
{
    Console.WriteLine("用法：");
    Console.WriteLine("  dotnet run --project ConsoleAppSLMT");
    Console.WriteLine("  dotnet run --project ConsoleAppSLMT -- <model.gguf>");
    Console.WriteLine("  dotnet run --project ConsoleAppSLMT -- <model.gguf> --all");
    Console.WriteLine("  dotnet run --project ConsoleAppSLMT -- <model.gguf> --dump-tensor <tensor 名稱>");
}
