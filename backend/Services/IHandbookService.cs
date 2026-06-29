using System.ComponentModel;

namespace backend.Services;

/// <summary>
/// Defines the contract for employee handbook search operations.
/// </summary>
public interface IHandbookService
{
    [Description("Searches the employee handbook PDF for policies, rules, and guidelines on " +
                 "leaves, vacation, work hours, wellness stipend, and timesheet submissions.")]
    Task<string> SearchHandbookAsync(
        [Description("The search query detailing what company policy or guideline to look up")]
        string query);
}
