//using feiyun0112.SemanticKernel.Connectors.OnnxRuntimeGenAI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

//using Microsoft.SemanticKernel.Connectors.Onnx;
using Microsoft.SemanticKernel.Connectors.OpenAI;
//using QSoft.SemanticKernel.Connectors.Onnx;
//using Phi3OnnxConsole.Utils;
using Spectre.Console;
using System.ComponentModel;




string modelPath = @"..\..\..\..\onnx_phi4\CPU";
modelPath = "..\\..\\..\\..\\onnx_phi4\\CPU-Phi-4-mini-instruct-onnx";

var fullpath = Path.GetFullPath(modelPath);
var builder = Kernel.CreateBuilder();
QSoft.SemanticKernel.OnnxKernelBuilderExtensions.AddOnnxRuntimeGenAIChatCompletion(builder, "phi4", fullpath);
//builder.AddOnnxRuntimeGenAIChatCompletion("phi4", fullpath);
builder.Plugins.AddFromType<TimePlugin>("TimeModule");



Kernel kernel = builder.Build();
//kernel.Plugins.AddFromType<TimePlugin>("TimeModule");
//builder.Plugins.AddFromType<FilePlugin>("FileModule");
System.Diagnostics.Trace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));

foreach (var plugin in kernel.Plugins)
{
    foreach(var oo in plugin.Select(x => x.AsKernelFunction()))
    {
        System.Diagnostics.Trace.WriteLine(oo.JsonSchema);
    }
}

var chatService = kernel.GetRequiredService<IChatCompletionService>();

var aaa = chatService as QSoft.SemanticKernel.Connectors.Onnx.OnnxRuntimeGenAIChatCompletionService;

var history = new ChatHistory();
history.AddSystemMessage("你是一個專業的助理。請盡量使用你擁有的工具來回答用戶的實時問題。");
history.AddUserMessage("獲取當前的本地日期與時間");

var executionSettings = new Microsoft.SemanticKernel.Connectors.Onnx.OnnxRuntimeGenAIPromptExecutionSettings
{
};
executionSettings.MaxTokens = 2048;


var response = await aaa.GetChatMessageContentAsync(history, executionSettings);
Console.WriteLine($"AI: {response.Content}");

public class TimePlugin
{
    [KernelFunction]
    [Description("獲取當前的本地日期與時間")]
    public string GetCurrentTime()
    {
        // 這裡直接讀取你電腦的系統時間
        return DateTime.Now.ToString("F");
    }
}


