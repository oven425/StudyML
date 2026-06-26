// See https://aka.ms/new-console-template for more information
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.ComponentModel;
//Microsoft.Agents.AI.AgentFileSkill;
Console.WriteLine("Hello, World!");

var aa = new ccc();
var agent = aa.AsAIAgent(new ChatClientAgentOptions()
{
    Name ="assiant",
    ChatOptions = new ChatOptions()
    {
        Instructions = "111",
        Tools = 
        [
            AIFunctionFactory.Create(QueryFolder)
        ]
    }
});
var session = await agent.CreateSessionAsync("");

[Description("讀取資料夾的內容")]
string QueryFolder(string folder)
{
    if(Directory.Exists(folder))
    {
        var ffs = Directory.GetFiles(folder);
    }

    return "not find";
}

public class ccc : IChatClient
{
    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}