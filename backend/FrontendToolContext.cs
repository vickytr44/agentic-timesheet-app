using Microsoft.Extensions.AI;

namespace backend;

/// <summary>
/// Ambient storage for frontend (CopilotKit) tools that need to flow
/// from the outermost AG-UI adapter layer down through the handoff workflow
/// into each sub-agent's ToolBridgeContextProvider.
///
/// Uses AsyncLocal so the tools are available to any code running within
/// the same async request, regardless of session boundaries.
/// </summary>
public static class FrontendToolContext
{
    private static readonly AsyncLocal<List<AITool>?> _tools = new();

    /// <summary>
    /// Gets or sets the frontend tools for the current async execution context.
    /// </summary>
    public static List<AITool>? CurrentTools
    {
        get => _tools.Value;
        set => _tools.Value = value;
    }
}
