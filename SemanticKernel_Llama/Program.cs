//using LLama;
//using LLama.Common;
//using LLama.Sampling;
//using System.Text;


//Console.OutputEncoding = Encoding.UTF8;
//Console.InputEncoding = Encoding.UTF8;
//string modelPath = @"..\..\..\..\gguf_gemma4\gemma-4-E2B-it-Q4_K_M.gguf"; // change it to your own model path.
//var fullPath = Path.GetFullPath(modelPath);
//var parameters = new ModelParams(fullPath)
//{
//    ContextSize = 1024, // The longest length of chat as memory.
//    GpuLayerCount = 5 // How many layers to offload to GPU. Please adjust it according to your GPU memory.
//};
//using var model = await LLamaWeights.LoadFromFileAsync(parameters);
//using var context = model.CreateContext(parameters);
//var executor = new InteractiveExecutor(context);

//// Add chat histories as prompt to tell AI how to act.
//var chatHistory = new ChatHistory();
//chatHistory.AddMessage(AuthorRole.System, "你是一個具備深度推理能力的助手。\r\n在回答使用者的問題之前，你必須先將你的思考過程詳細寫出來，並將思考過程包在 <think> 和 </think> 標籤之間。\r\n思考結束後，再輸出最終的答案。 \r\nTo use a tool, respond with a JSON object matching this schema:\r\n{\r\n  \"name\": \"function_name\",\r\n  \"arguments\": { ... }\r\n}\r\n\r\nAvailable tools:\r\n[{\"name\": \"get_weather\", \"description\": \"Get current weather\", ...}]");
//chatHistory.AddMessage(AuthorRole.User, "Hello, Bob.");
//chatHistory.AddMessage(AuthorRole.Assistant, "Hello. How may I help you today?");

//ChatSession session = new(executor, chatHistory);

//InferenceParams inferenceParams = new InferenceParams()
//{
//    MaxTokens = 256, // No more than 256 tokens should appear in answer. Remove it if antiprompt is enough for control.
//    AntiPrompts = new List<string> { "User:" }, // Stop generation once antiprompts appear.

//    SamplingPipeline = new DefaultSamplingPipeline(),

//};

//Console.ForegroundColor = ConsoleColor.Yellow;
//Console.Write("The chat session has started.\nUser: ");
//Console.ForegroundColor = ConsoleColor.Green;
//string userInput = Console.ReadLine() ?? "";

//while (userInput != "exit")
//{
//    await foreach (var text in session.ChatAsync(new ChatHistory.Message(AuthorRole.User, userInput), inferenceParams))
//    {
//        Console.ForegroundColor = ConsoleColor.White;
//        Console.Write(text);
//    }
//    Console.ForegroundColor = ConsoleColor.Green;
//    userInput = Console.ReadLine() ?? "";
//}

//https://ai.google.dev/gemma/docs/core/prompt-formatting-gemma4?hl=zh-tw

using LLama;
using LLama.Common;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

class Program
{
    // 1. 定義實質功能的 C# 函數：讀取本地文件
    static string ReadLocalFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                string content = File.ReadAllText(filePath);
                return content.Length > 2000 ? content.Substring(0, 2000) : content; // 限制字數
            }
            return $"Error: File '{filePath}' not found.";
        }
        catch (Exception ex)
        {
            return $"Error reading file: {ex.Message}";
        }
    }

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        string modelPath = @"..\..\..\..\gguf_gemma4\gemma-4-E2B-it-Q4_K_M.gguf"; // change it to your own model path.
        var parameters = new ModelParams(modelPath)
        {
            ContextSize = 4096,
            GpuLayerCount = 20,
        };

        using var model = LLamaWeights.LoadFromFile(parameters);
        using var context = model.CreateContext(parameters);

        // 使用 InteractiveExecutor 來維護對話的 KV Cache 狀態
        var executor = new InteractiveExecutor(context);
        var session = new ChatSession(executor);
        //<|think|>
        // 3. 根據 Gemma 4 規範建構 System Prompt (宣告工具與啟用思考)
        string systemPrompt = """
<|turn>system
You are a helpful assistant.<|tool>declaration:read_file{
  description: <|"|>讀取本機電腦中指定路徑的文字或數據檔案內容。<|"|>,
  parameters: {
    type: <|"|>object<|"|>,
    properties: {
      file_path: {
        type: <|"|>string<|"|>,
        description: <|"|>檔案的絕對路徑或相對路徑，例如 C:\\Users\\user\\Desktop\\config.json 或 ./data.csv<|"|>
      }
    },
    required: [<|"|>file_path<|"|>]
  }
}<tool|><|tool>declaration:list_directory{
  description: <|"|>列出指定資料夾中的所有檔案與目錄。<|"|>,
  parameters: {
    type: <<|"|>object<|"|>,
    properties: {
      dir_path: {
        type: <|"|>string<|"|>,
        description: <|"|>資料夾的絕對路徑<|"|>
      }
    },
    required: [<|"|>dir_path<|"|>]
  }
}<tool|><turn|>
""";

        // 4. 使用者提問
        string userQuestion = "幫我看看本地的./a.txt和./b.txt裡面寫了什麼？";
        //userQuestion = "幫我看看本地的a.txt和b.txt有什麼不同？";
        //userQuestion = "什麼是c#?";

        // 依照 Gemma 4 格式組裝輸入
        string fullPrompt = $"{systemPrompt}<|turn>user\n{userQuestion}<turn|>\n<|turn>model\n";

        Console.WriteLine("🤖 Gemma 4 正在處理中...\n");

        var inferenceParams = new InferenceParams()
        {
            MaxTokens = 512,
            AntiPrompts = new[] { "<turn|>" }
        };

        string modelOutput = "";
        bool inThought = false;

        // 串流接收 Token
        await foreach (var token in session.ChatAsync(new ChatHistory.Message(AuthorRole.User, fullPrompt), inferenceParams))
        {
            modelOutput += token;
            Console.Write(token);
            System.Diagnostics.Trace.Write(token);
        }
        string thoughtPattern = @"<\|channel>(.*?)(?=<channel\|>)";
        Match thoughtMatch = Regex.Match(modelOutput, thoughtPattern, RegexOptions.Singleline);

        string toolCallPattern = @"<\|tool_call>(.*?)<tool_call\|>";
        MatchCollection toolCallMatchs = Regex.Matches(modelOutput, toolCallPattern, RegexOptions.Singleline);
        var toolresps = new List<string>();
        for (int i=0; i < toolCallMatchs.Count; i++)
        {
            string toolCallJson = toolCallMatchs[i].Value.Trim();
            var (action, argsContent) = ProcessCommand(toolCallJson);
            switch(action)
            {
                case "read_file":
                    var readFileArgs = JsonSerializer.Deserialize<ReadFileArgs>(argsContent);
                    if (readFileArgs != null)
                    {
                        string fileContent = ReadLocalFile(readFileArgs.file_path);
                        var toolresp = $"<|tool_response>response:read_file{{content:<|\"|>{fileContent}<|\"|>}}<tool_response|>";
                        toolresps.Add(toolresp);
                    }
                    break;
            }

        }
        fullPrompt = fullPrompt + modelOutput;
        //fullPrompt = fullPrompt + "<|tool_response>response:read_file{content:<|\"|>abcdef<|\"|>}<tool_response|><|tool_response>response:read_file{content:<|\"|>apple is red<|\"|>}<tool_response|><|turn>model\n";
        fullPrompt = fullPrompt + string.Join("", toolresps);
        modelOutput = "";
        await foreach (var token in session.ChatAsync(new ChatHistory.Message(AuthorRole.User, fullPrompt), inferenceParams))
        {
            modelOutput += token;
            Console.Write(token);
            System.Diagnostics.Trace.Write(token);
        }
        fullPrompt = fullPrompt + modelOutput+ "<turn|>";
        userQuestion = "比對a.tx和b.txt的內容有什麼不同";
        fullPrompt = fullPrompt + $"<|turn>user\n{userQuestion}<turn|>\n<|turn>model\n";
        await foreach (var token in session.ChatAsync(new ChatHistory.Message(AuthorRole.User, fullPrompt), inferenceParams))
        {
            modelOutput += token;
            Console.Write(token);
            System.Diagnostics.Trace.Write(token);
        }
        Console.WriteLine("\n\n[對話結束]");
        Console.ReadLine();
    }

    static (string action, string argsContent) ProcessCommand(string input)
    {
        string basePattern = @"call:(?<action>\w+)\{(?<argsContent>.*?)\}";
        Match match = Regex.Match(input, basePattern);

        if (!match.Success)
        {
            return (string.Empty, string.Empty);
        }

        string action = match.Groups["action"].Value;
        string argsContent = match.Groups["argsContent"].Value;
        string cleanPattern = @"(?<key>\w+)\s*:\s*<\|""\|>(?<val>.*?)<\|""\|>";
        string standardizedArgs = Regex.Replace(argsContent, cleanPattern, @"""${key}"":""${val}""");


        string finalJson = $"{{{standardizedArgs}}}";
        return (action, finalJson);
    }
}

public class ReadFileArgs
{
    public string file_path { get; set; } = string.Empty;
}

