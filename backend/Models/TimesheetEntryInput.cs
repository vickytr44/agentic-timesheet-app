namespace backend.Models;

/// <summary>
/// Input DTO for the POST /api/timesheet/entry REST endpoint.
/// </summary>
public record TimesheetEntryInput(string Date, string Project, double Hours, string Description);
