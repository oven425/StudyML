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
using System.Text.RegularExpressions;
using static LLama.Common.ChatHistory;
using static System.Runtime.InteropServices.JavaScript.JSType;


Console.WriteLine("Hello, World!");
string m_ModelPath = @"..\..\..\..\gguf_gemma4\gemma-4-E2B-it-Q4_K_M.gguf";

var tools = new AIFunction[]
{
    AIFunctionFactory.Create(get_datetime)
};
var ssss = tools[0].JsonSchema.ToString().Replace("\"", "<|\"|>");
var option = new ChatOptions()
{
    Instructions = "你是個Windows助理,所有回答要有禮貌以及使用繁體中文",
    Tools =
        [
            AIFunctionFactory.Create(get_datetime)
        ]
};

var gemma4client = new Gemma4ChatClient(m_ModelPath);
var funcclient = gemma4client.AsBuilder().UseFunctionInvocation().Build();
var aaresp = await funcclient.GetResponseAsync("現在幾點?", option);

//var res = await gemma4client.GetResponseAsync("", option);
var agent = funcclient.AsAIAgent(new ChatClientAgentOptions()
{
    Name ="assiant",
    ChatOptions = option
});

var resp_agent = await agent.RunAsync("現在幾點?");

[Description("取得現在的時間")]
string get_datetime()
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

