namespace backend.Models;

/// <summary>
/// Input DTO for the POST /api/leave/apply REST endpoint.
/// </summary>
public record LeaveRequestInput(DateOnly StartDate, DateOnly EndDate, LeaveTypeEnum LeaveType, string Reason);
