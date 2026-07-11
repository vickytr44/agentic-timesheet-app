using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace backend
{
    public class DummyToolWrapper
    {
        public void Test(AIAgent agent)
        {
            var tool = AIFunctionFactory.Create(
                async (string query, CancellationToken cancellationToken) =>
                {
                    var response = await agent.RunAsync(query, cancellationToken: cancellationToken);
                    return response.Text ?? "No response";
                },
                "call_flint_agent",
                "Call the flint A2A agent to generate and plot charts based on the provided query."
            );
        }
    }
}
