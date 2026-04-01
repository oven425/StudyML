// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.AI;
using Microsoft.ML.OnnxRuntimeGenAI;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

string modelPath = @"..\..\..\..\onnx_phi4\CPU";
modelPath = "..\\..\\..\\..\\onnx_phi4\\CPU-Phi-4-mini-instruct-onnx";
var fullpath = Path.GetFullPath(modelPath);
System.Diagnostics.Trace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
using Config config = new Config(modelPath);
config.ClearProviders();
////config.AppendProvider("DML");

OnnxRuntimeGenAIChatClientOptions options = new OnnxRuntimeGenAIChatClientOptions();

var cc = new OnnxRuntimeGenAIChatClient1(fullpath);
ChatMessage systemMessage = new(ChatRole.System, "You are a helpful assistant with some tools.");
ChatMessage toolMessage = new(ChatRole.Tool, @"[{""name"": ""getcomputerdatetime"", ""description"": ""Gets the current date and time of this computer."", ""parameters"": {}}]");
ChatMessage userMessage = new(ChatRole.User, "What time is this computer?");
ChatMessage assistantMessage = new(ChatRole.Assistant, "");

ChatOptions chatOptions = new ChatOptions();
chatOptions.Temperature = 0.0f;
chatOptions.TopP = 1.0f;
chatOptions.MaxOutputTokens = 300;

await foreach (var oo in cc.GetStreamingResponseAsync([systemMessage, toolMessage, userMessage, assistantMessage], chatOptions))
{
    Console.Write(oo);
}

ChatMessage toolMessageresult = new(ChatRole.Tool, $"{DateTime.Now}");
await foreach (var oo in cc.GetStreamingResponseAsync([systemMessage, toolMessage, userMessage, assistantMessage, toolMessageresult, assistantMessage], chatOptions))
{
    Console.Write(oo);
}


fullpath = Path.GetFullPath(modelPath);
using Model model = new(fullpath);
using Tokenizer tokenizer = new(model);

do
{
    Console.Write("\nPrompt: ");
    var prompt = Console.ReadLine();
    prompt = "What is OOP?";
    if (string.IsNullOrEmpty(prompt))
    {
        continue;
    }


    //var sequences = tokenizer.Encode($"<|user|>\n{prompt}<|end|>\n<|assistant|>\n");
    //var sequences = tokenizer.Encode(@"<|system|>You are a helpful assistant with some tools.<|tool|>[{""name"": ""get_weather_updates"", ""description"": ""Fetches weather updates for a given city using the RapidAPI Weather API."", ""parameters"": {""city"": {""description"": ""The name of the city for which to retrieve weather information."", ""type"": ""str"", ""default"": ""London""}}}]<|/tool|><|end|><|user|>What is the weather like in Taipe today?<|end|><|assistant|>");

    var prompt1 = @"<|system|>You are a helpful assistant with some tools.<|tool|>[{""name"": ""getcomputerdatetime"", ""description"": ""Gets the current date and time of this computer."", ""parameters"": {}}]<|/tool|><|end|><|user|>What time is this computer?<|end|><|assistant|>";

    tokenizer.ApplyChatTemplate(
            template_str: null,
            messages: prompt1,
            tools: null,
            add_generation_prompt: true);

    var sequences = tokenizer.Encode(prompt1);
    var strb = new StringBuilder();
    using GeneratorParams generatorParams = new GeneratorParams(model);
    generatorParams.SetSearchOption("min_length", 1);
    generatorParams.SetSearchOption("max_length", 300);
    generatorParams.SetSearchOption("temperature", 0.0f);
    generatorParams.SetSearchOption("top_p", 1.0f);
    using var tokenizerStream = tokenizer.CreateStream();
    using var generator = new Generator(model, generatorParams);
    generator.AppendTokenSequences(sequences);
    
    var watch = System.Diagnostics.Stopwatch.StartNew();

    Console.WriteLine("\nOutput:");

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


            Console.Write(lastToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[Error during generation]: {ex.Message}");
            break;
        }
    }

    var match = Regex.Match(strb.ToString(), @"<\|tool_call\|>(?<json>.*?)<\|/tool_call\|>");

    if (match.Success)
    {
        string jsonContent = match.Groups["json"].Value;
        Console.WriteLine("提取出的 JSON: " + jsonContent);

        var toolCall = System.Text.Json.JsonSerializer.Deserialize<List<ToolCall>>(jsonContent);


    }

    var prompt2 = $"{strb}<|end|><|tool_result|>{DateTime.Now}<|end|><|assistant|>";
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


public class ToolCall
{
    public string name { get; set; }
    public object arguments { get; set; } // 若 arguments 結構固定，可換成具體類別
}


