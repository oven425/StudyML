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
        //public ObservableCollection<History> Historys { get; set; } = [];
        [ObservableProperty]
        public partial string UserQuestion { set; get; } = "";
        string m_ModelPath = @"..\..\..\..\..\gguf_gemma4\gemma-4-E2B-it-Q4_K_M.gguf";
        ModelParams? m_Parameters;
        LLamaWeights? m_Weights;
        LLamaContext? m_Context;
        InteractiveExecutor? m_Executor;
        string m_SystemPrompt = """
        <|turn>system
        你是個AI助理,所有回答要有禮貌以及使用繁體中文,
        <turn|>
        """;
        InferenceParams? m_InferenceParams;
        bool m_IsLoading;
        public async Task New()
        {
            //if (!this.IsLoadedModel && !m_IsLoading)
            //{
            //    this.m_IsLoading = true;
            //    this.m_Parameters = new ModelParams(this.m_ModelPath)
            //    {
            //        ContextSize = 8192,
            //        GpuLayerCount = 20,
            //    };
            //    //this.m_Weights = await LLamaWeights.LoadFromFileAsync(this.m_Parameters);
            //    //this.m_Context = this.m_Weights.CreateContext(this.m_Parameters);
            //    //this.m_Executor = new InteractiveExecutor(this.m_Context);
            //    //this.m_InferenceParams = new InferenceParams()
            //    //{
            //    //    MaxTokens = 81920,
            //    //    AntiPrompts = ["<turn|>"]
            //    //};
            //    ////History hh = new();
            //    ////this.Historys.Add(hh);
            //    //await foreach (var token in this.m_Executor.InferAsync($"{this.m_SystemPrompt}\n", this.m_InferenceParams))
            //    //{
            //    //    System.Diagnostics.Trace.Write(token);
            //    //    //hh.Message += token;
            //    //}

            await Task.Delay(1000);
            //this.m_IsLoading = false;
            this.IsLoaded = true;
            //}


        }
        [ObservableProperty]
        public partial bool IsLoaded { get; set; } = false;

        [RelayCommand]
        async Task Send()
        {
            //History hh = new();
            //this.Historys.Add(hh);
            //await foreach (var token in this.m_Executor.InferAsync($"<|turn>user\n{UserQuestion}<turn|>\n<|turn>model\n", this.m_InferenceParams))
            //{
            //    //Console.Write(token);
            //    System.Diagnostics.Trace.Write(token);
            //    hh.Message += token;
            //}

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
        public partial string Message { set; get; } = "";
    }
}
