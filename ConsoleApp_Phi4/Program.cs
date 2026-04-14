// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.AI;
using Microsoft.ML.OnnxRuntimeGenAI;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

string modelPath = @"..\..\..\..\onnx_phi4\CPU";
modelPath = "..\\..\\..\\..\\onnx_phi4\\CPU-Phi-4-mini-instruct-onnx";
var fullpath = Path.GetFullPath(modelPath);
System.Diagnostics.Trace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
using Config config = new Config(modelPath);
config.ClearProviders();
////config.AppendProvider("DML");
///
Orgg();
//await onnxclient();

async Task onnxclient()
{
    var strb = new StringBuilder();
    strb.Append("<|system|>You are a helpful assistant with some tools.<|tool|>[{\"\"name\"\": \"\"getcomputerdatetime\"\", \"\"description\"\": \"\"Gets the current date and time of this computer.\"\", \"\"parameters\"\": {}}]<|/tool|><|end|><|user|>What time is this computer?<|end|><|assistant|>");
    OnnxRuntimeGenAIChatClientOptions1 options = new OnnxRuntimeGenAIChatClientOptions1
    {
        PromptFormatter = (chatmsgs, opt) =>
        {
            var tools = @"[{""name"": ""getcomputerdatetime"", ""description"": ""Gets the current date and time of this computer."", ""parameters"": {}}]";
            var prompt = @"<|system|>You are a helpful assistant with some tools.<|tool|>[{""name"": ""getcomputerdatetime"", ""description"": ""Gets the current date and time of this computer."", ""parameters"": {}}]<|/tool|><|end|><|user|>What time is this computer?<|end|><|assistant|>";
            tools = @"[{""name"": ""getcomputertime"", ""description"": ""Gets the current time of this computer."", ""parameters"": {}},{""name"": ""getcomputerdate"", ""description"": ""Gets the current date of this computer."", ""parameters"": {}}]";
            return prompt;
        }
    };
    var cc = new OnnxRuntimeGenAIChatClient1(fullpath, options);
    ChatMessage systemMessage = new(ChatRole.System, "You are a helpful assistant with some tools.");
    ChatMessage toolMessage = new(ChatRole.Tool, @"[{""name"": ""getcomputerdatetime"", ""description"": ""Gets the current date and time of this computer."", ""parameters"": {}}]");
    ChatMessage userMessage = new(ChatRole.User, "What time is this computer?");
    ChatMessage assistantMessage = new(ChatRole.Assistant, "");

    ChatOptions chatOptions = new ChatOptions();
    chatOptions.Temperature = 0.0f;
    chatOptions.TopP = 1.0f;
    chatOptions.MaxOutputTokens = 300;
    //chatOptions.ResponseFormat = ChatResponseFormat.Json;

    var strb_resp = new StringBuilder();
    await foreach (var oo in cc.GetStreamingResponseAsync([systemMessage, toolMessage, userMessage, assistantMessage], chatOptions))
    {
        strb_resp.Append(oo);
        Console.Write(oo);
    }

    System.Diagnostics.Trace.WriteLine(strb_resp.ToString());

    ChatMessage toolMessageresult = new(ChatRole.Tool, $"{DateTime.Now}");
    await foreach (var oo in cc.GetStreamingResponseAsync([systemMessage, toolMessage, userMessage, assistantMessage, toolMessageresult, assistantMessage], chatOptions))
    {
        Console.Write(oo);
    }
}

//<| system |>
//[系統指令：你是一個助理...]
//<| tool |>
//[JSON 格式的工具清單]
//<|/ tool |>
//<| end |>
//<| user |>
//[使用者的問題]
//<| end |>
//<| assistant |>
void Orgg()
{
    var tools = @"[{""name"": ""getcomputerdatetime"", ""description"": ""Gets the current date and time of this computer."", ""parameters"": {}}]";
    var prompt = @"<|system|>You are a helpful assistant with some tools.<|tool|>[{""name"": ""getcomputerdatetime"", ""description"": ""Gets the current date and time of this computer."", ""parameters"": {}}]<|/tool|><|end|><|user|>What time is this computer?<|end|><|assistant|>";
    tools = @"[{""name"": ""getcomputertime"", ""description"": ""Gets the current time of this computer."", ""parameters"": {}},{""name"": ""getcomputerdate"", ""description"": ""Gets the current date of this computer."", ""parameters"": {}}]";

    prompt = $@"<|system|>You are a helpful assistant with some tools.<|tool|>{tools}<|/tool|><|end|><|user|>What date and time is this computer?<|end|><|assistant|>";

    prompt = $@"<|im_start|>system
You are a helpful assistant with some tools.
Here are the available tools:
<|tool|>
[{{""name"": ""getcomputertime"", ""description"": ""Gets the current time of this computer."", ""parameters"": {{}}}},
 {{""name"": ""getcomputerdate"", ""description"": ""Gets the current date of this computer."", ""parameters"": {{}}}}]
<|/tool|>
<|im_end|>
<|im_start|>user
What date and time is this computer?
<|im_end|>
<|im_start|>assistant";
    prompt = prompt.Replace("\r\n", "");
    fullpath = Path.GetFullPath(modelPath);
    using Model model = new(fullpath);
    using Tokenizer tokenizer = new(model);

    do
    {
        var sequences = tokenizer.Encode(prompt);
        var strb = new StringBuilder();
        using GeneratorParams generatorParams = new GeneratorParams(model);
        generatorParams.SetSearchOption("min_length", 1);
        generatorParams.SetSearchOption("max_length", 300);
        generatorParams.SetSearchOption("temperature", 0.6f);
        generatorParams.SetSearchOption("top_p", 1.0f);
        //generatorParams.SetGuidance("json_schema", "{}");

        using var generator = new Generator(model, generatorParams);
        generator.AppendTokenSequences(sequences);

        var watch = System.Diagnostics.Stopwatch.StartNew();

        using var tokenizerStream = tokenizer.CreateStream();
        while (!generator.IsDone())
        {
            try
            {
                //generator.GenerateNextToken();
                //var lastToken = generator.GetSequence(0)[^1];
                //strb.Append(tokenizerStream.Decode(lastToken));

                generator.GenerateNextToken();
                string lastToken = tokenizerStream.Decode(GetLastToken(generator.GetSequence(0)));

                // workaround until C# 13 is adopted and ref locals are usable in async methods
                static int GetLastToken(ReadOnlySpan<int> span) => span[span.Length - 1];
                strb.Append(lastToken);
                Console.Write(lastToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[Error during generation]: {ex.Message}");
                break;
            }
        }
        ParseFunctionCall(strb.ToString());
        var match = Regex.Match(strb.ToString(), @"<\|tool_call\|>(?<json>.*?)<\|/tool_call\|>");

        if (match.Success)
        {
            string jsonContent = match.Groups["json"].Value;
            Console.WriteLine("提取出的 JSON: " + jsonContent);

            //var toolCall = System.Text.Json.JsonSerializer.Deserialize<List<ToolCall>>(jsonContent);


        }

        var prompt2 = $"{strb}<|end|><|tool_result|>[{DateTime.Now.ToShortTimeString()}, {DateTime.Now.ToShortDateString()}]<|end|><|assistant|>";
        prompt2 = $"{strb}<|end|><|tool_result|>[{DateTime.Now.ToShortDateString()}]<|end|><|assistant|>";

        var sequences2 = tokenizer.Encode(prompt2);
        generator.AppendTokenSequences(sequences2);
        while (!generator.IsDone())
        {
            try
            {
                generator.GenerateNextToken();
                var lastToken = generator.GetSequence(0)[^1];
                strb.Append(tokenizerStream.Decode(lastToken));
                Console.Write(tokenizerStream.Decode(lastToken));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[Error during generation]: {ex.Message}");
                break;
            }
        }

        watch.Stop();
        Console.WriteLine();
        System.Diagnostics.Trace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        // 顯示效能數據
        var runTimeInSeconds = watch.Elapsed.TotalSeconds;
        var totalTokens = generator.GetSequence(0).Length;
        Console.WriteLine($"\n[Stats] Tokens: {totalTokens} | Time: {runTimeInSeconds:0.00}s | Speed: {totalTokens / runTimeInSeconds:0.00} tps");

    } while (true);

}

void ParseFunctionCall(string jsonResponse)
{
    try
    {
        // 1. 反序列化成 List
        var calls = JsonSerializer.Deserialize<List<FunctionCall>>(jsonResponse);

        if (calls == null) return;

        foreach (var call in calls)
        {
            Console.WriteLine($"執行函數: {call.Name}");

            // 2. 根據函數名稱進行分流 (Dispatch)
            if (call.Name == "getcomputerdate")
            {
                //ExecuteGetComputerDate();
            }
        }
    }
    catch (JsonException ex)
    {
        Console.WriteLine($"JSON 格式錯誤: {ex.Message}");
    }
}



//public record FunctionCall(
//    [property: JsonPropertyName("name")] string Name,
//    [property: JsonPropertyName("description")] string Description
//    //[property: JsonPropertyName("parameters")] JsonElement Parameters
//);

public class FunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("description")]
    public string Description { get; set; }
    //[JsonPropertyName("parameters")]
    //public JsonElement Parameters { get; set; }
}