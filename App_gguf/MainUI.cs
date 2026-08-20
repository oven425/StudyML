using AgentFrameworkToolkit.Tools;
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
        IChatClient? m_ChatClient;

        [ObservableProperty]
        public partial bool IsLoaded { get; set; } = false;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendCommand))]
        public partial string InputText { get; set; } = "";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendCommand))]
        [NotifyPropertyChangedFor(nameof(IsInputEnabled))]
        public partial bool IsSending { get; set; } = false;

        /// <summary>Bound to the input TextBox so it locks while a response is being generated.</summary>
        public bool IsInputEnabled => !IsSending;

        [ObservableProperty]
        public partial string TokenStatus { get; set; } = "尚無 token 使用資訊";
        AIFunction[]? m_Tools;

        bool CanSend() => !IsSending && !string.IsNullOrWhiteSpace(InputText);
        AIToolsFactory? m_ToolsFactory;
        ChatOptions? m_ChatOptions;
        [RelayCommand(CanExecute = nameof(CanSend))]
        async Task Send()
        {
            var userText = InputText.Trim();
            if (string.IsNullOrEmpty(userText))
            {
                return;
            }

            InputText = string.Empty;
            Historys.Add(new History { Role = History.RoleKind.User, Message = userText });
            History? assistantEntry = null;

            IsSending = true;
            try
            {
                m_ToolsFactory ??= new AIToolsFactory();
                
                string m_ModelPath = @"..\..\..\..\..\gguf_gemma4\gemma-4-E2B-it-Q4_K_M.gguf";
                string m_MmProjPath = @"..\..\..\..\..\gguf_gemma4\mmproj-gemma-4-E2B-it-Q8_0.gguf";
                m_Gemma4 ??= new(m_ModelPath, m_MmProjPath);
                m_ChatClient ??= m_Gemma4.AsBuilder().UseFunctionInvocation().Build();
                m_ChatOptions ??= new ChatOptions()
                    {
                        Tools =
                        [
                            ..m_ToolsFactory.GetTimeTools()
                        ]
                    };
                var messages = new List<ChatMessage> { new(ChatRole.User, userText) };
                var toolNamesByCallId = new Dictionary<string, string>(StringComparer.Ordinal);
                var displayedToolCallIds = new HashSet<string>(StringComparer.Ordinal);
                var displayedToolResultIds = new HashSet<string>(StringComparer.Ordinal);
                UsageDetails? usage = null;

                await foreach (var update in m_ChatClient.GetStreamingResponseAsync(messages, m_ChatOptions))
                {
                    foreach (var content in update.Contents)
                    {
                        switch (content)
                        {
                            case TextContent { Text: { Length: > 0 } text }:
                                assistantEntry ??= new History { Role = History.RoleKind.AI };
                                if (!Historys.Contains(assistantEntry))
                                {
                                    Historys.Add(assistantEntry);
                                }

                                assistantEntry.Message += text;
                                break;

                            case FunctionCallContent functionCall when displayedToolCallIds.Add(functionCall.CallId):
                                toolNamesByCallId[functionCall.CallId] = functionCall.Name;
                                Historys.Add(new History
                                {
                                    Role = History.RoleKind.ToolCall,
                                    Title = $"正在呼叫 {functionCall.Name}",
                                    Message = FormatToolValue(functionCall.Arguments)
                                });
                                assistantEntry = null;
                                break;

                            case FunctionResultContent functionResult when displayedToolResultIds.Add(functionResult.CallId):
                                var toolName = toolNamesByCallId.GetValueOrDefault(functionResult.CallId, "工具");
                                Historys.Add(new History
                                {
                                    Role = History.RoleKind.ToolResult,
                                    Title = $"{toolName} 回應",
                                    Message = FormatToolValue(functionResult.Result)
                                });
                                assistantEntry = null;
                                break;

                            case UsageContent usageContent:
                                usage = usageContent.Details;
                                break;
                        }
                    }
                }

                if (assistantEntry is null)
                {
                    Historys.Add(new History { Role = History.RoleKind.AI, Message = "(無回應)" });
                }
                else if (string.IsNullOrEmpty(assistantEntry.Message))
                {
                    assistantEntry.Message = "(無回應)";
                }

                if (usage is not null)
                {
                    var cacheHitRate = usage.AdditionalCounts is not null && usage.AdditionalCounts.TryGetValue("CacheHitRate", out var rate)
                        ? rate
                        : 0;
                    TokenStatus = $"輸入 {usage.InputTokenCount ?? 0} · 輸出 {usage.OutputTokenCount ?? 0} · 總計 {usage.TotalTokenCount ?? 0} tokens（快取命中率 {cacheHitRate}%）";
                }
            }
            catch (Exception ex)
            {
                assistantEntry.Message = $"發生錯誤: {ex.Message}";
            }
            finally
            {
                IsLoaded = true;
                IsSending = false;
            }
        }

        private static string FormatToolValue(object? value)
            => value switch
            {
                null => "null",
                string text => text,
                _ => JsonSerializer.Serialize(value)
            };
    }



    public partial class History : ObservableObject
    {
        public enum RoleKind
        {
            AI,
            User,
            ToolCall,
            ToolResult
        }

        [ObservableProperty]
        public partial RoleKind Role { get; set; } = RoleKind.AI;

        [ObservableProperty]
        public partial string Title { get; set; } = "";

        [ObservableProperty]
        public partial string Message { set; get; } = "";
    }
}
