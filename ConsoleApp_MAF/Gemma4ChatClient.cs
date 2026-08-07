using CommunityToolkit.HighPerformance.Helpers;
using LLama;
using LLama.Common;
using LLama.Native;
using Microsoft.Agents.AI;
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
        InferenceParams? m_InferenceParams_Zero;
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
                    foreach (var oo in options.Tools ?? [])
                    {
                        var name = oo.GetType().Name;
                        var str_pps = "";
                        if(oo is ApprovalRequiredAIFunction apfun)
                        {
                            str_pps = JsonSerializer.Serialize(apfun.JsonSchema, new JsonSerializerOptions()
                            {
                                WriteIndented = true,
                                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                            });
                        }
                        else if (oo is AIFunction aifun)
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
                        if (strb_tool.Length > 0)
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
                    ContextSize = 8192,
                    GpuLayerCount = 0,
                    Threads = 4,
                    BatchThreads = 12,
                    UseMemorymap = true,
                    //UseMemoryLock = true,
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
                    MaxTokens = 8192,
                    AntiPrompts = ["<turn|>"]
                };
                this.m_InferenceParams_Zero = new InferenceParams()
                {
                    MaxTokens = 0,
                    AntiPrompts = ["<turn|>"]
                };

            }
        }
        JsonSerializerOptions jsonoptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        };
        UsageDetails? m_UsageDetails;
        string m_SystemPrompt = "";
        bool m_IsFirst = true;
        async public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {

            await Init(options);
            
            if (m_Executor is null) return new ChatResponse();
            m_UsageDetails ??= new UsageDetails()
            {
                TotalTokenCount = 0,
            };
            List<string> prompts = [];
            var strb = new StringBuilder();
            var lastmsg = messages.LastOrDefault();
            if (lastmsg != null)
            {
                if (lastmsg.Role == ChatRole.Tool)
                {
                    foreach (var content in lastmsg.Contents.OfType<FunctionResultContent>())
                    {
                        if (content.Result is JsonElement jsonResult)
                        {
                            
                            if(jsonResult.ValueKind == JsonValueKind.String)
                            {
                                var kk = jsonResult.ValueKind;
                                var kk_str = jsonResult.GetString()
                                    .Replace("<available_resources />", "")
                                    .Replace("<available_scripts />", "")
                                    .TrimEnd();
                                strb.Append($"<|tool_response>{kk_str}<tool_response|>");
                                continue;
                            }
                            else
                            {
                                var toolresp = JsonSerializer.Deserialize<ToolResponse>(jsonResult);
                                if (!string.IsNullOrEmpty(toolresp?.ImageFileName))
                                {
                                    if (this.m_MtmdWeights == null)
                                    {
                                        strb.Append($"<|tool_response>不支援多模態<tool_response|>");
                                        continue;
                                    }
                                    else if (File.Exists(toolresp.ImageFileName))
                                    {
                                        var vv = await File.ReadAllBytesAsync(toolresp.ImageFileName);
                                        this.m_Executor.Embeds.Add(this.m_MtmdWeights.LoadMedia(vv));
                                        prompts.Add("");
                                    }
                                    toolresp.ImageFileName = null;
                                    strb.Append($"<|tool_response>{JsonSerializer.Serialize(toolresp, jsonoptions)}<tool_response|>");
                                    continue;
                                }
                                strb.Append($"<|tool_response>{JsonSerializer.Serialize(content.Result, jsonoptions)}<tool_response|>");
                            }
                            

                        }


                    }
                    prompts.Add(strb.ToString());
                    strb.Clear();
                }
                else if(lastmsg.Role == ChatRole.User)
                {
                    strb.Append("<|turn>user\n");
                    foreach (var oo in  lastmsg.Contents)
                    {
                        if(oo is TextContent tc)
                        {
                            strb.Append(tc.Text);
                        }
                        else if(this.m_MtmdWeights != null && oo is DataContent dc)
                        {
                            m_Executor.Embeds.Add(m_MtmdWeights.LoadMedia(dc.Data.Span));
                        }
                    }
                    strb.Append("\n<turn|>\n<|turn>model");
                    if (m_IsFirst)
                    {
                        if(m_Executor.Embeds.Count == 0)
                        {
                            prompts.Add($"{m_SystemPrompt}\n{strb}");
                        }
                        else
                        {
                            prompts.Add(m_SystemPrompt);
                            prompts.Add(strb.ToString());
                        }
                        m_IsFirst = false;
                    }
                    else
                    {
                        prompts.Add(strb.ToString());
                    }
                }
                foreach(var oo in prompts)
                {
                    var timingsBefore = this.m_Context.NativeHandle.GetTimings();
                    strb.Clear();
                    System.Diagnostics.Trace.WriteLine(oo);
                    var param = string.IsNullOrEmpty(oo) ? this.m_InferenceParams_Zero : this.m_InferenceParams;
                    await foreach (var token in this.m_Executor.InferAsync(oo, param, cancellationToken))
                    {
                        strb.Append(token);
                    }
                    var timingsAfter = this.m_Context.NativeHandle.GetTimings();
                    CalcToken(timingsBefore, timingsAfter);
                    System.Diagnostics.Trace.WriteLine(strb.ToString());
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
                        if (!string.IsNullOrEmpty(args))
                        {
                            arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(args, new JsonSerializerOptions
                            {
                                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                            });
                        }
                        string callId = Guid.NewGuid().ToString("N")[..8];
                        functionCalls.Add(new FunctionCallContent(callId, action, arguments));
                    }
                    catch(Exception ee)
                    {
                        System.Diagnostics.Trace.WriteLine(ee.Message);
                    }
                }
                var assistantMsg = new ChatMessage(ChatRole.Assistant, [.. functionCalls]);
                response = new ChatResponse(assistantMsg)
                {
                    FinishReason = ChatFinishReason.ToolCalls

                };

            }


            response ??= new(new ChatMessage(ChatRole.Assistant, strb.ToString()));
            response.Usage = this.m_UsageDetails;
            return response;
        }


        void CalcToken(LLamaPerfContextTimings before, LLamaPerfContextTimings after)
        {
            m_UsageDetails ??= new UsageDetails();

            var inputTokens = after.PrompTokensEvaluated - before.PrompTokensEvaluated;
            var outputTokens = after.TokensEvaluated - before.TokensEvaluated;
            var promptMilliseconds = Math.Max(0L, (long)Math.Round((after.PromptEval - before.PromptEval).TotalMilliseconds));
            var generationMilliseconds = Math.Max(0L, (long)Math.Round((after.Eval - before.Eval).TotalMilliseconds));

            m_UsageDetails.InputTokenCount = (m_UsageDetails.InputTokenCount ?? 0) + inputTokens;
            m_UsageDetails.OutputTokenCount = (m_UsageDetails.OutputTokenCount ?? 0) + outputTokens;
            m_UsageDetails.TotalTokenCount = (m_UsageDetails.InputTokenCount ?? 0) + (m_UsageDetails.OutputTokenCount ?? 0);

            var additionalCounts = m_UsageDetails.AdditionalCounts ??= new();
            additionalCounts["PromptEvaluationMilliseconds"] = GetAdditionalCount(additionalCounts, "PromptEvaluationMilliseconds") + promptMilliseconds;
            additionalCounts["GenerationMilliseconds"] = GetAdditionalCount(additionalCounts, "GenerationMilliseconds") + generationMilliseconds;
            additionalCounts["PromptTokensPerSecondX1000"] = CalculateTokensPerSecondX1000(m_UsageDetails.InputTokenCount ?? 0, additionalCounts["PromptEvaluationMilliseconds"]);
            additionalCounts["OutputTokensPerSecondX1000"] = CalculateTokensPerSecondX1000(m_UsageDetails.OutputTokenCount ?? 0, additionalCounts["GenerationMilliseconds"]);

            System.Diagnostics.Trace.WriteLine(
                $"input={inputTokens}, output≈{outputTokens}, " +
                $"prompt={promptMilliseconds}ms, generation={generationMilliseconds}ms, " +
                $"output={additionalCounts["OutputTokensPerSecondX1000"] / 1000d:F2} tok/s");
        }

        static long GetAdditionalCount(AdditionalPropertiesDictionary<long> additionalCounts, string key)
            => additionalCounts.TryGetValue(key, out var value) ? value : 0;

        static long CalculateTokensPerSecondX1000(long tokenCount, long elapsedMilliseconds)
            => elapsedMilliseconds > 0
                ? (long)Math.Round(tokenCount * 1_000_000d / elapsedMilliseconds)
                : 0;

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
            //standardizedArgs = Regex.Replace(standardizedArgs, @"\\(?![""\\/bfnrt]|u[0-9a-fA-F]{4})", @"\\");
            //standardizedArgs = Regex.Replace(standardizedArgs, @"(?<!\\)\\(?![\\""/bfnrtu])", @"\\");
            //            <| tool_call > call:GetCurrent{
            //                "Long": 121.6577,
            //  "Lat": 25.0696
            //}< tool_call |>
            standardizedArgs = standardizedArgs.Replace(@"\", @"\\");
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
}
