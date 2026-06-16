using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace backend;

internal sealed class FrontendToolBridgeAgent : DelegatingAIAgent
{
    private const string StateBagKey = "agui_client_tools";

    public FrontendToolBridgeAgent(AIAgent innerAgent)
        : base(innerAgent) { }

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession session,
        AgentRunOptions options,
        CancellationToken cancellationToken = default)
    {
        StoreFrontendTools(session, options);

        // Sanitize history to eliminate downstream double-evaluation loops
        var sanitizedMessages = SanitizeMessageHistory(messages);

        var response = await InnerAgent.RunAsync(sanitizedMessages, session, options, cancellationToken).ConfigureAwait(false);

        // Filter out unwrapped frontend tool calls from response messages
        if (response.Messages != null)
        {
            foreach (var msg in response.Messages)
            {
                if (msg.Role == ChatRole.Assistant && msg.Contents != null)
                {
                    var filteredContents = msg.Contents.Where(c => 
                        c is not FunctionCallContent fcc || 
                        !(fcc.Name == "setThemeColor" || fcc.Name == "showLeaveForm") || 
                        fcc.CallId.Contains("_FunctionCall:") || fcc.CallId.Contains(":")
                    ).ToList();

                    msg.Contents = filteredContents;
                }
            }
        }

        return response;
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession session,
        AgentRunOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        StoreFrontendTools(session, options);

        // Sanitize history to eliminate downstream double-evaluation loops
        var sanitizedMessages = SanitizeMessageHistory(messages);

        await foreach (var update in InnerAgent.RunStreamingAsync(sanitizedMessages, session, options, cancellationToken).ConfigureAwait(false))
        {
            if (update.Contents != null && update.Contents.Count > 0)
            {
                var filteredContents = new List<AIContent>();
                foreach (var content in update.Contents)
                {
                    if (content is FunctionCallContent fcc)
                    {
                        bool isFrontendTool = fcc.Name == "setThemeColor" || fcc.Name == "showLeaveForm";
                        bool isWrapped = fcc.CallId.Contains("_FunctionCall:") || fcc.CallId.Contains(":");

                        if (isFrontendTool && !isWrapped)
                        {
                            Console.WriteLine($"[FrontendToolBridge] Filtering out unwrapped frontend tool call stream: {fcc.Name} ({fcc.CallId})");
                            continue;
                        }
                    }
                    filteredContents.Add(content);
                }

                if (filteredContents.Count == 0)
                {
                    continue;
                }

                update.Contents = filteredContents;
            }

            yield return update;
        }
    }

    private static void StoreFrontendTools(AgentSession? session, AgentRunOptions? options)
    {
        if (options is ChatClientAgentRunOptions chatRunOptions &&
            chatRunOptions.ChatOptions?.Tools is { Count: > 0 } incomingTools)
        {
            var clientTools = incomingTools.OfType<AITool>().ToList();
            if (clientTools.Count > 0)
            {
                session?.StateBag.SetValue(StateBagKey, (object)clientTools);
                FrontendToolContext.CurrentTools = clientTools;
                Console.WriteLine($"[FrontendToolBridge] Stored {clientTools.Count} frontend tool(s).");
            }
        }
    }

    /// <summary>
    /// Scans history and filters out duplicate/residual tool call allocations 
    /// so nested sub-agents don't mistake old tool buffers for current instructions.
    /// </summary>
    private static IEnumerable<ChatMessage> SanitizeMessageHistory(IEnumerable<ChatMessage> messages)
    {
        var messageList = messages.ToList();

        // 1. Sanitize any tool messages that have empty content to avoid orchestration loop
        foreach (var msg in messageList)
        {
            if (msg.Role == ChatRole.Tool)
            {
                bool isEmpty = string.IsNullOrWhiteSpace(msg.Text) &&
                               (msg.Contents == null ||
                                msg.Contents.Count == 0 ||
                                msg.Contents.All(c =>
                                    (c is TextContent tc && string.IsNullOrWhiteSpace(tc.Text)) ||
                                    (c is FunctionResultContent frc && (frc.Result == null || string.IsNullOrWhiteSpace(frc.Result.ToString())))
                                ));

                if (isEmpty)
                {
                    Console.WriteLine("[FrontendToolBridge] Detected tool message with empty content. Overwriting with 'Success' to prevent orchestration loop.");

                    var funcResult = msg.Contents?.OfType<FunctionResultContent>().FirstOrDefault();
                    if (funcResult != null)
                    {
                        var newFuncResult = new FunctionResultContent(funcResult.CallId, "Success");
                        msg.Contents = new List<AIContent> { newFuncResult };
                    }
                    else
                    {
                        msg.Contents = new List<AIContent> { new TextContent("Success") };
                    }
                }
            }
        }

        // 2. Align tool call IDs between FunctionCallContent (assistant) and FunctionResultContent (tool)
        var assistantCalls = messageList
            .Where(m => m.Role == ChatRole.Assistant && m.Contents != null)
            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .ToList();

        var assistantCallIds = assistantCalls.Select(f => f.CallId).ToList();

        foreach (var msg in messageList)
        {
            if (msg.Role == ChatRole.Tool && msg.Contents != null)
            {
                var newContents = new List<AIContent>();
                bool mutated = false;
                foreach (var content in msg.Contents)
                {
                    if (content is FunctionResultContent frc)
                    {
                        var matchingCallId = assistantCallIds.FirstOrDefault(id => 
                            id == frc.CallId || 
                            id.EndsWith(":" + frc.CallId, StringComparison.OrdinalIgnoreCase) ||
                            id.EndsWith("FunctionCall:" + frc.CallId, StringComparison.OrdinalIgnoreCase)
                        );

                        if (matchingCallId != null && matchingCallId != frc.CallId)
                        {
                            Console.WriteLine($"[FrontendToolBridge] Aligning mismatching tool call ID. Tool response ID '{frc.CallId}' aligned to wrapped ID '{matchingCallId}'");
                            newContents.Add(new FunctionResultContent(matchingCallId, frc.Result));
                            mutated = true;
                            continue;
                        }
                    }
                    newContents.Add(content);
                }

                if (mutated)
                {
                    msg.Contents = newContents;
                }
            }
        }

        return messageList;
    }
}