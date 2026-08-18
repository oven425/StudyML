using LLama;
using LLama.Common;
using LLama.Native;
using Microsoft.Extensions.AI;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace QSoft.GGUF;

public sealed class Gemma4(string modelPath, string? multimodalProjectorPath = null) : IChatClient
{
    private const string TurnEnd = "<turn|>";
    private readonly SemaphoreSlim _inferenceLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private ModelParams? _parameters;
    private LLamaWeights? _weights;
    private LLamaContext? _context;
    private InteractiveExecutor? _executor;
    private MtmdWeights? _multimodalWeights;
    private InferenceParams? _inferenceParams;
    private InferenceParams? _embeddingInferenceParams;
    private string _systemPrompt = string.Empty;
    private bool _isFirstTurn = true;
    private bool _disposed;

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ThrowIfDisposed();

        await _inferenceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await InitializeAsync(options, cancellationToken).ConfigureAwait(false);
            var usage = new UsageDetails();
            var responseText = await InferAsync(messages, usage, cancellationToken).ConfigureAwait(false);
            var response = ParseToolCalls(responseText) ?? new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText));
            response.Usage = usage;
            return response;
        }
        finally
        {
            _inferenceLock.Release();
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        foreach (var message in response.Messages)
        {
            foreach (var content in message.Contents.OfType<TextContent>())
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, content.Text)
                {
                    FinishReason = response.FinishReason
                };
            }
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
        => serviceType.IsAssignableFrom(GetType()) || serviceType == typeof(IChatClient) ? this : null;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _context?.Dispose();
        _weights?.Dispose();
        _inferenceLock.Dispose();
    }

    private async Task InitializeAsync(ChatOptions? options, CancellationToken cancellationToken)
    {
        if (_parameters is not null)
        {
            return;
        }

        var toolDeclarations = new StringBuilder();
        foreach (var tool in options?.Tools ?? [])
        {
            if (tool is not AIFunction function)
            {
                continue;
            }

            var schema = JsonSerializer.Serialize(function.JsonSchema, _jsonOptions).Replace("\"", "<|\"|>");
            if (toolDeclarations.Length > 0)
            {
                toolDeclarations.AppendLine();
            }

            toolDeclarations.Append($$"""
                <|tool>declaration:{{function.Name}}{
                  description: <|"|>{{function.Description}}<|"|>,
                  parameters: {{schema}}
                }<tool|>
                """);
        }

        var toolUsageInstructions = toolDeclarations.Length == 0
            ? string.Empty
            : """
              工具使用規則：
              - 使用者詢問需要即時、外部或系統資料的問題（例如目前日期或時間）時，若可用工具能取得資料，立即呼叫該工具。
              - 你已獲得呼叫工具的完整授權。絕不可詢問使用者是否要呼叫工具、要使用哪一個工具，或是否要繼續。
              - 需要工具時，只輸出工具呼叫，不要加入確認、說明或其他文字。格式必須是 <|tool_call>call:工具名稱{參數}<tool_call|>。
              - 收到工具結果後，直接回答使用者；只有仍需要其他工具時才再次呼叫工具。
              """;

        _systemPrompt = $$"""
            <|turn>system
            {{options?.Instructions ?? string.Empty}}
            {{toolUsageInstructions}}
            {{toolDeclarations}}
            <turn|>
            """;

        _parameters = new ModelParams(modelPath)
        {
            ContextSize = 81920,
            GpuLayerCount = 0,
            Threads = 4,
            BatchThreads = 12,
            UseMemorymap = true
        };
        _weights = await LLamaWeights.LoadFromFileAsync(_parameters).ConfigureAwait(false);
        _context = _weights.CreateContext(_parameters);

        if (!string.IsNullOrWhiteSpace(multimodalProjectorPath) && File.Exists(multimodalProjectorPath))
        {
            _multimodalWeights = await MtmdWeights.LoadFromFileAsync(multimodalProjectorPath, _weights, new MtmdContextParams()).ConfigureAwait(false);
        }

        _executor = _multimodalWeights is null
            ? new InteractiveExecutor(_context)
            : new InteractiveExecutor(_context, _multimodalWeights);
        _inferenceParams = new InferenceParams { MaxTokens = 8192, AntiPrompts = [TurnEnd] };
        _embeddingInferenceParams = new InferenceParams { MaxTokens = 0, AntiPrompts = [TurnEnd] };
    }

    private async Task<string> InferAsync(IEnumerable<ChatMessage> messages, UsageDetails usage, CancellationToken cancellationToken)
    {
        var executor = _executor ?? throw new InvalidOperationException("The model executor was not initialized.");
        var prompts = BuildPrompts(messages, executor);
        var output = new StringBuilder();

        foreach (var prompt in prompts)
        {
            var before = _context!.NativeHandle.GetTimings();
            output.Clear();
            var inferenceParams = string.IsNullOrEmpty(prompt) ? _embeddingInferenceParams! : _inferenceParams!;
            await foreach (var token in executor.InferAsync(prompt, inferenceParams, cancellationToken).ConfigureAwait(false))
            {
                output.Append(token);
            }

            AddUsage(usage, before, _context.NativeHandle.GetTimings());
        }

        return output.ToString();
    }

    private List<string> BuildPrompts(IEnumerable<ChatMessage> messages, InteractiveExecutor executor)
    {
        var prompts = new List<string>();
        var message = messages.LastOrDefault(static value => value.AdditionalProperties is null);
        if (message is null)
        {
            return prompts;
        }

        var prompt = new StringBuilder();
        if (message.Role == ChatRole.Tool)
        {
            foreach (var result in message.Contents.OfType<FunctionResultContent>())
            {
                var serializedResult = JsonSerializer.Serialize(result.Result, _jsonOptions).Replace("\"", "<|\"|>");
                prompt.Append($"<|tool_response>{serializedResult}<tool_response|>");
            }

            prompts.Add(prompt.ToString());
            return prompts;
        }

        if (message.Role != ChatRole.User)
        {
            return prompts;
        }

        prompt.Append("<|turn>user\n");
        foreach (var content in message.Contents)
        {
            if (content is TextContent text)
            {
                prompt.Append(text.Text);
            }
            else if (content is DataContent data && _multimodalWeights is not null)
            {
                executor.Embeds.Add(_multimodalWeights.LoadMedia(data.Data.Span));
            }
        }

        prompt.Append("\n<turn|>\n<|turn>model");
        if (_isFirstTurn)
        {
            if (executor.Embeds.Count == 0)
            {
                prompts.Add($"{_systemPrompt}\n{prompt}");
            }
            else
            {
                prompts.Add(_systemPrompt);
                prompts.Add(prompt.ToString());
            }

            _isFirstTurn = false;
        }
        else
        {
            prompts.Add(prompt.ToString());
        }

        return prompts;
    }

    private static ChatResponse? ParseToolCalls(string responseText)
    {
        const string toolCallStart = "<|tool_call>";
        const string toolCallEnd = "<tool_call|>";
        var calls = new List<FunctionCallContent>();
        var position = 0;

        while (true)
        {
            var start = responseText.IndexOf(toolCallStart, position, StringComparison.Ordinal);
            if (start < 0)
            {
                break;
            }

            var contentStart = start + toolCallStart.Length;
            var end = responseText.IndexOf(toolCallEnd, contentStart, StringComparison.Ordinal);
            if (end < 0 ||
                !TryParseToolCall(responseText.AsSpan(contentStart, end - contentStart), out var name, out var arguments))
            {
                return null;
            }

            calls.Add(new FunctionCallContent(Guid.NewGuid().ToString("N")[..8], name, arguments));
            position = end + toolCallEnd.Length;
        }

        return calls.Count == 0
            ? null
            : new ChatResponse(new ChatMessage(ChatRole.Assistant, [.. calls])) { FinishReason = ChatFinishReason.ToolCalls };
    }

    private static bool TryParseToolCall(
        ReadOnlySpan<char> input,
        out string name,
        out Dictionary<string, object?> arguments)
    {
        var position = 0;
        arguments = [];
        SkipWhitespace(input, ref position);
        if (!TryReadLiteral(input, ref position, "call:"))
        {
            name = string.Empty;
            return false;
        }

        var nameStart = position;
        while (position < input.Length && input[position] != '{')
        {
            position++;
        }

        name = input[nameStart..position].Trim().ToString();
        if (string.IsNullOrWhiteSpace(name) || !TryReadObject(input, ref position, out arguments))
        {
            return false;
        }

        SkipWhitespace(input, ref position);
        return position == input.Length;
    }

    private static bool TryReadObject(
        ReadOnlySpan<char> input,
        ref int position,
        out Dictionary<string, object?> result)
    {
        result = [];
        SkipWhitespace(input, ref position);
        if (!TryReadCharacter(input, ref position, '{'))
        {
            return false;
        }

        SkipWhitespace(input, ref position);
        if (TryReadCharacter(input, ref position, '}'))
        {
            return true;
        }

        while (position < input.Length)
        {
            if (!TryReadKey(input, ref position, out var key))
            {
                return false;
            }

            SkipWhitespace(input, ref position);
            if (!TryReadCharacter(input, ref position, ':') ||
                !TryReadValue(input, ref position, out var value))
            {
                return false;
            }

            result[key] = value;
            SkipWhitespace(input, ref position);
            if (TryReadCharacter(input, ref position, '}'))
            {
                return true;
            }

            if (!TryReadCharacter(input, ref position, ','))
            {
                return false;
            }

            SkipWhitespace(input, ref position);
        }

        return false;
    }

    private static bool TryReadValue(ReadOnlySpan<char> input, ref int position, out object? value)
    {
        SkipWhitespace(input, ref position);
        if (StartsWith(input, position, "<|\"|>"))
        {
            return TryReadDelimitedString(input, ref position, out value);
        }

        if (position < input.Length && input[position] == '{')
        {
            var success = TryReadObject(input, ref position, out var objectValue);
            value = objectValue;
            return success;
        }

        if (position < input.Length && input[position] == '[')
        {
            return TryReadArray(input, ref position, out value);
        }

        var start = position;
        while (position < input.Length && input[position] is not ',' and not '}' and not ']')
        {
            position++;
        }

        var token = input[start..position].Trim();
        if (token.SequenceEqual("true"))
        {
            value = true;
            return true;
        }

        if (token.SequenceEqual("false"))
        {
            value = false;
            return true;
        }

        if (token.SequenceEqual("null"))
        {
            value = null;
            return true;
        }

        if (long.TryParse(token, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var integer))
        {
            value = integer;
            return true;
        }

        if (double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number))
        {
            value = number;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryReadArray(ReadOnlySpan<char> input, ref int position, out object? value)
    {
        var result = new List<object?>();
        value = result;
        if (!TryReadCharacter(input, ref position, '['))
        {
            return false;
        }

        SkipWhitespace(input, ref position);
        if (TryReadCharacter(input, ref position, ']'))
        {
            return true;
        }

        while (position < input.Length)
        {
            if (!TryReadValue(input, ref position, out var item))
            {
                return false;
            }

            result.Add(item);
            SkipWhitespace(input, ref position);
            if (TryReadCharacter(input, ref position, ']'))
            {
                return true;
            }

            if (!TryReadCharacter(input, ref position, ','))
            {
                return false;
            }

            SkipWhitespace(input, ref position);
        }

        return false;
    }

    private static bool TryReadKey(ReadOnlySpan<char> input, ref int position, out string key)
    {
        SkipWhitespace(input, ref position);
        if (StartsWith(input, position, "<|\"|>"))
        {
            if (TryReadDelimitedString(input, ref position, out var value) && value is string text)
            {
                key = text;
                return true;
            }

            key = string.Empty;
            return false;
        }

        var start = position;
        while (position < input.Length && input[position] is not ':' and not '}' and not ',')
        {
            position++;
        }

        key = input[start..position].Trim().ToString();
        return !string.IsNullOrWhiteSpace(key);
    }

    private static bool TryReadDelimitedString(ReadOnlySpan<char> input, ref int position, out object? value)
    {
        const string delimiter = "<|\"|>";
        position += delimiter.Length;
        var end = input[position..].IndexOf(delimiter, StringComparison.Ordinal);
        if (end < 0)
        {
            value = null;
            return false;
        }

        value = input.Slice(position, end).ToString();
        position += end + delimiter.Length;
        return true;
    }

    private static bool TryReadLiteral(ReadOnlySpan<char> input, ref int position, string value)
    {
        if (!StartsWith(input, position, value))
        {
            return false;
        }

        position += value.Length;
        return true;
    }

    private static bool TryReadCharacter(ReadOnlySpan<char> input, ref int position, char value)
    {
        if (position >= input.Length || input[position] != value)
        {
            return false;
        }

        position++;
        return true;
    }

    private static bool StartsWith(ReadOnlySpan<char> input, int position, string value)
        => position <= input.Length - value.Length &&
            input[position..].StartsWith(value, StringComparison.Ordinal);

    private static void SkipWhitespace(ReadOnlySpan<char> input, ref int position)
    {
        while (position < input.Length && char.IsWhiteSpace(input[position]))
        {
            position++;
        }
    }

    private static void AddUsage(UsageDetails usage, LLamaPerfContextTimings before, LLamaPerfContextTimings after)
    {
        var inputTokens = after.PrompTokensEvaluated - before.PrompTokensEvaluated;
        var outputTokens = after.TokensEvaluated - before.TokensEvaluated;
        usage.InputTokenCount = (usage.InputTokenCount ?? 0) + inputTokens;
        usage.OutputTokenCount = (usage.OutputTokenCount ?? 0) + outputTokens;
        usage.TotalTokenCount = (usage.InputTokenCount ?? 0) + (usage.OutputTokenCount ?? 0);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
