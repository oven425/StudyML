// See https://aka.ms/new-console-template for more information
using LLama;
using LLama.Common;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Text.Json;
//Microsoft.Agents.AI.AgentFileSkill;
Console.WriteLine("Hello, World!");
string m_ModelPath = @"..\..\..\..\gguf_gemma4\gemma-4-E2B-it-Q4_K_M.gguf";

var tools = new AIFunction[]
{
    AIFunctionFactory.Create(get_datetime)
};
var ssss = tools[0].JsonSchema.ToString().Replace("\"", "<|\"|>");
var option = new ChatOptions
{
    Tools = tools
};

var aa = new Gemma4ChatClient(m_ModelPath);


var res = await aa.GetResponseAsync("", option);
var agent = aa.AsAIAgent(new ChatClientAgentOptions()
{
    Name ="assiant",
    ChatOptions = new ChatOptions()
    {
        Instructions = "111",
        Tools = 
        [
            AIFunctionFactory.Create(get_datetime)
        ]
    }
});

var session = await agent.CreateSessionAsync("");

[Description("讀取資料夾的內容")]
string get_datetime(string folder)
{
    var resp = JsonSerializer.Serialize(new ToolResponse()
    {
        Data = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")
    });

    return resp;
}


public class ToolResponse
{
    public bool IsFail { get; set; }
    public string FailMessgae { set; get; } = string.Empty;
    public string Data { set; get; } = string.Empty;
}

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
            
            var tools = "";
            if(options != null)
            {
                foreach(var oo in options.Tools)
                {
                    var tool = """
                        <|tool>declaration:list_directory{
                          description: <|"|><|"|>,
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
                        """;
                }
            }
            m_SystemPrompt = """
                <|turn>system
                你是個Windows助理,所有回答要有禮貌以及使用繁體中文
                <turn|>
                """;
        }
    }

    string m_SystemPrompt = "";

    async public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        await Init(options);

        var strb = new StringBuilder();
        await foreach (var token in this.m_Executor.InferAsync("", this.m_InferenceParams))
        {
            strb.Append(token);
        }
        ChatResponse response = new(new ChatMessage(ChatRole.Assistant, strb.ToString()));
        return response;
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
            yield return new ChatResponseUpdate(ChatRole.Assistant,token)
            {
                FinishReason = ChatFinishReason.Stop,
            };
        }
    }
}