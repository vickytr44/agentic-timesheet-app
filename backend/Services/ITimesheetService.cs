using backend.Models;
using System.ComponentModel;

namespace backend.Services;

/// <summary>
/// Defines the contract for timesheet management operations.
/// Description attributes are required for AI tool function binding via AIFunctionFactory.
/// </summary>
public interface ITimesheetService
{
    [Description("Gets the complete list of logged timesheet entries.")]
    List<TimesheetEntry> GetTimesheet();

    [Description("Adds a new timesheet entry. Returns the newly created entry.")]
    TimesheetEntry AddTimesheetEntry(
        [Description("The date of the work in YYYY-MM-DD format (e.g. 2026-05-23).")] string date,
        [Description("The project name, e.g. 'Project Antigravity' or 'Admin & Training'.")] string project,
        [Description("The number of hours worked (e.g. 7.5 or 8).")] double hours,
        [Description("A brief description of what task was completed.")] string description);

    [Description("Removes a specific timesheet entry using its unique identifier (GUID).")]
    bool RemoveTimesheetEntry(
        [Description("The unique identifier (GUID string) of the timesheet entry to delete.")] string id);

    [Description("Clears all timesheet entries in the current timesheet.")]
    bool ClearTimesheet();

    [Description("Submits the current timesheet, marking its status as Submitted. This locks the timesheet from further modifications.")]
    string SubmitTimesheet();

    [Description("Unlocks a submitted timesheet, changing its status back to 'Draft' so it can be edited again.")]
    string UnlockTimesheet();

    [Description("Gets the current submission status of the timesheet (either 'Draft' or 'Submitted').")]
    string GetStatus();

    [Description("Gets the summary metrics: TotalHours, TotalEntries, Status, and ProjectCount.")]
    Dictionary<string, object> GetSummary();

    /// <summary>
    /// Synchronizes the local timesheet state with an incoming frontend state snapshot.
    /// Not exposed as an AI tool — used internally by the state synchronization agent.
    /// </summary>
    void SyncState(List<TimesheetEntry> entries, string status);
}
