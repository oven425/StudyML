using Microsoft.ML.OnnxRuntimeGenAI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App_onnx
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainUI MainUI { set; get; } = new();
        public MainWindow()
        {
            InitializeComponent();
            
            //string modelPath = @"..\..\..\..\onnx_phi4\CPU";
            //modelPath = "..\\..\\..\\..\\..\\..\\onnx_phi4\\CPU-Phi-4-mini-instruct-onnx";
            //var fullpath = Path.GetFullPath(modelPath);
            //using Model model = new(fullpath);
            //using Tokenizer tokenizer = new(model);
            //var tools = @"[{""name"": ""getcomputerdatetime"", ""description"": ""Gets the current date and time of this computer."", ""parameters"": {}}]";
            //var prompt = @"<|system|>You are a helpful assistant with some tools.<|tool|>[{""name"": ""getcomputerdatetime"", ""description"": ""Gets the current date and time of this computer."", ""parameters"": {}}]<|/tool|><|end|><|user|>What time is this computer?<|end|><|assistant|>";
            //tools = @"[{""name"": ""getcomputertime"", ""description"": ""Gets the current time of this computer."", ""parameters"": {}},{""name"": ""getcomputerdate"", ""description"": ""Gets the current date of this computer."", ""parameters"": {}}]";

            //prompt = $@"<|system|>You are a helpful assistant with some tools.<|tool|>{tools}<|/tool|><|end|><|user|>What date and time is this computer?<|end|><|assistant|>";

            //var sequences = tokenizer.Encode(prompt);
            //var strb = new StringBuilder();
            //using GeneratorParams generatorParams = new GeneratorParams(model);
            //generatorParams.SetSearchOption("min_length", 1);
            //generatorParams.SetSearchOption("max_length", 300);
            //generatorParams.SetSearchOption("temperature", 0.6f);
            //generatorParams.SetSearchOption("top_p", 1.0f);
            ////generatorParams.SetGuidance("json_schema", "{}");

            //using var generator = new Generator(model, generatorParams);
            //generator.AppendTokenSequences(sequences);

            //var watch = System.Diagnostics.Stopwatch.StartNew();

            //using var tokenizerStream = tokenizer.CreateStream();
            //while (!generator.IsDone())
            //{
            //    try
            //    {
            //        //generator.GenerateNextToken();
            //        //var lastToken = generator.GetSequence(0)[^1];
            //        //strb.Append(tokenizerStream.Decode(lastToken));

            //        generator.GenerateNextToken();
            //        string lastToken = tokenizerStream.Decode(GetLastToken(generator.GetSequence(0)));

            //        // workaround until C# 13 is adopted and ref locals are usable in async methods
            //        static int GetLastToken(ReadOnlySpan<int> span) => span[span.Length - 1];
            //        strb.Append(lastToken);
            //        Console.Write(lastToken);
            //    }
            //    catch (Exception ex)
            //    {
            //        Console.WriteLine($"\n[Error during generation]: {ex.Message}");
            //        break;
            //    }
            //}
        }

        string modelPath = "..\\..\\..\\..\\..\\..\\onnx_phi4\\CPU-Phi-4-mini-instruct-onnx";
        Model? m_Model;
        Tokenizer? m_Tokenizer;
        GeneratorParams? m_GeneratorParams;
        void CreateModel()
        {
            var fullpath = Path.GetFullPath(modelPath);
            m_Model = new(fullpath);
        }
        async private void button_send_Click(object sender, RoutedEventArgs e)
        {
            if(sender is Control fe)
            {
                fe.IsEnabled = false;
            }
            var user = new ChatHistory() { Role = ChatHistory.Roles.user, Text = this.MainUI.UserInputText }; 
            this.MainUI.Historys.Add(user);
            var tools = "";
            var prompt = $"<|im_start|>system<|im_sep|>你是個對話機器人,都要要中文回答.<|im_end|>" +
                $"<|im_start|>user<|im_sep|>{this.MainUI.UserInputText}<|im_end|>" +
                $"<|im_start|>assistant<|im_sep|>";
            prompt = "<|im_start|>system\r\nYou are a Windows desktop assistant. \r\n Available tools:\r\n[\r\n  {\"name\":\"getcomputerdate\",\"parameters\":{}},\r\n  {\"name\":\"getcomputertime\",\"parameters\":{}},\r\n  {\"name\":\"open_application\",\"parameters\":{\"app\":\"string\"}},\r\n  {\"name\":\"search_files\",\"parameters\":{\"query\":\"string\"}},\r\n  {\"name\":\"control_volume\",\"parameters\":{\"level\":\"int\"}}\r\n]\r\n<|im_end|>\r\n\r\n<|im_start|>user\r\n請幫我打開記事本\r\n<|im_end|>\r\n\r\n<|im_start|>assistant\r\n";
            if(Directory.Exists("skills"))
            {
                var ffs = Directory.GetFiles("skills", "*.md");
                foreach(var ff in ffs)
                {
                    var content = await File.ReadAllTextAsync(ff);
                    tools = tools + content + "\r\n";
                }
            }

            prompt = $"<|im_start|>You are a tool generator.\r\nConvert the following Skill (Markdown) into a JSON tool definition.\r\n- Output ONLY valid JSON.\r\n- Do not invent new commands or environments (stick to Windows CLI).\r\n- Use the Skill name as the tool name.\r\n- Include \"description\" and \"parameters\".\r\n- Parameters must be derived from the Skill content.\r\n- Example:\r\nSkill: CopyCommand\r\n→\r\n{{\r\n  \"name\": \"copy_command\",\r\n  \"description\": \"Copy files using Windows CLI command 'copy'.\",\r\n  \"parameters\": {{\r\n    \"source\": \"string (required, source file path)\",\r\n    \"destination\": \"string (required, destination file path)\"\r\n  }}\r\n}}<|im_end|>\r\n\r\n<|im_start|>user\r\n{tools}<|im_end|><|im_start|>assistant";
            prompt = "<|im_start|>system\r\nYou are a tool generator.\r\nConvert the following Skills (Markdown) into JSON tool definitions.\r\n- Output ONLY valid JSON.\r\n- Do not invent new commands or environments (stick to Windows CLI).\r\n- Use the Skill name as the tool name.\r\n- Include \"description\" and \"parameters\".\r\n- Parameters must be simple key:value pairs (string or boolean), not full command strings.\r\n- Do not add descriptive text inside parameter values.\r\n- Do not add extra braces or text outside the JSON.\r\n- Example:\r\nSkill: CopyCommand\r\n→\r\n{\r\n  \"name\": \"copy_command\",\r\n  \"description\": \"Copy files using Windows CLI command 'copy'.\",\r\n  \"parameters\": {\r\n    \"source\": \"string (required, source file path)\",\r\n    \"destination\": \"string (required, destination file path)\"\r\n  }\r\n}\r\n<|im_end|>\r\n\r\n<|im_start|>user\r\n# Skill: DirCommand\r\n## 說明\r\n透過 Windows CLI 指令 `dir` 列舉指定資料夾的內容。\r\n## 指令用法\r\n- `dir <path>` → 顯示檔案與子資料夾。\r\n- `dir <path> /b` → 只顯示檔名。\r\n- `dir <path> /a:d` → 只顯示資料夾。\r\n- `dir <path> /a:-d` → 只顯示檔案。\r\n## 範例\r\n{\"name\":\"dir_command\",\"parameters\":{\"path\":\"D:\\\\Projects\",\"options\":\"/b\"}}\r\n\r\n# Skill: CopyCommand\r\n## 說明\r\n透過 Windows CLI 指令 `copy` 複製檔案。\r\n## 指令用法\r\n- `copy <source> <destination>` → 複製檔案。\r\n## 範例\r\n{\"name\":\"copy_command\",\"parameters\":{\"source\":\"D:\\\\file.txt\",\"destination\":\"E:\\\\backup\"}}\r\n\r\n# Skill: CdCommand\r\n## 說明\r\n透過 Windows CLI 指令 `cd` 切換目錄。\r\n## 指令用法\r\n- `cd <path>` → 切換到指定資料夾。\r\n## 範例\r\n{\"name\":\"cd_command\",\"parameters\":{\"path\":\"D:\\\\Projects\"}}\r\n<|im_end|>\r\n";
            prompt = "<|im_start|>system You are a helpful assistant.Here are the available tools:<|tool|>[{\"name\":\"getcomputertime\",\"description\":\"Gets the current time of this computer.\",\"parameters\":{}},{\"name\":\"getcomputerdate\",\"description\":\"Gets the current date of this computer.\",\"parameters\":{}}]<|/tool|><|im_end|><|im_start|>userWhat date and time is this computer?<|im_end|><|im_start|>assistant";

            ChatHistory system = new ChatHistory()
            {
                Role = ChatHistory.Roles.system,
                Text = "system You are a helpful assistant.Here are the available tools:<|tool|>[{\"name\":\"getcomputertime\",\"description\":\"Gets the current time of this computer.\",\"parameters\":{}},{\"name\":\"getcomputerdate\",\"description\":\"Gets the current date of this computer.\",\"parameters\":{}}]<|/tool|>"
            };
            
            ChatHistory assistant = new() { Role = ChatHistory.Roles.assistant, Text = "" };
            prompt = system.ToString()+ user.ToString() + assistant.ToString();
            this.MainUI.Historys.Add(assistant);

            await Task.Run(() =>
            {
                if (this.m_Model is null)
                {
                    m_Tokenizer?.Dispose();
                    m_GeneratorParams?.Dispose();
                    CreateModel();
                    m_Tokenizer = new(m_Model);
                    m_GeneratorParams = new GeneratorParams(m_Model);
                }


                var sequences = m_Tokenizer.Encode(prompt);

                //m_GeneratorParams.SetSearchOption("do_sample", true);

                m_GeneratorParams.SetSearchOption("temperature", 0);
                m_GeneratorParams.SetSearchOption("top_p", 1); 
                m_GeneratorParams.SetSearchOption("top_k", 1); 
                //m_GeneratorParams.SetSearchOption("repetition_penalty", 1.15);
                m_GeneratorParams.SetSearchOption("max_length", 2048); 

                using var generator = new Generator(m_Model, m_GeneratorParams);
                generator.AppendTokenSequences(sequences);


                using var tokenizerStream = m_Tokenizer.CreateStream();
                var respstr = "";
                while (!generator.IsDone())
                {
                    try
                    {
                        generator.GenerateNextToken();
                        string lastToken = tokenizerStream.Decode(GetLastToken(generator.GetSequence(0)));

                        static int GetLastToken(ReadOnlySpan<int> span) => span[span.Length - 1];

                        System.Diagnostics.Trace.WriteLine(lastToken);

                        respstr = respstr+ lastToken;
                        var endidx = respstr.IndexOf("<|im_end|>");
                        if(endidx >-1)
                        {
                            respstr = respstr[..endidx];
                        }
                        
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            assistant.Text = respstr;
                        });
                        if (endidx > -1)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\n[Error during generation]: {ex.Message}");
                        break;
                    }
                }
            });
            if (sender is Control fe1)
            {
                fe1.IsEnabled = true;
            }
        }
    }

    public class MainUI : INotifyPropertyChanged
    {
        public ObservableCollection<ChatHistory> Historys { set; get; } = [];
        public event PropertyChangedEventHandler? PropertyChanged;
        string m_UserInputText = "Hello";
        public string UserInputText
        {
            set
            {
                m_UserInputText = value;
                this.Update();
            }
            get => m_UserInputText;
        }
        void Update([CallerMemberName]string name = "")
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }


    public class ChatHistory : INotifyPropertyChanged
    {
        public override string ToString()
        {
            if(this.Text  =="")
            {
                return $"<|im_start|>{Role}";
            }
            return $"<|im_start|>{Role} {Text}<|im_end|>";
        }
        public enum Roles
        {
            system,
            user,
            assistant
        }
        public Roles Role { get; set; } = Roles.user;

        string m_Text = "";
        public string Text
        {
            get => m_Text;
            set
            {
                m_Text = value;
                Update();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        void Update([CallerMemberName] string name = "")
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

    }
}
