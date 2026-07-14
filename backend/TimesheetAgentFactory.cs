using A2A;
using backend.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text.Json;

namespace backend;

public class TimesheetAgentFactory(
    IChatClient chatClient,
    ITimesheetService timesheetService,
    IHandbookService handbookService,
    ILeaveService leaveService,
    JsonSerializerOptions jsonSerializerOptions,
    ILogger<TimesheetAgentFactory> logger)
{
    private readonly ILogger<TimesheetAgentFactory> _logger = logger;

    public async Task<AIAgent> CreateTimesheetAgent()
    {
        var currentDateStr = DateTime.Today.ToString("yyyy-MM-dd");
        var currentDayOfWeekStr = DateTime.Today.DayOfWeek.ToString();

        // 1. Create the Timesheet Agent (Specialist)
        var timesheetTools = TimesheetAgentTools.CreateTools(timesheetService);

        var coreTimesheetAgent = new ChatClientAgent(
            chatClient: chatClient,
            options: new ChatClientAgentOptions
            {
                Name = "timesheet_agent",
                Description = "Timesheet AI Assistant",
                ChatOptions = new()
                {
                    Instructions = $"Today's date is {currentDateStr} ({currentDayOfWeekStr}). " +
                          "You are an expert Timesheet Assistant. Your job is to help users manage, log, review, and submit their timesheets. " +
                          "Use your tools to query or mutate timesheet records. Always be friendly, concise, and helpful. " +
                          "If the user asks to generate or visualize a chart based on the timesheet data you've retrieved, hand off to the flint_agent. " +
                          "If the request is completely unrelated to timesheets or charting, let the user know and suggest they start a new request.",
                    Tools = timesheetTools
                }
            }
            );

        var middlewareEnabledTimeSheetAgent = coreTimesheetAgent.AsBuilder().Use(CustomFunctionCallingMiddleware).Build();

        // Wrap the Timesheet Agent with the state synchronization logic
        var timesheetAgent = new TimesheetSharedStateAgent(middlewareEnabledTimeSheetAgent, timesheetService, jsonSerializerOptions, _logger);

        // 2. Create the Handbook Agent (Specialist)
        var handbookAgent = new ChatClientAgent(
            chatClient: chatClient,
            name: "handbook_agent",
            instructions: $"Today's date is {currentDateStr} ({currentDayOfWeekStr}). " +
                          "You are an expert Employee Handbook and HR Policy Assistant. " +
                          "Your job is to search the employee handbook to answer the user's policy questions. " +
                          "Always use your search tool to look up policies and answer clearly based ONLY on the retrieved facts. " +
                          "If the user asks to generate or visualize a chart based on the data you've retrieved, hand off to the flint_agent. " +
                          "If the request is completely unrelated to handbook policies or charting, let the user know and suggest they start a new request.",
            description: "Employee Handbook Policy Assistant",
            tools: TimesheetAgentTools.CreateHandbookTools(handbookService)
        );

        var middlewareEnabledHandbookAgent = handbookAgent.AsBuilder().Use(CustomFunctionCallingMiddleware).Build();

        // 3. Create the Leave Agent (Specialist)
        var leaveAgent = new ChatClientAgent(
            chatClient: chatClient,
            name: "leave_agent",
            instructions: $"Today's date is {currentDateStr} ({currentDayOfWeekStr}). " +
                          "You are an expert Leave and Time Off Assistant. Your job is to help users check their leave balances and apply for leaves. " +
                          "Always refer to the user's available leave balances when requested. " +
                          "When the user requests N days of leave starting on a specific date, you MUST calculate the end date such that the number of weekdays (excluding Saturdays and Sundays) from the start date to the end date (inclusive) is exactly N. " +
                          "The start date itself counts as day 1. " +
                          "For example: " +
                          "- A 3-day leave starting on a Monday (like 2026-06-22) covers Monday, Tuesday, Wednesday, so the end date MUST be Wednesday 2026-06-24 (not June 25). " +
                          "- A 3-day leave starting on a Friday (like 2026-06-19) covers Friday, Monday, Tuesday, so the end date MUST be Tuesday 2026-06-23. " +
                          "If the user asks to generate or visualize a chart based on the leave data you've retrieved, hand off to the flint_agent. " +
                          "If the request is completely unrelated to leaves or charting, let the user know and suggest they start a new request.",
            description: "Leave Management Assistant",
            tools: TimesheetAgentTools.CreateLeaveTools(leaveService)
        );

        var middlewareEnabledLeaveAgent = leaveAgent.AsBuilder().Use(CustomFunctionCallingMiddleware).Build();

        // 4. Resolve the A2A Flint agent
        var path = "/.well-known/agent-card.json";
        A2ACardResolver agentCardResolver = new A2ACardResolver(new Uri("http://localhost:5000"), new HttpClient(), agentCardPath: path, logger: _logger);

        AIAgent agent = await agentCardResolver.GetAIAgentAsync();

        var flintAgent = new ChatClientAgent(
            chatClient: chatClient,
            name: "flint_agent",
            instructions: $"Today's date is {currentDateStr} ({currentDayOfWeekStr}). " +
                          "You are the Flint Chart Assistant. Your job is to process user requests for charting and visualization. " +
                          "Always use your tool to call the specialized A2A charting agent and provide the user with the result. " +
                          "Do NOT output any markdown image tags, placeholders, or image links (e.g. `![Chart]()`) in your text response. " +
                          "If the request is unrelated to charts, let the user know and suggest they start a new request.",
            description: "Chart Generation and Visualization Assistant",
            tools: [
                AIFunctionFactory.Create(
                    async (
                        [Description("The charting or visualization request (e.g. 'Create a chart for this data').")] string query,
                        [Description("The dataset to visualize. Locate the complete dataset corresponding to the query (including all data rows/points and column headers) and provide it here.")] string dataset,
                        CancellationToken cancellationToken) =>
                    {
                        // Combine query and dataset for the backend A2A agent
                        string combinedQuery = $"User Request: {query}\n\nDataset:\n{dataset}";
                        
                        var innerFunction = agent.AsAIFunction();
                        var arguments = new AIFunctionArguments { ["query"] = combinedQuery };
                        var result = await innerFunction.InvokeAsync(arguments, cancellationToken);
                        return result?.ToString() ?? string.Empty;
                    },
                    name: "FlintAgent",
                    description: "Call the specialized A2A charting agent to generate and plot charts."
                )
            ]
        );

        var middlewareEnabledFlintAgent = flintAgent.AsBuilder().Use(CustomFunctionCallingMiddleware).Build();

        // 5. Create the Triage Agent (Coordinator)
        var triageAgent = new ChatClientAgent(
            chatClient: chatClient,
            options: new ChatClientAgentOptions
            {
                Name = "triage_agent",
                Description = "Routes users to the appropriate agent",
                ChatOptions = new() {
                    Instructions = $"Today's date is {currentDateStr} ({currentDayOfWeekStr}). " +
                          "You are the primary assistant coordinator. Your job is to route the user's request to the correct specialist.\n\n" +
                          "CRITICAL: Before routing or calling any handoff tools, check the conversation history:\n" +
                          "- If the history shows that the frontend tool `showLeaveForm` has already been called and completed (returned a result like 'Success' or 'Cancelled') for the current request, DO NOT hand off to the leave_agent. Instead, directly respond to the user: if 'Success', say that their leave request has been submitted successfully; if 'Cancelled', say that the request was cancelled.\n" +
                          "- If a handoff tool was already called and executed for the current request, do not call it again unless a new user request has been made.\n\n" +
                          "MULTI-STEP REQUEST HANDLING (HIGHEST PRIORITY):\n" +
                          "Before applying routing rules, first determine if the user's request requires multiple specialists to fulfill.\n" +
                          "If a request needs data that hasn't been fetched yet AND processing of that data (e.g., charting, visualizing), follow this strategy:\n" +
                          "Hand off to the data-providing agent (timesheet_agent, leave_agent, or handbook_agent) to retrieve the required data.\n" +
                          "The specialist agent will automatically forward to flint_agent for charting once the data is ready.\n" +
                          "Do NOT hand off directly to flint_agent if the required data has not been fetched yet.\n\n" +
                          "Routing rules (apply AFTER checking for multi-step requests):\n" +
                          "- If the user wants to apply for leave, submit a leave request, or check their personal leave balances/history, hand off to the leave_agent.\n" +
                          "- If the user wants to log hours, modify, submit, view, or unlock their timesheet, hand off to the timesheet_agent.\n" +
                          "- If the user asks general informational questions about company policies, employee benefits, remote work guidelines, leave rules/stipends, or HR handbooks, hand off to the handbook_agent.\n" +
                          "- If the user wants to generate or visualize a chart and the required data is already available in the conversation, hand off to the flint_agent.\n" +
                          "- If the request is generic (like hello), greet the user, explain what you can help with (timesheets, leaves, or handbook policies), and ask how you can assist."
                },
                AIContextProviders = [
                    new ToolBridgeContextProvider()
                ],
            }
            );

        var middlewareEnabledTriageAgent = triageAgent.AsBuilder().Use(CustomFunctionCallingMiddleware).Build();

        // 6. Build the workflow using the Handoff pattern with explicit reasons (descriptions for the LLM)
        var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(middlewareEnabledTriageAgent)
            .WithHandoffs(middlewareEnabledTriageAgent, [middlewareEnabledTimeSheetAgent, middlewareEnabledLeaveAgent, middlewareEnabledHandbookAgent, middlewareEnabledFlintAgent])
            .WithHandoffs([middlewareEnabledTimeSheetAgent, middlewareEnabledLeaveAgent, middlewareEnabledHandbookAgent], middlewareEnabledFlintAgent)
            .Build();

        // 6. Return the workflow wrapped with the FrontendToolBridge
        //    so that frontend tools from the AG-UI adapter are captured
        //    BEFORE the handoff workflow replaces ChatOptions.Tools.
        return new FrontendToolBridgeAgent(workflow.AsAIAgent(), _logger);
    }

    private async ValueTask<object?> CustomFunctionCallingMiddleware(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Function Name: {FunctionName} invoked by agent name: {AgentName}", context.Function.Name, agent.Name);
        var result = await next(context, cancellationToken);
        _logger.LogInformation("Function Call Result: {Result}", result);

        return result;
    }
}
