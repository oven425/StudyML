using LLama;
using LLama.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SemanticKernel_Llama
{
    public class text1
    {

        async public Task Test()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            string modelPath = @"..\..\..\..\gguf_gemma4\gemma-4-E2B-it-Q4_K_M.gguf"; // change it to your own model path.
            var parameters = new ModelParams(modelPath)
            {
                ContextSize = 8192,
                GpuLayerCount = 20,
            };

            using var model = LLamaWeights.LoadFromFile(parameters);


            using var context = model.CreateContext(parameters);

            var executor = new InteractiveExecutor(context);


            var session = new ChatSession(executor);




            //<|think|>
            // 3. 根據 Gemma 4 規範建構 System Prompt (宣告工具與啟用思考)
            var systemPrompt = """
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
            for (int i = 0; i < toolCallMatchs.Count; i++)
            {
                string toolCallJson = toolCallMatchs[i].Value.Trim();
                var (action, argsContent) = ProcessCommand(toolCallJson);
                switch (action)
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
            fullPrompt = fullPrompt + modelOutput + "<turn|>";
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

        string ReadLocalFile(string filePath)
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
}
