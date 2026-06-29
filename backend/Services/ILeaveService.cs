using backend.Models;
using System.ComponentModel;

namespace backend.Services;

/// <summary>
/// Defines the contract for leave management operations.
/// </summary>
public interface ILeaveService
{
    [Description("Gets the user's available leave balances for each leave type (Vacation, Sick, Parental).")]
    Dictionary<LeaveTypeEnum, double> GetLeaveBalances();

    [Description("Gets the list of all applied leave requests.")]
    List<LeaveRequest> GetLeaveRequests();

    [Description("Applies for a new leave request. Deducts balances and automatically populates the user's timesheet for those dates.")]
    LeaveRequest ApplyLeave(
        [Description("Start date of leave in YYYY-MM-DD format.")] DateOnly startDate,
        [Description("End date of leave in YYYY-MM-DD format.")] DateOnly endDate,
        [Description("Leave type (e.g., Vacation, Sick, Parental).")] LeaveTypeEnum leaveType,
        [Description("Reason or description for the leave.")] string reason);
}
