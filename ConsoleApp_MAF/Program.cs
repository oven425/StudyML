// See https://aka.ms/new-console-template for more information
using ConsoleApp_MAF;
using LLama;
using LLama.Common;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using static LLama.Common.ChatHistory;
using static System.Runtime.InteropServices.JavaScript.JSType;

var rrrr = await get_currentlocation();
Console.WriteLine("Hello, World!");
string m_ModelPath = @"..\..\..\..\gguf_gemma4\gemma-4-E2B-it-Q4_K_M.gguf";

var tools = new AIFunction[]
{
    AIFunctionFactory.Create(get_datetime),
    AIFunctionFactory.Create(get_currentuser),
    AIFunctionFactory.Create(get_currentlocation)
};

var option = new ChatOptions()
{
    Instructions = "你是個Windows助理,所有回答要有禮貌以及使用繁體中文",
    Tools = tools
};

var gemma4client = new Gemma4ChatClient(m_ModelPath);
var funcclient = gemma4client.AsBuilder().UseFunctionInvocation().Build();
var aaresp = await funcclient.GetResponseAsync("現在的使用者是誰和現在幾點?", option);

var agent = funcclient.AsAIAgent(new ChatClientAgentOptions()
{
    Name ="assiant",
    ChatOptions = option
});

var resp_agent = await agent.RunAsync("現在幾點?");

[Description("取得現在的時間")]
ToolResponse get_datetime()
    => new() { Data = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") };
[Description("取得windows現在的使用者名稱")]
ToolResponse get_currentuser()
    => new() { Data = Environment.UserName };
[Description("取得現在使用者的城市/GPS")]
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
            resp.Data = $"City:{ipapi.City},Lon:{ipapi.Lon}Lat:{ipapi.Lat}";
        }
        
    }
    catch (Exception ex)
    {
        resp.IsFail = true;
        resp.FailMessgae = ex.Message;
    }
    
    return resp;
}

[Description("取得經位度位置的天氣")]
async Task<ToolResponse> get_currentweather(string lon, string lat)
{
    var resp = new ToolResponse();
    try
    {
        using var client = new HttpClient();
        string url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current_weather=true";
        string response = await client.GetStringAsync(url);

    }
    catch (Exception ex)
    {
        resp.IsFail = true;
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
    public bool IsFail { get; set; }
    public string FailMessgae { set; get; } = string.Empty;
    public string Data { set; get; } = string.Empty;
}

