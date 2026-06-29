using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLama;
using LLama.Abstractions;
using LLama.Common;
using LLama.Sampling;
using Microsoft.UI.Xaml.Documents;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace App_gguf
{
    public partial class MainUI : ObservableObject
    {
        public ObservableCollection<History> Historys { get; set; } = [];
        [ObservableProperty]
        public partial string UserQuestion { set; get; } = "目前使用者桌面有什麼檔案?";
        string m_ModelPath = @"..\..\..\..\..\gguf_gemma4\gemma-4-E2B-it-Q4_K_M.gguf";
        ModelParams? m_Parameters;
        LLamaWeights? m_Weights;
        LLamaContext? m_Context;
        InteractiveExecutor? m_Executor;
        string m_SystemPrompt = """
        <|turn>system
        你是個Windows助理,所有回答要有禮貌以及使用繁體中文
        <|tool>declaration:read_file{
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
        }<tool|>
        <|tool>declaration:list_directory{
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
        }<tool|>
        <|tool>declaration:get_FolderPath{
          description:<|"|>Gets the absolute path to the specified Windows system special folder.<|"|>,
          parameters:{
            properties:{
              folder:{
                description:<|"|>The name of the .NET Environment.SpecialFolder enum (e.g., Desktop, MyDocuments).<|"|>,
                type:<|\"|>STRING<|"|>
                }
              },
              required:[<|"|>folder<|"|>]
            }
        }<tool|>
        <|tool>declaration:get_usename{
          description: <|"|>取得windows現在的使用者名稱。<|"|>,
          parameters: {          
          }
        }<tool|>
        <|tool>declaration:get_datetime{
          description: <|"|>取得現在的時間<|"|>,
          parameters: {          
          }
        }<tool|>
        
        <turn|>
        """;
        InferenceParams? m_InferenceParams;
        bool m_IsLoading;
        public async Task New()
        {
            if (!this.IsLoaded && !m_IsLoading)
            {
                this.m_IsLoading = true;
                this.m_Parameters = new ModelParams(this.m_ModelPath)
                {
                    ContextSize = 81920,
                    GpuLayerCount = 0,
                };
                this.m_Weights = await LLamaWeights.LoadFromFileAsync(this.m_Parameters);
                this.m_Context = this.m_Weights.CreateContext(this.m_Parameters);
                this.m_Executor = new InteractiveExecutor(this.m_Context);
                this.m_InferenceParams = new InferenceParams()
                {
                    //SamplingPipeline = new DefaultSamplingPipeline()
                    //{
                    //    TopK = 40,
                    //    TopP = 0.9F,
                    //    Temperature = 0.1F
                    //},
                    MaxTokens = 81920,
                    AntiPrompts = ["<turn|>"]
                };
                //History hh = new();
                //this.Historys.Add(hh);
                //await foreach (var token in this.m_Executor.InferAsync($"{this.m_SystemPrompt}\n", this.m_InferenceParams))
                //{
                //    System.Diagnostics.Trace.Write(token);
                //    hh.Message += token;
                //}

                this.m_IsLoading = false;
                this.IsLoaded = true;
            }


        }
        [ObservableProperty]
        public partial bool IsLoaded { get; set; } = false;
        bool m_IsFirst = true;

        string modelOutput = "";
        [RelayCommand]
        async Task Send()
        {
            if (m_Executor is null) return;
            
            this.Historys.Add(new History()
            {
                Message = this.UserQuestion
            });
            

            History hh = new();
            this.Historys.Add(hh);
            string user = $"""
                <|turn>user
                {UserQuestion}<turn|>
                <|turn>model
                """;
            if(this.m_IsFirst)
            {
                user = $"{m_SystemPrompt}\n{user}";
                this.m_IsFirst = false;
            }
            await foreach (var token in this.m_Executor.InferAsync(user, this.m_InferenceParams))
            {
                System.Diagnostics.Trace.Write(token);
                hh.Message += token;
            }

            string thoughtPattern = @"<\|channel>(.*?)(?=<channel\|>)";
            Match thoughtMatch = Regex.Match(hh.Message, thoughtPattern, RegexOptions.Singleline);

            while(true)
            {
                var toolresp = ToolCall(hh.Message);
                if(string.IsNullOrEmpty(toolresp))
                {
                    break;
                }
                hh = new History();
                this.Historys.Add(hh);
                await foreach (var token in this.m_Executor.InferAsync(toolresp, this.m_InferenceParams))
                {
                    System.Diagnostics.Trace.Write(token);
                    hh.Message += token;
                }
            }

            //string toolCallPattern = @"<\|tool_call>(.*?)<tool_call\|>";
            //MatchCollection toolCallMatchs = Regex.Matches(hh.Message, toolCallPattern, RegexOptions.Singleline);
            //if(toolCallMatchs.Count > 0)
            //{
            //    string toolresp = "";
            //    for (int i = 0; i < toolCallMatchs.Count; i++)
            //    {
            //        string toolCallJson = toolCallMatchs[i].Value.Trim();
            //        var (action, argsContent) = NormailiszeCToolCall(toolCallJson);
            //        switch (action)
            //        {
            //            case "read_file":
            //                var readFileArgs = JsonSerializer.Deserialize<ReadFileArgs>(argsContent);
            //                if (readFileArgs != null)
            //                {
            //                    string fileContent = ReadLocalFile(readFileArgs.file_path);
            //                    toolresp = $"<|tool_response>response:read_file{fileContent}<tool_response|>";
            //                }
            //                break;
            //            case "get_datetime":
            //                var resp = JsonSerializer.Serialize(new ToolRepsonse()
            //                {
            //                    Data = DateTime.Now.ToString()
            //                });
            //                toolresp = $"<|tool_response>response:get_datetime{resp}<tool_response|>";
            //                break;
            //            case "list_directory":
            //                var listdirargs = JsonSerializer.Deserialize<ListDirArgs>(argsContent);
            //                resp = list_directory(listdirargs.dir_path);
            //                toolresp = $"<|tool_response>response:list_directory{resp}<tool_response|>";
            //                break;
            //            case "get_usename":
            //                resp = JsonSerializer.Serialize(new ToolRepsonse()
            //                {
            //                    Data = Environment.UserName
            //                });
            //                toolresp = $"<|tool_response>response:get_usename{resp}<tool_response|>";
            //                break;
            //            case "get_FolderPath":
            //                var getfolderargs = JsonSerializer.Deserialize<GetFolderArgs>(argsContent);
            //                var sss = getfolderargs.folder switch
            //                {
            //                    "Desktop" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            //                    _=> "",
            //                };
            //                resp = JsonSerializer.Serialize(new ToolRepsonse()
            //                {
            //                    Data = sss
            //                });
            //                toolresp = $"<|tool_response>response:get_FolderPath{resp}<tool_response|>";


            //                break;
            //        }
            //        await foreach (var token in this.m_Executor.InferAsync(toolresp, this.m_InferenceParams))
            //        {
            //            System.Diagnostics.Trace.Write(token);
            //            hh.Message += token;
            //        }
            //    }
            //}

        }

        string ToolCall(string message)
        {
            string toolresp = string.Empty;
            string toolCallPattern = @"<\|tool_call>(.*?)<tool_call\|>";
            MatchCollection toolCallMatchs = Regex.Matches(message, toolCallPattern, RegexOptions.Singleline);
            if (toolCallMatchs.Count > 0)
            {
                for (int i = 0; i < toolCallMatchs.Count; i++)
                {
                    string toolCallJson = toolCallMatchs[i].Value.Trim();
                    var (action, argsContent) = NormailiszeCToolCall(toolCallJson);
                    switch (action)
                    {
                        case "read_file":
                            var readFileArgs = JsonSerializer.Deserialize<ReadFileArgs>(argsContent);
                            if (readFileArgs != null)
                            {
                                string fileContent = ReadLocalFile(readFileArgs.file_path);
                                toolresp = $"<|tool_response>response:read_file{fileContent}<tool_response|>";
                            }
                            break;
                        case "get_datetime":
                            var resp = JsonSerializer.Serialize(new ToolRepsonse()
                            {
                                Data = DateTime.Now.ToString()
                            });
                            toolresp = $"<|tool_response>response:get_datetime{resp}<tool_response|>";
                            break;
                        case "list_directory":
                            var listdirargs = JsonSerializer.Deserialize<ListDirArgs>(argsContent);
                            resp = list_directory(listdirargs.dir_path);
                            toolresp = $"<|tool_response>response:list_directory{resp}<tool_response|>";
                            break;
                        case "get_usename":
                            resp = JsonSerializer.Serialize(new ToolRepsonse()
                            {
                                Data = Environment.UserName
                            });
                            toolresp = $"<|tool_response>response:get_usename{resp}<tool_response|>";
                            break;
                        case "get_FolderPath":
                            var getfolderargs = JsonSerializer.Deserialize<GetFolderArgs>(argsContent);
                            var sss = getfolderargs.folder switch
                            {
                                "Desktop" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                                _ => "",
                            };
                            resp = JsonSerializer.Serialize(new ToolRepsonse()
                            {
                                Data = sss
                            });
                            toolresp = $"<|tool_response>response:get_FolderPath{resp}<tool_response|>";
                            break;
                    }
                }
            }
            return toolresp;
        }



        string list_directory(string dir_path)
        {
            ToolRepsonse resp = new ToolRepsonse();
            try
            {
                var dir = new DirectoryInfo(dir_path);
                var dirs = dir.GetDirectories().Select(x => new { x.Name, x.CreationTime });
                var files = dir.GetFiles().Select(x => new { x.Name, x.CreationTime });
                var alls = dirs.Concat(files).OrderBy(x => x.CreationTime)
                    .Select(x=>new {x.Name, CreateTime=x.CreationTime.ToString("yyyy/MM/dd HH:mm:ss") });
                var str_alls = JsonSerializer.Serialize(alls, new JsonSerializerOptions()
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                });
                resp.Data = str_alls;
            }
            catch(Exception ee)
            {
                resp.IsFail = true;
                resp.FailMessage = ee.Message;
            }
            

            return JsonSerializer.Serialize(resp, new JsonSerializerOptions()
                {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                });
        }

        string ReadLocalFile(string filename)
        {
            var resp = new ToolRepsonse();
            try
            {
                resp.Data =File.ReadAllText(filename);
            }
            catch (Exception e)
            {
                resp.IsFail = true;
                resp.FailMessage = e.Message;
            }
            return JsonSerializer.Serialize(resp);
        }



        static (string action, string argsContent) NormailiszeCToolCall(string input)
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
            standardizedArgs = Regex.Replace(standardizedArgs, @"\\(?![""\\/bfnrt]|u[0-9a-fA-F]{4})", @"\\");

            string finalJson = $"{{{standardizedArgs}}}";
            return (action, finalJson);
        }

    }

    public class ToolRepsonse
    {
        public bool IsFail {  get; set; }
        public string Data { set; get; } = string.Empty;
        public string FailMessage { get; set; } = string.Empty;
    }
    public class GetFolderArgs
    {
        public string folder { set; get; } = string.Empty;
    }
    public class ListDirArgs
    {
        public string dir_path { set; get; } = string.Empty;
    }

    public class ReadFileArgs
    {
        public string file_path { get; set; } = string.Empty;
    }

    public partial class History : ObservableObject
    {
        public enum Role
        {
            AI,
            User
        }
        [ObservableProperty]
        public partial string Message { set; get; } = "";
    }
}
