using backend.Services;
using Microsoft.Extensions.AI;

namespace backend;

/// <summary>
/// Static tool definitions for the Timesheet Agent, following the AgentTools pattern.
/// Each tool wraps a method on TimesheetService.
/// </summary>
public static class TimesheetAgentTools
{
    /// <summary>
    /// Creates the full list of AI tools bound to the given TimesheetService instance.
    /// </summary>
    public static IList<AITool> CreateTools(TimesheetService service)
    {
        return
        [
            AIFunctionFactory.Create(service.GetTimesheet),
            AIFunctionFactory.Create(service.AddTimesheetEntry),
            AIFunctionFactory.Create(service.RemoveTimesheetEntry),
            AIFunctionFactory.Create(service.ClearTimesheet),
            AIFunctionFactory.Create(service.SubmitTimesheet),
            AIFunctionFactory.Create(service.UnlockTimesheet),
            AIFunctionFactory.Create(service.GetStatus),
            AIFunctionFactory.Create(service.GetSummary)
        ];
    }

    /// <summary>
    /// Creates the full list of AI tools bound to the given LeaveService instance.
    /// </summary>
    public static IList<AITool> CreateLeaveTools(LeaveService service)
    {
        return
        [
            AIFunctionFactory.Create(service.GetLeaveBalances),
            AIFunctionFactory.Create(service.GetLeaveRequests),
            //AIFunctionFactory.Create(service.ApplyLeave)
        ];
    }

    /// <summary>
    /// Creates the full list of AI tools bound to the given HandbookService instance.
    /// </summary>
    public static IList<AITool> CreateHandbookTools(HandbookService service)
    {
        return
        [
            AIFunctionFactory.Create(service.SearchHandbookAsync)
        ];
    }
}
