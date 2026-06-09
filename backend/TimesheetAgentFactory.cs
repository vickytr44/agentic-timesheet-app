using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using TimesheetCopilotApp.Backend.Services;

namespace TimesheetCopilotApp.Backend;

public class TimesheetAgentFactory(
    IChatClient chatClient,
    TimesheetService timesheetService,
    JsonSerializerOptions jsonSerializerOptions)
{
    public AIAgent CreateTimesheetAgent()
    {
        var tools = TimesheetAgentTools.CreateTools(timesheetService);

        var chatClientAgent = new ChatClientAgent(
            chatClient: chatClient,
            name: "my_agent",
            instructions: "You are an expert Timesheet Assistant. Your job is to help users manage, log, review, and submit their timesheets. " +
                         "Use your tools to query or mutate timesheet records. Always be friendly, concise, and helpful.",
            description: "Timesheet AI Assistant",
            tools: tools
        );

        return new TimesheetSharedStateAgent(chatClientAgent, timesheetService, jsonSerializerOptions);
    }
}
