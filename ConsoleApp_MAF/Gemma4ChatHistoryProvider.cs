using Microsoft.Agents.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp_MAF
{
    public class Gemma4ChatHistoryProvider: ChatHistoryProvider
    {
        protected override ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken cancellationToken = default)
        {
            return base.StoreChatHistoryAsync(context, cancellationToken);
        }
    }
}
