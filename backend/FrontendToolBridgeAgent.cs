using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace backend;

/// <summary>
/// A DelegatingAIAgent wrapper that intercepts the initial AG-UI request,
/// extracts frontend tools from ChatClientAgentRunOptions.ChatOptions.Tools,
/// and stores them in the AgentSession.StateBag so that downstream agents
/// (via ToolBridgeContextProvider) can use them.
///
/// This must wrap the outermost workflow agent so it sees the original
/// ChatOptions before the handoff workflow replaces them with handoff tools.
/// </summary>
internal sealed class FrontendToolBridgeAgent : DelegatingAIAgent
{
    private const string StateBagKey = "agui_client_tools";

    public FrontendToolBridgeAgent(AIAgent innerAgent)
        : base(innerAgent) { }

    protected override Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession session,
        AgentRunOptions options,
        CancellationToken cancellationToken = default)
    {
        StoreFrontendTools(session, options);
        return InnerAgent.RunAsync(messages, session, options, cancellationToken);
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession session,
        AgentRunOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        StoreFrontendTools(session, options);

        await foreach (var update in InnerAgent.RunStreamingAsync(messages, session, options, cancellationToken).ConfigureAwait(false))
        {
            yield return update;
        }
    }

    /// <summary>
    /// Extracts frontend (client-side) tools from the AG-UI ChatOptions.Tools
    /// and persists them in the session StateBag. The ToolBridgeContextProvider
    /// reads from this key to inject the tools into each sub-agent.
    /// </summary>
    private static void StoreFrontendTools(AgentSession? session, AgentRunOptions? options)
    {
        if (options is ChatClientAgentRunOptions chatRunOptions &&
            chatRunOptions.ChatOptions?.Tools is { Count: > 0 } incomingTools)
        {
            // Only keep actual client tools (skip any that are handoff tools, etc.)
            var clientTools = incomingTools.OfType<AITool>().ToList();
            if (clientTools.Count > 0)
            {
                session?.StateBag.SetValue(StateBagKey, (object)clientTools);
                FrontendToolContext.CurrentTools = clientTools;
                Console.WriteLine($"[FrontendToolBridge] Stored {clientTools.Count} frontend tool(s) in StateBag and FrontendToolContext: " +
                    $"{string.Join(", ", clientTools.Select(t => t.Name))}");
            }
        }
    }
}
