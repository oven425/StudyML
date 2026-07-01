using LLama;
using LLama.Common;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ConsoleApp_MAF
{
    public class Gemma4ChatClient(string modelpath) : IChatClient
    {
        ModelParams? m_Parameters;
        LLamaWeights? m_Weights;
        LLamaContext? m_Context;
        InteractiveExecutor? m_Executor;
        InferenceParams? m_InferenceParams;

        public void Dispose()
        {
            m_Context?.Dispose();
            this.m_Weights?.Dispose();
        }


        async Task Init(ChatOptions? options)
        {
            if (m_Parameters == null)
            {
                var tools = "";
                if (options != null)
                {
                    foreach (var oo in options.Tools)
                    {

                        if (oo is AIFunction aifun)
                        {

                            string schemaJson = JsonSerializer.Serialize(aifun.JsonSchema, new JsonSerializerOptions { WriteIndented = false });
                            var tt = $"<|tool>declaration:{aifun.Name}<tool|>";
                        }

                        var tool = """
                        <|tool>declaration:get_datetime{
                          description: <|"|>取得現在的時間<|"|>,
                          parameters: {          
                          }
                        }<tool|>
                        """;
                        tools = tool;
                    }
                }
                m_SystemPrompt = $"""
                <|turn>system
                {options?.Instructions ?? ""}
                {tools}
                <turn|>
                """;

                this.m_Parameters = new ModelParams(modelpath)
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


            }
        }

        string m_SystemPrompt = "";
        bool m_IsFirst = true;
        async public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var contents = new List<AIContent>();
            await Init(options);
            var strb = new StringBuilder();
            var lastmsg = messages.LastOrDefault();
            if (lastmsg != null)
            {
                string prompt_user = $"""
                <|turn>user
                {lastmsg.Text}<turn|>
                <|turn>model
                """;
                if (m_IsFirst)
                {
                    prompt_user = $"{m_SystemPrompt}\n{prompt_user}";
                    m_IsFirst = false;
                }

                await foreach (var token in this.m_Executor.InferAsync(prompt_user, this.m_InferenceParams))
                {
                    strb.Append(token);
                }
            }
            ChatResponse? response = null;
            string toolCallPattern = @"<\|tool_call>(.*?)<tool_call\|>";
            var resp_str = strb.ToString();
            MatchCollection toolCallMatchs = Regex.Matches(resp_str, toolCallPattern, RegexOptions.Singleline);
            if (toolCallMatchs.Count > 0)
            {
                var functionCalls = new List<FunctionCallContent>();
                foreach (Match match in toolCallMatchs)
                {
                    string toolCallJson = match.Groups[1].Value.Trim();
                    try
                    {
                        var doc = JsonDocument.Parse(toolCallJson);
                        string funcName = doc.RootElement.GetProperty("name").GetString() ?? "";
                        IDictionary<string, object?>? arguments = null;
                        if (doc.RootElement.TryGetProperty("arguments", out var argsElem))
                        {
                            arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(argsElem.GetRawText());
                        }

                        string callId = Guid.NewGuid().ToString("N")[..8];
                        functionCalls.Add(new FunctionCallContent(callId, funcName, arguments));
                    }
                    catch { /* 略過格式錯誤的 tool call */ }
                }
                var assistantMsg = new ChatMessage(ChatRole.Assistant, [.. functionCalls]);
                response = new ChatResponse(assistantMsg)
                {
                    FinishReason = ChatFinishReason.ToolCalls
                };

            }


            response ??= new(new ChatMessage(ChatRole.Assistant, strb.ToString()));
            return response;
        }

        private string BuildPrompt(IEnumerable<ChatMessage> messages)
        {
            var sb = new StringBuilder();
            bool isFirst = m_IsFirst;
            if (isFirst)
            {
                sb.Append(m_SystemPrompt);
                sb.AppendLine();
                m_IsFirst = false;
            }

            foreach (var msg in messages)
            {
                if (msg.Role == ChatRole.User)
                {
                    sb.AppendLine($"<|turn>user\n{msg.Text}<turn|>");
                }
                else if (msg.Role == ChatRole.Assistant)
                {
                }
                else if (msg.Role == ChatRole.Tool)
                {
                    foreach (var content in msg.Contents.OfType<FunctionResultContent>())
                    {
                        sb.AppendLine($"<|tool_response>{JsonSerializer.Serialize(content.Result)}<tool_response|>");
                    }
                }
            }

            sb.AppendLine("<|turn>model");
            return sb.ToString();
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return this;
        }

        async public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            await Init(options);


            await foreach (var token in this.m_Executor.InferAsync("", this.m_InferenceParams))
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, token)
                {
                    FinishReason = ChatFinishReason.Stop,
                };
            }
        }
    }
}
