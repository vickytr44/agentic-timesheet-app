using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace backend;

public class ToolBridgeContextProvider : AIContextProvider
{
    protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        // 1. Create a concrete, modifiable list for our dynamic tools
        var dynamicTools = new List<AITool>();

        // 2. Safely extract client-side tools from the AsyncLocal FrontendToolContext first
        var currentTools = FrontendToolContext.CurrentTools;
        if (currentTools != null)
        {
            foreach (var tool in currentTools)
            {
                if (!dynamicTools.Any(t => t.Name == tool.Name))
                {
                    dynamicTools.Add(tool);
                }
            }
        }

        // 3. Instantiate the AIContext passing the populated list into its constructor
        // or assigning it directly to clear the collection manipulation block
        var aiContext = new AIContext
        {
            Tools = dynamicTools
        };

        return new ValueTask<AIContext>(aiContext);
    }
}
