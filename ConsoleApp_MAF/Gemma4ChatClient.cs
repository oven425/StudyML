using CommunityToolkit.HighPerformance.Helpers;
using LLama;
using LLama.Common;
using LLama.Native;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ConsoleApp_MAF
{
    public class Gemma4ChatClient(string modelpath, string? mtm_modepath = null) : IChatClient
    {
        ModelParams? m_Parameters;
        LLamaWeights? m_Weights;
        LLamaContext? m_Context;
        InteractiveExecutor? m_Executor;
        InferenceParams? m_InferenceParams;
        MtmdWeights? m_MtmdWeights;
        MtmdContextParams? m_MtmdContextParams;
        public void Dispose()
        {
            m_Context?.Dispose();
            this.m_Weights?.Dispose();
        }


        async Task Init(ChatOptions? options)
        {
            if (m_Parameters == null)
            {
                var strb_tool = new StringBuilder();
                if (options != null)
                {
                    foreach (var oo in options.Tools)
                    {
                        var str_pps = "";
                        if (oo is AIFunction aifun)
                        {

                            str_pps = JsonSerializer.Serialize(aifun.JsonSchema, new JsonSerializerOptions()
                            {
                                WriteIndented = true,
                                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                            });
                        }
                        str_pps = str_pps.Replace("\"", "<|\"|>");
                        string tool = $$"""
                            <|tool>declaration:{{oo.Name}}{
                              description: <|"|>{{oo.Description}}<|"|>,
                              parameters: {{str_pps}}
                            }<tool|>
                            """;
                        if(strb_tool.Length >0)
                        {
                            strb_tool.AppendLine();
                        }
                        strb_tool.Append(tool);
                    }
                }
                m_SystemPrompt = $"""
                <|turn>system
                {options?.Instructions ?? ""}
                {strb_tool}
                <turn|>
                """;

                this.m_Parameters = new ModelParams(modelpath)
                {
                    ContextSize = 81920,
                    GpuLayerCount = 0,
                };
                this.m_Weights = await LLamaWeights.LoadFromFileAsync(this.m_Parameters);
                this.m_Context = this.m_Weights.CreateContext(this.m_Parameters);
                if(!string.IsNullOrEmpty(mtm_modepath))
                {
                    this.m_MtmdContextParams = new MtmdContextParams();
                    this.m_MtmdWeights = await MtmdWeights.LoadFromFileAsync(mtm_modepath, this.m_Weights, this.m_MtmdContextParams);
                }

                if(this.m_MtmdWeights != null)
                {
                    this.m_Executor = new InteractiveExecutor(this.m_Context, m_MtmdWeights);
                }
                else
                {
                    this.m_Executor = new InteractiveExecutor(this.m_Context);
                }
                

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

            await Init(options);
            if (m_Executor is null) return new ChatResponse();
            var strb = new StringBuilder();
            var lastmsg = messages.LastOrDefault();
            if (lastmsg != null)
            {
                var prompt_user = "";
                if (lastmsg.Role == ChatRole.Tool)
                {
                    foreach (var content in lastmsg.Contents.OfType<FunctionResultContent>())
                    {
                        strb.Append($"<|tool_response>{JsonSerializer.Serialize(content.Result)}<tool_response|>");
                        
                    }
                    prompt_user = strb.ToString();
                    strb.Clear();
                }
                else if(lastmsg.Role == ChatRole.User)
                {
                    foreach(var oo in  lastmsg.Contents)
                    {
                        
                        if(this.m_MtmdWeights != null && oo is DataContent dc)
                        {
                            var jpg_a = m_MtmdWeights.LoadMedia(dc.Data.Span);
                            m_Executor.Embeds.Add(jpg_a);
                        }
                    }
                    prompt_user = $"""
                        <|turn>user
                        {lastmsg.Text}<turn|>
                        <|turn>model
                        """;
                    if (m_IsFirst)
                    {
                        prompt_user = $"{m_SystemPrompt}\n{prompt_user}";
                        m_IsFirst = false;
                    }
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
                    (string action, string args) = NormailiszeCToolCall(toolCallJson);
                    try
                    {
                        IDictionary<string, object?>? arguments = null;
                        if(!string.IsNullOrEmpty(args))
                        {
                            arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(args);
                        }
                        string callId = Guid.NewGuid().ToString("N")[..8];
                        functionCalls.Add(new FunctionCallContent(callId, action, arguments));
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

        (string action, string argsContent) NormailiszeCToolCall(string input)
        {
            string basePattern = @"call:(?<action>\w+)\{(?<argsContent>.*?)\}";
            Match match = Regex.Match(input, basePattern, RegexOptions.Singleline);

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

    //public static class Gemma4Extensions
    //{
    //    public static Gemma4ChatBuilder EnableReasoningTrace(this Gemma4ChatBuilder builder)
    //    {
    //        return builder.AddMiddleware(async (context, next) =>
    //        {
    //            // 在 prompt 前加上 <|think|>
    //            context.Input = "<|think|>\n" + context.Input;

    //            await next();

    //            // 解析模型輸出
    //            var raw = context.Response.Text;
    //            context.Response.ReasoningTrace = ExtractBetween(raw, "<|think|>", "<|assistant|>");
    //            context.Response.Answer = ExtractAfter(raw, "<|assistant|>");
    //        });
    //    }
    //}
}
