using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLama;
using LLama.Abstractions;
using LLama.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace App_gguf
{
    public partial class MainUI : ObservableObject
    {
        public ObservableCollection<History> Historys { get; set; } = new ObservableCollection<History>();
        [ObservableProperty]
        string _User = "";
        string m_ModelPath = @"..\..\..\..\gguf_gemma4\gemma-4-E2B-it-Q4_K_M.gguf";
        ModelParams? m_Parameters;
        LLamaWeights? m_Weights;
        LLamaContext? m_Context;
        InteractiveExecutor? m_Executor;
        string m_SystemPrompt = """
        <|turn>system
        你是個AI助理,所有回答都要用中文
        <turn|>
        """;
        InferenceParams? m_InferenceParams;
        public async Task New()
        {
            this.m_Parameters = new ModelParams(this.m_ModelPath)
            {
                ContextSize = 8192,
                GpuLayerCount = 20,
            };
            this.m_Weights = await LLamaWeights.LoadFromFileAsync(this.m_Parameters);
            this.m_Context = this.m_Weights.CreateContext(this.m_Parameters);
            this.m_Executor = new InteractiveExecutor(this.m_Context);
            this.m_InferenceParams = new InferenceParams()
            {
                MaxTokens = 81920,
                AntiPrompts = ["<turn|>"]
            };
            History hh = new History();
            this.Historys.Add(hh);
            await foreach (var token in this.m_Executor.InferAsync($"{this.m_SystemPrompt}\n", this.m_InferenceParams))
            {
                System.Diagnostics.Trace.Write(token);
                hh.Message = hh.Message + token;
            }
        }

        [RelayCommand]
        async Task Send()
        {
            string modelPath = @"..\..\..\..\gguf_gemma4\gemma-4-E2B-it-Q4_K_M.gguf"; // change it to your own model path.
            var parameters = new ModelParams(modelPath)
            {
                ContextSize = 8192,
                GpuLayerCount = 20,
            };

            using var model = await LLamaWeights.LoadFromFileAsync(parameters);


            using var context = model.CreateContext(parameters);

            var executor = new InteractiveExecutor(context);

            //<|think|>
            // 3. 根據 Gemma 4 規範建構 System Prompt (宣告工具與啟用思考)
            var systemPrompt = """
        <|turn>system
        你是個AI助理,所有回答都要用中文
        <turn|>
        """;

            Console.WriteLine("🤖 Gemma 4 正在處理中...\n");
            // 1. 建立採樣參數，並設定 Temperature

            var inferenceParams = new InferenceParams()
            {
                MaxTokens = 81920,
                AntiPrompts = ["<turn|>"]
            };

            await foreach (var token in executor.InferAsync($"{systemPrompt}\n", inferenceParams))
            {
                Console.Write(token);
                System.Diagnostics.Trace.Write(token);
            }
            while (true)
            {
                string userQuestion = Console.ReadLine();
                if (userQuestion == "exit")
                {
                    return;
                }



                await foreach (var token in executor.InferAsync($"<|turn>user\n{userQuestion}<turn|>\n<|turn>model\n", inferenceParams))
                {
                    Console.Write(token);
                    System.Diagnostics.Trace.Write(token);
                }
            }

        }
    }

    public partial class History : ObservableObject
    {
        public enum Role
        {
            AI,
            User
        }
        [ObservableProperty]
        string _Message = "";
    }
}
