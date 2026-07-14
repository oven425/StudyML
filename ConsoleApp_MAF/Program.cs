// See https://aka.ms/new-console-template for more information
using ConsoleApp_MAF;
using LLama;
using LLama.Common;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
Console.OutputEncoding = System.Text.Encoding.UTF8;
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
    AIFunctionFactory.Create(get_currentlocation),
    AIFunctionFactory.Create(om.GetCurrent)
    
};

var option = new ChatOptions()
{
    Instructions = $"""
    你是個Windows助理,所有回答要有禮貌以及使用繁體中文
    桌面的路徑是{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)}
    """,
    Tools = tools,
};


var gemma4client = new Gemma4ChatClient(m_ModelPath, m_MmProjPath);

var funcclient = gemma4client.AsBuilder().UseFunctionInvocation().Build();



//var aaresp = await funcclient.GetResponseAsync("現在的位置的天氣?", option);
var cm = new ChatMessage(ChatRole.User,
    [
    await DataContent.LoadFromAsync("a.jpg"),
        new TextContent("用中文描述這張圖片"),

    ]);
//var aaresp1 = await funcclient.GetResponseAsync(cm, option);


var agent = funcclient.AsAIAgent(new ChatClientAgentOptions()
{
    Name ="assiant",
    ChatOptions = option,
    //AIContextProviders = [new TextSearchProvider()]
    //AIContextProviders = [new TodoProvider()]
});
var session = await agent.CreateSessionAsync();

while(true)
{
    Console.Write("User:");
    var question = Console.ReadLine();
    var runresp = await agent.RunAsync(question);
    Console.Write("Assistant:");
    Console.WriteLine(runresp.Text);
}
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


