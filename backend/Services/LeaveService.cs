using backend.Models;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace backend.Services
{
    public class LeaveService : ILeaveService
    {
        private readonly List<LeaveRequest> _requests = [];
        private readonly Dictionary<LeaveTypeEnum, double> _balances = [];
        private readonly ITimesheetService _timesheetService;
        private readonly object _lock = new();
        private readonly ILogger<LeaveService> _logger;

        public LeaveService(ITimesheetService timesheetService, ILogger<LeaveService> logger)
        {
            _timesheetService = timesheetService;
            _logger = logger;

            // Seed leave balances
            _balances[LeaveTypeEnum.Vacation] = 20.0;
            _balances[LeaveTypeEnum.Sick] = 10.0;
            _balances[LeaveTypeEnum.Parental] = 60.0; // 12 weeks = 60 working days

            // Seed historical request (e.g. 2 days parental leave last month)
            var prevMonth = DateTime.Today.AddMonths(-1);
            var start = DateOnly.FromDateTime(prevMonth);
            var end = DateOnly.FromDateTime(prevMonth.AddDays(1));

            var historicalRequest = new LeaveRequest
            {
                Id = Guid.NewGuid(),
                StartDate = start,
                EndDate = end,
                LeaveType = LeaveTypeEnum.Parental,
                Reason = "Family trip",
                Status = LeaveStatusEnum.Approved,
                Days = 2.0
            };

            _requests.Add(historicalRequest);
            _balances[LeaveTypeEnum.Parental] -= 2.0;
        }

        [Description("Gets the user's available leave balances for each leave type (Vacation, Sick, Parental).")]
        public Dictionary<LeaveTypeEnum, double> GetLeaveBalances()
        {
            lock (_lock)
            {
                return new Dictionary<LeaveTypeEnum, double>(_balances);
            }
        }

        [Description("Gets the list of all applied leave requests.")]
        public List<LeaveRequest> GetLeaveRequests()
        {
            lock (_lock)
            {
                return _requests.OrderByDescending(r => r.StartDate).ToList();
            }
        }

        [Description("Applies for a new leave request. Deducts balances and automatically populates the user's timesheet for those dates.")]
        public LeaveRequest ApplyLeave(
            [Description("Start date of leave in YYYY-MM-DD format.")] DateOnly startDate,
            [Description("End date of leave in YYYY-MM-DD format.")] DateOnly endDate,
            [Description("Leave type (e.g., Vacation, Sick, Parental).")] LeaveTypeEnum leaveType,
            [Description("Reason or description for the leave.")] string reason)
        {
            lock (_lock)
            {
                // Validate dates (DateOnly parameters)
                if (endDate < startDate)
                {
                    throw new ArgumentException("End date cannot be before start date.");
                }

                // Calculate working days (weekdays)
                var weekdays = new List<DateOnly>();
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                    {
                        weekdays.Add(date);
                    }
                }

                var days = weekdays.Count;

                if (days == 0)
                {
                    throw new ArgumentException("Leave duration must contain at least one weekday (Monday to Friday).");
                }

                if (!_balances.TryGetValue(leaveType, out var balance))
                {
                    throw new ArgumentException($"Unknown leave type: {leaveType}. Available types are: Vacation, Sick, Parental.");
                }

                if (balance < days)
                {
                    throw new InvalidOperationException($"Insufficient leave balance. Remaining {leaveType} balance: {balance} days. Requested: {days} days.");
                }

                // Deduct balance
                _balances[leaveType] = balance - days;

                var request = new LeaveRequest
                {
                    Id = Guid.NewGuid(),
                    StartDate = startDate,
                    EndDate = endDate,
                    LeaveType = leaveType,
                    Reason = reason,
                    Status = LeaveStatusEnum.Approved, // Auto-approved for this copilot demonstration
                    Days = days
                };

                _requests.Add(request);
                _logger.LogInformation("Leave request applied: {Id} of type: {LeaveType} for {Days} days", request.Id, request.LeaveType, request.Days);

                // Automatically populate timesheet entries for each weekday of the leave
                foreach (var date in weekdays)
                {
                    try
                    {
                        _timesheetService.AddTimesheetEntry(
                            date: date.ToString("yyyy-MM-dd"),
                            project: "Leave",
                            hours: 8.0,
                            description: $"{leaveType} Leave: {reason}"
                        );
                    }
                    catch (Exception ex)
                    {
                        // If timesheet is locked/submitted, we output to diagnostic logger but don't crash the leave application
                        _logger.LogWarning(ex, "Auto-timesheet population skipped for {Date}", date.ToString("yyyy-MM-dd"));
                    }
                }

                return request;
            }
        }
    }
}
