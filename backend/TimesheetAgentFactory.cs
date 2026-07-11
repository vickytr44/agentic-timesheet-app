using A2A;
using backend.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
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
                          "If the user asks questions about company policies, employee benefits, remote work, leaves, or HR handbooks, " +
                          "you must hand off back to the triage_agent.",
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
                          "If the user asks to log hours, modify, submit, or unlock timesheets, " +
                          "you must hand off back to the triage_agent.",
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
                          "If the request is unrelated to leaves (e.g. they want to log hours, modify timesheets, search general handbook policies), " +
                          "you must hand off back to the triage_agent.",
            description: "Leave Management Assistant",
            tools: TimesheetAgentTools.CreateLeaveTools(leaveService)
        );

        var middlewareEnabledLeaveAgent = leaveAgent.AsBuilder().Use(CustomFunctionCallingMiddleware).Build();

        // 4. Resolve the A2A Flint agent
        var path = "/.well-known/agent-card.json";
        A2ACardResolver agentCardResolver = new A2ACardResolver(new Uri("http://localhost:5000"), new HttpClient(), agentCardPath: path, logger: _logger);

        AIAgent agent = await agentCardResolver.GetAIAgentAsync();

        var middlewareEnabledFlintAgent = agent.AsBuilder().Use(CustomFunctionCallingMiddleware).Build();

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
                          "Routing rules:\n" +
                          "- If the user wants to apply for leave, submit a leave request, or check their personal leave balances/history, you MUST hand off to the leave_agent.\n" +
                          "- If the user wants to log hours, modify, submit, view, or unlock their timesheet, you MUST hand off to the timesheet_agent.\n" +
                          "- If the user asks general informational questions about company policies, employee benefits, remote work guidelines, leave rules/stipends, or HR handbooks, hand off to the handbook_agent.\n" +
                          "- If the user wants to generate, construct, or visualize a chart (like a bar chart, line chart, pie chart, scatter plot, or histogram) from data, you MUST hand off to the flint-agent.\n" +
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
            .WithHandoffs([middlewareEnabledTimeSheetAgent, middlewareEnabledLeaveAgent, middlewareEnabledHandbookAgent, middlewareEnabledFlintAgent], middlewareEnabledTriageAgent)
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
