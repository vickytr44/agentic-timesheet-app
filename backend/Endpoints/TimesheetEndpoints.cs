using backend.Models;
using backend.Services;

namespace backend.Endpoints;

/// <summary>
/// Timesheet REST API endpoint definitions using Minimal API route groups.
/// No try/catch needed — <see cref="Middleware.GlobalExceptionHandlerMiddleware"/> handles errors.
/// </summary>
public static class TimesheetEndpoints
{
    public static WebApplication MapTimesheetEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/timesheet");

        group.MapGet("/", (ITimesheetService service) =>
            Results.Ok(service.GetTimesheet()));

        group.MapGet("/summary", (ITimesheetService service) =>
            Results.Ok(service.GetSummary()));

        group.MapPost("/entry", (ITimesheetService service, TimesheetEntryInput input) =>
            Results.Ok(service.AddTimesheetEntry(input.Date, input.Project, input.Hours, input.Description)));

        group.MapDelete("/entry/{id}", (ITimesheetService service, string id) =>
        {
            var removed = service.RemoveTimesheetEntry(id);
            return removed
                ? Results.Ok(new { success = true })
                : Results.NotFound(new { error = "Entry not found" });
        });

        group.MapPost("/submit", (ITimesheetService service) =>
            Results.Ok(new { message = service.SubmitTimesheet() }));

        group.MapPost("/unlock", (ITimesheetService service) =>
            Results.Ok(new { message = service.UnlockTimesheet() }));

        return app;
    }
}
