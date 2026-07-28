// See https://aka.ms/new-console-template for more information
using ConsoleApp_MAF;
using LLama;
using LLama.Common;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.ComponentModel;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("Hello, World!");
string m_ModelPath = @"..\..\..\..\gguf_gemma4\gemma-4-E2B-it-Q4_K_M.gguf";
string m_MmProjPath = @"..\..\..\..\gguf_gemma4\mmproj-gemma-4-E2B-it-Q8_0.gguf";


//using var codeAct = new HyperlightCodeActProvider(HyperlightCodeActProviderOptions.CreateForWasm(guestPath));
OpenMeteo om = new();
ComputerInfo info = new();
FileOperation fs = new();
var tools = new AIFunction[]
{
    AIFunctionFactory.Create(info.GetCurrentDateTime),
    AIFunctionFactory.Create(info.GetCurrentUser),
    AIFunctionFactory.Create(info.GetFolder),
    AIFunctionFactory.Create(info.list_directory),
    AIFunctionFactory.Create(fs.ReadTxt),
    AIFunctionFactory.Create(fs.ReadImage),
    AIFunctionFactory.Create(fs.GetFullPath),
    AIFunctionFactory.Create(get_currentlocation),
    AIFunctionFactory.Create(om.GetCurrent)
    
};

var option = new ChatOptions()
{
    Instructions = $"""
    你是個Windows助理,所有回答要有禮貌以及使用繁體中文
    桌面的路徑是{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)}
    """,
    //Tools = tools,

};


var gemma4client = new Gemma4ChatClient(m_ModelPath, m_MmProjPath);


var skillsDir = Path.Combine(Directory.GetCurrentDirectory(), "skills");
var fileOptions = new AgentFileSkillsSourceOptions
{
    ResourceFilter = context => false,  // 排除所有 resources
    ScriptFilter = context => false,    // 排除所有 scripts
};
var files = new AgentFileSkillsSource(["./skills"]);
var aa = await files.GetSkillsAsync(null);

var agentSkillsProvider = new AgentSkillsProvider(skillsDir, fileOptions: fileOptions);

var funcclient = gemma4client.AsBuilder()
    .UseFunctionInvocation()
    .Build();



//var aaresp = await funcclient.GetResponseAsync("現在的位置的天氣?", option);
var cm = new ChatMessage(ChatRole.User,
    [
    await DataContent.LoadFromAsync("a.jpg"),
        //new TextContent("用中文描述這張圖片"),
        new TextContent("這張圖片裡面有什麼東西?"),
    ]);
//var aaresp1 = await funcclient.GetResponseAsync(cm, option);
var trackingContextProvider = new TrackingContextProvider();



//var skill = new AgentInlineSkill(
//        name: "unit-converter",
//        description: "Converts between measurement units.",
//        instructions: """
//            Use this skill to convert values between metric and imperial units.
//            Refer to the conversion-table resource for supported unit pairs.
//            Run the convert script to perform conversions.
//            """
//    )
//    .AddResource("kg=2.205lb, m=3.281ft, L=0.264gal", "conversion-table", "Supported unit pairs");

//var source = new AgentInMemorySkillsSource([skill]);

//var provider = new AgentSkillsProvider(source);

var agent = funcclient.AsAIAgent(new ChatClientAgentOptions()
{
    Name ="assiant",
    ChatOptions = option,
    UseProvidedChatClientAsIs=true,
    AIContextProviders = [agentSkillsProvider, trackingContextProvider]
});


while(true)
{
    Console.Write("User:");
    var question = Console.ReadLine();
    var runresp = await agent.RunAsync(question);
    Console.Write("Assistant:");
    Console.WriteLine($"{runresp.Usage?.TotalTokenCount}");
    var functionApprovalRequests = runresp.Messages
    .SelectMany(x => x.Contents)
    .OfType<ToolApprovalRequestContent>()
    .ToList();
    if(functionApprovalRequests.Count >0)
    {
        foreach (var oo in functionApprovalRequests)
        {
            var approvalMessage = new ChatMessage(ChatRole.User, [oo.CreateResponse(true)]);
            Console.WriteLine(await agent.RunAsync(approvalMessage));
        }
    }
    else
    {
        Console.WriteLine(runresp.Text);
    }
    
}

async Task RunCopilotAsync()
{
    await using var copilotClient = new GitHub.Copilot.CopilotClient();
    await copilotClient.StartAsync();

#pragma warning disable GHCP001
    var sessionConfig = new GitHub.Copilot.SessionConfig
    {
        Tools = tools,
        SystemMessage = new GitHub.Copilot.SystemMessageConfig
        {
            Content = $"""
                你是個 Windows 助理，所有回答要有禮貌以及使用繁體中文。
                桌面的路徑是 {Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)}
                """
        },
        OnPermissionRequest = (request, _) =>
        {
            Console.WriteLine($"Copilot 權限請求：{JsonSerializer.Serialize(request, new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            })}");
            Console.Write("允許這次操作嗎？(y/N): ");

            var approved = Console.ReadLine()?.Equals("y", StringComparison.OrdinalIgnoreCase) == true;
            return Task.FromResult<GitHub.Copilot.Rpc.PermissionDecision>(
                approved
                    ? new GitHub.Copilot.Rpc.PermissionDecisionApproveOnce()
                    : new GitHub.Copilot.Rpc.PermissionDecisionReject());
        }
    };
#pragma warning restore GHCP001
    Console.WriteLine($"Copilot 模型：{sessionConfig.Model ?? "使用服務預設模型"}");
    Console.WriteLine($"思考強度：{sessionConfig.ReasoningEffort ?? "使用模型預設強度"}");
    AIAgent copilotAgent = GitHub.Copilot.CopilotClientExtensions.AsAIAgent(
        copilotClient,
        sessionConfig: sessionConfig);

    AgentSession session = await copilotAgent.CreateSessionAsync();

    while (true)
    {
        Console.Write("Copilot User: ");
        string? question = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(question) ||
            question.Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            break;
        }

        AgentResponse response = await copilotAgent.RunAsync(question, session);
        Console.WriteLine($"Copilot Assistant: {response}");
    }
}




//


//https://github.com/microsoft/agent-framework/tree/main
var resp_agent = await agent.RunAsync("現在幾點?");

[Description("取得現在使用者的城市和經緯度")]
async Task<ToolResponse> get_currentlocation()
{
    var resp = new ToolResponse();
    try
    {
        using var client = new HttpClient();
        string response = await client.GetStringAsync("http://ip-api.com/json/");
        var ipapi = JsonSerializer.Deserialize<IpApiResponse>(response);
        if(ipapi != null)
        {
            resp.Data = $"城市:{ipapi.City},經度:{ipapi.Lon},緯度:{ipapi.Lat}";
        }
        
    }
    catch (Exception ex)
    {
        resp.FailMessgae = ex.Message;
    }
    
    return resp;
}


public class IpApiResponse
{
    [JsonPropertyName("country")]
    public string Country { get; set; }
    [JsonPropertyName("regionName")]
    public string RegionName { get; set; }
    [JsonPropertyName("city")]
    public string City { get; set; }
    [JsonPropertyName("lat")]
    public double Lat { get; set; }
    [JsonPropertyName("lon")]
    public double Lon { get; set; }
}


public class ToolResponse
{
    [JsonPropertyName("isFail")]
    public bool? IsFail => string.IsNullOrEmpty(FailMessgae)?null:true;
    [JsonPropertyName("failMessage")]
    public string? FailMessgae { set; get; } = null;
    [JsonPropertyName("data")]
    public string Data { set; get; } = string.Empty;
    [JsonPropertyName("imageFileName")]
    public string? ImageFileName { set; get; } = null;
}

class TrackingContextProvider : AIContextProvider
{
    protected override ValueTask<AIContext> InvokingCoreAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        var aiContext = context.AIContext!;
        Console.WriteLine($"{new string('-', 50)}Tools{new string('-', 50)}");
        foreach (var tool in aiContext.Tools ?? [])
        {
            if (tool is AIFunction function)
            {
                Console.WriteLine($"""
                    **{function.Name}**
                    Description: {function.Description}
                    JsonSchema: 
                    {JsonSerializer.Serialize(function.JsonSchema, new JsonSerializerOptions { WriteIndented = true })}

                    """);
            }
        }

        Console.WriteLine($"""
            {new string('-', 50)}Instructions{new string('-', 50)}
                {aiContext.Instructions}
            """);

        return base.InvokingCoreAsync(context, cancellationToken);
    }
}



