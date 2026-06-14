using backend.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace backend;

public class TimesheetAgentFactory(
    IChatClient chatClient,
    TimesheetService timesheetService,
    HandbookService handbookService,
    LeaveService leaveService,
    JsonSerializerOptions jsonSerializerOptions)
{
    public AIAgent CreateTimesheetAgent()
    {
        // 1. Create the Timesheet Agent (Specialist)
        var timesheetTools = TimesheetAgentTools.CreateTools(timesheetService);
        var coreTimesheetAgent = new ChatClientAgent(
            chatClient: chatClient,
            name: "timesheet_agent",
            instructions: "You are an expert Timesheet Assistant. Your job is to help users manage, log, review, and submit their timesheets. " +
                         "Use your tools to query or mutate timesheet records. Always be friendly, concise, and helpful. " +
                         "If the user asks questions about company policies, employee benefits, remote work, leaves, or HR handbooks, " +
                         "you must hand off back to the triage_agent.",
            description: "Timesheet AI Assistant",
            tools: timesheetTools
        );

        // Wrap the Timesheet Agent with the state synchronization logic
        var timesheetAgent = new TimesheetSharedStateAgent(coreTimesheetAgent, timesheetService, jsonSerializerOptions);

        // 2. Create the Handbook Agent (Specialist)
        var handbookTools = new List<AITool>
        {
            AIFunctionFactory.Create(handbookService.SearchHandbookAsync)
        };
        var handbookAgent = new ChatClientAgent(
            chatClient: chatClient,
            name: "handbook_agent",
            instructions: "You are an expert Employee Handbook and HR Policy Assistant. " +
                          "Your job is to search the employee handbook to answer the user's policy questions. " +
                          "Always use your search tool to look up policies and answer clearly based ONLY on the retrieved facts. " +
                          "If the user asks to log hours, modify, submit, or unlock timesheets, " +
                          "you must hand off back to the triage_agent.",
            description: "Employee Handbook Policy Assistant",
            tools: handbookTools
        );

        // 3. Create the Leave Agent (Specialist)
        var leaveTools = new List<AITool>
        {
            AIFunctionFactory.Create(leaveService.GetLeaveBalances),
            AIFunctionFactory.Create(leaveService.GetLeaveRequests),
            AIFunctionFactory.Create(leaveService.ApplyLeave)
        };
        var leaveAgent = new ChatClientAgent(
            chatClient: chatClient,
            name: "leave_agent",
            instructions: "You are an expert Leave and Time Off Assistant. Your job is to help users check their leave balances and apply for leaves. " +
                          "Always refer to the user's available leave balances when requested. " +
                          "If the request is unrelated to leaves (e.g. they want to log hours, modify timesheets, search general handbook policies), " +
                          "you must hand off back to the triage_agent.",
            description: "Leave Management Assistant",
            tools: leaveTools
        );

        // 4. Create the Triage Agent (Coordinator)
        var triageAgent = new ChatClientAgent(
            chatClient: chatClient,
            name: "triage_agent",
            instructions: "You are the primary assistant coordinator. Your job is to route the user's request to the correct specialist.\n" +
                          "- If the user wants to apply for leave, submit a leave request, or check their personal leave balances/history, you MUST hand off to the leave_agent.\n" +
                          "- If the user wants to log hours, modify, submit, view, or unlock their timesheet, you MUST hand off to the timesheet_agent.\n" +
                          "- If the user asks general informational questions about company policies, employee benefits, remote work guidelines, leave rules/stipends, or HR handbooks, hand off to the handbook_agent.\n" +
                          "- If the request is generic (like hello), greet the user, explain what you can help with (timesheets, leaves, or handbook policies), and ask how you can assist.",
            description: "Routes users to the appropriate agent"
        );

        // 5. Build the workflow using the Handoff pattern with explicit reasons (descriptions for the LLM)
        var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)
            .WithHandoff(triageAgent, timesheetAgent, "Use this tool to transition to the Timesheet Agent when the user wants to perform actions on their timesheet (e.g. log hours, submit, unlock, view).")
            .WithHandoff(triageAgent, leaveAgent, "Use this tool ONLY when the user wants to take action on leaves (e.g., apply for leave, submit a leave request, check personal leave balances, view leave requests).")
            .WithHandoff(triageAgent, handbookAgent, "Use this tool ONLY when the user asks general questions, read-only policy lookups, or informational questions about the employee handbook, HR rules, remote work guidelines, or leave policies.")
            .Build();

        // 6. Return the workflow wrapped as a single AIAgent
        return workflow.AsAIAgent();
    }
}
