// See https://aka.ms/new-console-template for more information
using AgentFrameworkToolkit.Tools;
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

//await Audio.To("123.mp3");
//using var codeAct = new HyperlightCodeActProvider(HyperlightCodeActProviderOptions.CreateForWasm(guestPath));
OpenMeteo om = new();
ComputerInfo info = new();
FileOperation fs = new();
DataBase sqlitedb = new();
AIToolsFactory toolsFactory = new();
toolsFactory.GetTimeTools();


var tools = new AIFunction[]
{
    //AIFunctionFactory.Create(info.GetCurrentDateTime),
    //AIFunctionFactory.Create(info.GetCurrentUser),
    //AIFunctionFactory.Create(info.GetFolder),
    AIFunctionFactory.Create(info.GetFullName),
    //AIFunctionFactory.Create(info.list_directory),
    //AIFunctionFactory.Create(fs.ReadTxt),
    //AIFunctionFactory.Create(fs.ReadImage),
    //AIFunctionFactory.Create(fs.GetFullPath),
    //AIFunctionFactory.Create(get_currentlocation),
    //AIFunctionFactory.Create(om.GetCurrent),
    AIFunctionFactory.Create(sqlitedb.ListTables),
    AIFunctionFactory.Create(sqlitedb.GetTableSchemas),
    AIFunctionFactory.Create(sqlitedb.Query)

};

var option = new ChatOptions()
{
    //Instructions = $"""
    //你是個Windows助理,所有回答要有禮貌以及使用繁體中文
    //現在的路徑是{AppDomain.CurrentDomain.BaseDirectory}
    //桌面的路徑是{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)}
    //""",
    Instructions = $"""
    你是個電腦助理,所有回答要有禮貌以及使用繁體中文
    """,
    Tools = 
    [
        ..tools,
        //..toolsFactory.GetTimeTools(),
        ..toolsFactory.GetFileSystemTools()
    ],

};

var gemma4client = new Gemma4ChatClient(m_ModelPath, m_MmProjPath);


var skillsDir = Path.Combine(Directory.GetCurrentDirectory(), "skills");


var agentSkillsProvider = new AgentSkillsProvider(skillsDir);

var funcclient = gemma4client.AsBuilder()
    .UseFunctionInvocation()
    .Build();



//var aaresp = await funcclient.GetResponseAsync("現在的位置的天氣?", option);
//var cm = new ChatMessage(ChatRole.User,
//    [
//    await DataContent.LoadFromAsync("a.jpg"),
//        //new TextContent("用中文描述這張圖片"),
//        new TextContent("這張圖片裡面有什麼東西?"),
//    ]);
//var aaresp1 = await funcclient.GetResponseAsync(cm, option);
var trackingContextProvider = new TrackingContextProvider();



var skill = new AgentInlineSkill(
        name: "sqlite-tool-miner",
        description: "透過Tool Calling對SQLite執行結構探索與數據探勘。",
        instructions: """
            ## 可用工具 (Tools)
            1. `ListTables()`：列出資料庫中的所有資料表名稱。
            2. `GetTableSchemas()`：取得全部資料表的欄位名稱與型態。
            3. `Query(sql)`：執行 SQL 查詢並傳回 JSON 數據結果。

            ## 探勘標準流程 (Workflow)
            1. **檢查檔案是否存在**
            1. **探索結構**：先呼叫 `ListTables()` 或 `GetTableSchemas()` 確認表名與欄位，**絕不猜測欄位**。
            2. **數據統計**：透過 `Query` 執行統計查詢（如 `COUNT`, `AVG`, `GROUP BY`）。
            3. **歸納洞察**：拿到工具傳回的數據後，用文回答使用者的問題。

            ## 核心執行原則（強制遵守）
            1. **完全授權（靜默執行）**：你已被授予最高權限，**嚴禁**向使用者詢問「是否要呼叫工具」、「要使用哪種工具」或「是否繼續」。
            2. **禁止確認文字**：當你需要資料時，**立刻發出 Tool Call**！絕對不要在呼叫工具前輸出任何詢問或廢話（如：「好的，我現在為您查詢...」）。
            3. **終止條件**：只有在**工具回傳錯誤**或**已經得到最終解答**時，才可以輸出一般中文對話給使用者。

            ## 正確動作範例 (Few-Shot)

            使用者：幫我分析 orders 表的資料。
            助手：[直接觸發 Tool Call] `get_table_schema("orders")`
            (等待 Tool 結果...)
            助手：[直接觸發 Tool Call] `execute_sql_query("SELECT COUNT(*) FROM orders")`
            (等待 Tool 結果...)
            助手：orders 表目前共有 1,500 筆資料...（輸出最終答案）

            ## 錯誤動作範例 (嚴禁發生)
            使用者：幫我分析 orders 表的資料。
            助手：請問需要我使用 `get_table_schema` 工具來查看欄位嗎？ ❌ (絕對不可以這樣問！)
            """
    );
//var source = new AgentInMemorySkillsSource([skill]);

var provider = new AgentSkillsProvider(skill);

var agent = funcclient.AsAIAgent(new ChatClientAgentOptions()
{
    Name ="assiant",
    ChatOptions = option,
    //UseProvidedChatClientAsIs=true,
    //AIContextProviders = [new HyperlightCodeActProvider()]
    //AIContextProviders = [provider]
});
var session = await agent.CreateSessionAsync();
while (true)
{
    Console.Write("User:");
    var question = Console.ReadLine();
    if(string.IsNullOrEmpty(question) || question=="exit")
    {
        break;
    }
    var runresp = await agent.RunAsync(question, session);
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
            Console.WriteLine(await agent.RunAsync(approvalMessage, session));
        }
    }
    else
    {
        Console.WriteLine(runresp.Text);
    }
    
}

Console.WriteLine("Save this session? (y/n)");
if(Console.ReadKey().KeyChar == 'y')
{
    var json = session.ToJsonString();
    System.Diagnostics.Trace.WriteLine(json);
}


async Task RunCopilotAsync()
{
    await using var copilotClient = new GitHub.Copilot.CopilotClient();
    await copilotClient.StartAsync();
    var readOnlyToolOptions = new GitHub.Copilot.CopilotToolOptions { SkipPermission = true };
#pragma warning disable GHCP001
    var sessionConfig = new GitHub.Copilot.SessionConfig
    {
        Tools =
        [
            GitHub.Copilot.CopilotTool.DefineTool(sqlitedb.ListTables, toolOptions: readOnlyToolOptions),
            GitHub.Copilot.CopilotTool.DefineTool(sqlitedb.GetTableSchemas, toolOptions: readOnlyToolOptions),
            GitHub.Copilot.CopilotTool.DefineTool(sqlitedb.Query, toolOptions: readOnlyToolOptions)
        ],
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

    // 直接使用原生 CopilotSession（而非 AsAIAgent 包裝），才能訂閱到
    // AssistantReasoningEvent / ToolExecutionStartEvent 等底層事件。
    await using GitHub.Copilot.CopilotSession session = await copilotClient.CreateSessionAsync(sessionConfig);

    using var subscription = session.On<GitHub.Copilot.SessionEvent>(evt =>
    {
        switch (evt)
        {
            case GitHub.Copilot.AssistantReasoningEvent reasoning:
                Console.WriteLine($"[思考] {reasoning.Data?.Content}");
                break;

            case GitHub.Copilot.ToolExecutionStartEvent toolStart:
                Console.WriteLine($"[步驟開始] 工具={toolStart.Data?.ToolName} 參數={JsonSerializer.Serialize(toolStart.Data?.Arguments)}");
                break;

            case GitHub.Copilot.ToolExecutionCompleteEvent toolComplete:
                Console.WriteLine($"[步驟完成] ToolCallId={toolComplete.Data?.ToolCallId} 成功={toolComplete.Data?.Success}");
                break;
        }
    });

    while (true)
    {
        Console.Write("Copilot User: ");
        string? question = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(question) ||
            question.Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            break;
        }


        var idleTcs = new TaskCompletionSource<GitHub.Copilot.AssistantMessageEvent?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        GitHub.Copilot.AssistantMessageEvent? lastMessage = null;

        using var messageSubscription = session.On<GitHub.Copilot.SessionEvent>(evt =>
        {
            switch (evt)
            {
                case GitHub.Copilot.AssistantMessageEvent msg:
                    lastMessage = msg;
                    break;
                case GitHub.Copilot.SessionIdleEvent:
                    idleTcs.TrySetResult(lastMessage);
                    break;
            }
        });

        await session.SendAsync(question);
        var response = await idleTcs.Task;
        Console.WriteLine($"Copilot Assistant: {response?.Data?.Content}");
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

//public class ToolResponse<T>: ToolResponse
//{
//    public T? Data { set; get; }
//}
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

public static class ToolResponseExtension
{
    static public string ToJsonString<T>(this T src)
    {
        return JsonSerializer.Serialize(src);
    }
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



