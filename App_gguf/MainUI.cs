using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLama;
using LLama.Abstractions;
using LLama.Common;
using LLama.Sampling;
using Microsoft.Extensions.AI;
using Microsoft.UI.Xaml.Documents;
using Microsoft.VisualBasic.FileIO;
using QSoft.GGUF;
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
        Gemma4? m_Gemma4;

        [ObservableProperty]
        public partial bool IsLoaded { get; set; } = false;
        [RelayCommand]
        async Task Send()
        {
            string m_ModelPath = @"..\..\..\..\gguf_gemma4\gemma-4-E2B-it-Q4_K_M.gguf";
            string m_MmProjPath = @"..\..\..\..\gguf_gemma4\mmproj-gemma-4-E2B-it-Q8_0.gguf";
            m_Gemma4 ??= new(m_ModelPath, m_MmProjPath);
            var funcclient = m_Gemma4.AsBuilder().UseFunctionInvocation().Build();
            //var agent = funcclient.AsAIAgent(new ChatClientAgentOptions()
            //{
            //    Name = "assiant",
            //    ChatOptions = option,
            //    //AIContextProviders = [todoProvider, modeProvider, trackingContextProvider]
            //    //UseProvidedChatClientAsIs=true,
            //    //AIContextProviders = [new HyperlightCodeActProvider()]
            //    //AIContextProviders = [provider]
            //});
            //var session = await agent.CreateSessionAsync();
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
