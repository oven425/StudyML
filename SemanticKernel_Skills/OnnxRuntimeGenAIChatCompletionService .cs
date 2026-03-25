

using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntimeGenAI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Services;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace feiyun0112.SemanticKernel.Connectors.OnnxRuntimeGenAI;

/// <summary>
/// Represents a chat completion service using OnnxRuntimeGenAI.
/// </summary>
public sealed class OnnxRuntimeGenAIChatCompletionService : IChatCompletionService
{
    private readonly Model _model;
    private readonly Tokenizer _tokenizer;

    private Dictionary<string, object?> AttributesInternal { get; } = new();

    /// <summary>
    /// Initializes a new instance of the OnnxRuntimeGenAIChatCompletionService class.
    /// </summary>
    /// <param name="modelPath">The generative AI ONNX model path for the chat completion service.</param>
    /// <param name="loggerFactory">Optional logger factory to be used for logging.</param>
    public OnnxRuntimeGenAIChatCompletionService(
        string modelPath,
        ILoggerFactory? loggerFactory = null)
    {
        _model = new Model(modelPath);
        _tokenizer = new Tokenizer(_model);

        this.AttributesInternal.Add(AIServiceExtensions.ModelIdKey, _tokenizer);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?> Attributes => this.AttributesInternal;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
    {
        var result = new StringBuilder();

        await foreach (var content in RunInferenceAsync(chatHistory, executionSettings, cancellationToken))
        {
            result.Append(content);
        }

        return new List<ChatMessageContent>
        {
            new(
                role: AuthorRole.Assistant,
                content: result.ToString())
        };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
    {
        await foreach (var content in RunInferenceAsync(chatHistory, executionSettings, cancellationToken))
        {
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, content);
        }
    }

    private async IAsyncEnumerable<string> RunInferenceAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings, CancellationToken cancellationToken)
    {
        OnnxRuntimeGenAIPromptExecutionSettings onnxRuntimeGenAIPromptExecutionSettings = OnnxRuntimeGenAIPromptExecutionSettings.FromExecutionSettings(executionSettings);

        var prompt = GetPrompt(chatHistory, onnxRuntimeGenAIPromptExecutionSettings);
        var tokens = _tokenizer.Encode(prompt);

        var generatorParams = new GeneratorParams(_model);
        ApplyPromptExecutionSettings(generatorParams, onnxRuntimeGenAIPromptExecutionSettings);
        //generatorParams.AppendTokenSequences(tokens);

        var generator = new Generator(_model, generatorParams);
        generator.AppendTokenSequences(tokens);
        while (!generator.IsDone())
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return await Task.Run(() =>
            {
                //generator.ComputeLogits();
                generator.GenerateNextToken();

                var outputTokens = generator.GetSequence(0);
                var newToken = outputTokens.Slice(outputTokens.Length - 1, 1);
                var output = _tokenizer.Decode(newToken);
                return output;
            }, cancellationToken);
        }
    }

    private string GetPrompt(ChatHistory chatHistory, OnnxRuntimeGenAIPromptExecutionSettings onnxRuntimeGenAIPromptExecutionSettings)
    {
        var promptBuilder = new StringBuilder();
        foreach (var message in chatHistory)
        {
            promptBuilder.Append($"<|{message.Role}|>\n{message.Content}");
        }
        promptBuilder.Append($"<|end|>\n<|assistant|>");

        return promptBuilder.ToString();
    }

    private void ApplyPromptExecutionSettings(GeneratorParams generatorParams, OnnxRuntimeGenAIPromptExecutionSettings onnxRuntimeGenAIPromptExecutionSettings)
    {
        generatorParams.SetSearchOption("top_p", onnxRuntimeGenAIPromptExecutionSettings.TopP);
        generatorParams.SetSearchOption("top_k", onnxRuntimeGenAIPromptExecutionSettings.TopK);
        generatorParams.SetSearchOption("temperature", onnxRuntimeGenAIPromptExecutionSettings.Temperature);
        generatorParams.SetSearchOption("repetition_penalty", onnxRuntimeGenAIPromptExecutionSettings.RepetitionPenalty);
        generatorParams.SetSearchOption("past_present_share_buffer", onnxRuntimeGenAIPromptExecutionSettings.PastPresentShareBuffer);
        generatorParams.SetSearchOption("num_return_sequences", onnxRuntimeGenAIPromptExecutionSettings.NumReturnSequences);
        generatorParams.SetSearchOption("no_repeat_ngram_size", onnxRuntimeGenAIPromptExecutionSettings.NoRepeatNgramSize);
        generatorParams.SetSearchOption("min_length", onnxRuntimeGenAIPromptExecutionSettings.MinLength);
        generatorParams.SetSearchOption("max_length", onnxRuntimeGenAIPromptExecutionSettings.MaxLength);
        generatorParams.SetSearchOption("length_penalty", onnxRuntimeGenAIPromptExecutionSettings.LengthPenalty);
        generatorParams.SetSearchOption("early_stopping", onnxRuntimeGenAIPromptExecutionSettings.EarlyStopping);
        generatorParams.SetSearchOption("do_sample", onnxRuntimeGenAIPromptExecutionSettings.DoSample);
        generatorParams.SetSearchOption("diversity_penalty", onnxRuntimeGenAIPromptExecutionSettings.DiversityPenalty);
    }
}

public sealed class OnnxRuntimeGenAIPromptExecutionSettings : PromptExecutionSettings
{
    public static OnnxRuntimeGenAIPromptExecutionSettings FromExecutionSettings(PromptExecutionSettings? executionSettings)
    {
        switch (executionSettings)
        {
            case OnnxRuntimeGenAIPromptExecutionSettings settings:
                return settings;
            default:
                return new OnnxRuntimeGenAIPromptExecutionSettings();
        }
    }

    private int _topK = 50;
    private float _topP = 0.9f;
    private float _temperature = 1;
    private float _repetitionPenalty = 1;
    private bool _pastPresentShareBuffer = false;
    private int _numReturnSequences = 1;
    private int _numBeams = 1;
    private int _noRepeatNgramSize = 0;
    private int _minLength = 0;
    private int _maxLength = 200;
    private float _lengthPenalty = 1;
    private bool _earlyStopping = true;
    private bool _doSample = false;
    private float _diversityPenalty = 0;

    [JsonPropertyName("top_k")]
    public int TopK
    {
        get { return _topK; }
        set { _topK = value; }
    }

    [JsonPropertyName("top_p")]
    public float TopP
    {
        get { return _topP; }
        set { _topP = value; }
    }

    [JsonPropertyName("temperature")]
    public float Temperature
    {
        get { return _temperature; }
        set { _temperature = value; }
    }

    [JsonPropertyName("repetition_penalty")]
    public float RepetitionPenalty
    {
        get { return _repetitionPenalty; }
        set { _repetitionPenalty = value; }
    }

    [JsonPropertyName("past_present_share_buffer")]
    public bool PastPresentShareBuffer
    {
        get { return _pastPresentShareBuffer; }
        set { _pastPresentShareBuffer = value; }
    }

    [JsonPropertyName("num_return_sequences")]
    public int NumReturnSequences
    {
        get { return _numReturnSequences; }
        set { _numReturnSequences = value; }
    }

    [JsonPropertyName("num_beams")]
    public int NumBeams
    {
        get { return _numBeams; }
        set { _numBeams = value; }
    }

    [JsonPropertyName("no_repeat_ngram_size")]
    public int NoRepeatNgramSize
    {
        get { return _noRepeatNgramSize; }
        set { _noRepeatNgramSize = value; }
    }

    [JsonPropertyName("min_length")]
    public int MinLength
    {
        get { return _minLength; }
        set { _minLength = value; }
    }

    [JsonPropertyName("max_length")]
    public int MaxLength
    {
        get { return _maxLength; }
        set { _maxLength = value; }
    }

    [JsonPropertyName("length_penalty")]
    public float LengthPenalty
    {
        get { return _lengthPenalty; }
        set { _lengthPenalty = value; }
    }

    [JsonPropertyName("diversity_penalty")]
    public float DiversityPenalty
    {
        get { return _diversityPenalty; }
        set { _diversityPenalty = value; }
    }

    [JsonPropertyName("early_stopping")]
    public bool EarlyStopping
    {
        get { return _earlyStopping; }
        set { _earlyStopping = value; }
    }

    [JsonPropertyName("do_sample")]
    public bool DoSample
    {
        get { return _doSample; }
        set { _doSample = value; }
    }
}