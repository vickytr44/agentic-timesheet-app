using backend.Models;
using backend.Services;

namespace backend.Endpoints;

/// <summary>
/// Leave management REST API endpoint definitions using Minimal API route groups.
/// No try/catch needed — <see cref="Middleware.GlobalExceptionHandlerMiddleware"/> handles errors.
/// </summary>
public static class LeaveEndpoints
{
    public static WebApplication MapLeaveEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/leave");

        group.MapGet("/", (ILeaveService service) =>
            Results.Ok(service.GetLeaveRequests()));

        group.MapGet("/balances", (ILeaveService service) =>
            Results.Ok(service.GetLeaveBalances()));

        group.MapPost("/apply", (ILeaveService service, LeaveRequestInput input) =>
            Results.Ok(service.ApplyLeave(input.StartDate, input.EndDate, input.LeaveType, input.Reason)));

        return app;
    }
}
