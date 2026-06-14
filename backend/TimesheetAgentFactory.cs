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
            AIFunctionFactory.Create(leaveService.ShowLeaveForm)
        };
        var leaveAgent = new ChatClientAgent(
            chatClient: chatClient,
            name: "leave_agent",
            instructions: "You are an expert Leave and Time Off Assistant. Your job is to help users check their leave balances and apply for leaves. " +
                          "If the user wants to apply for leave, you MUST invoke the frontend tool 'showLeaveForm' to open the leave request form for them. " +
                          "Pre-populate as many parameters (startDate, endDate, leaveType, reason) as possible from the user's input. " +
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
            instructions: "You are the primary assistant coordinator. Your job is to route the user's request. " +
                          "- For logging hours, timesheets, submitting, or viewing timesheet status, hand off to the timesheet_agent. " +
                          "- For checking personal leave balances, applying for leaves, or viewing leave requests/history, hand off to the leave_agent. " +
                          "- For general HR policies, employee handbooks, general leave policies, benefits, remote work, wellness stipend, or office hours, hand off to the handbook_agent. " +
                          "- If the request is generic (like hello), greet the user, explain what you can help with (timesheets, leaves, or handbook policies), and ask how you can assist.",
            description: "Routes users to the appropriate agent"
        );

        // 5. Build the workflow using the Handoff pattern with explicit reasons (descriptions for the LLM)
        var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)
            .WithHandoff(triageAgent, timesheetAgent, "Use this tool to transition the conversation to the Timesheet Agent when the user wants to log hours, view timesheets, submit, or unlock timesheets.")
            .WithHandoff(triageAgent, leaveAgent, "Use this tool to transition the conversation to the Leave Agent when the user wants to apply for leave, check their leave balances, or see their leave requests.")
            .WithHandoff(triageAgent, handbookAgent, "Use this tool to transition the conversation to the Handbook Agent when the user asks general questions about company policies, employee benefits, remote work, sick leaves guidelines, wellness stipend, or HR guidelines.")
            .WithHandoff(timesheetAgent, triageAgent, "Use this tool ONLY if the user's request is unrelated to timesheets (e.g. they ask about company policies, leaves, benefits, or employee handbook questions).")
            .WithHandoff(handbookAgent, triageAgent, "Use this tool ONLY if the user's request is unrelated to employee handbook/HR policies (e.g. they want to log hours, modify timesheets, submit, or unlock timesheets).")
            .WithHandoff(leaveAgent, triageAgent, "Use this tool ONLY if the user's request is unrelated to leaves (e.g. they want to log hours, modify timesheets, submit, or unlock timesheets, or search handbook policies).")
            .Build();

        // 6. Return the workflow wrapped as a single AIAgent
        return workflow.AsAIAgent();
    }
}
