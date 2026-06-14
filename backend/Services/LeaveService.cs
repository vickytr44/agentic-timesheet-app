using backend.Models;
using System.ComponentModel;

namespace backend.Services
{
    public class LeaveService
    {
        private readonly List<LeaveRequest> _requests = new();
        private readonly Dictionary<string, double> _balances = new();
        private readonly TimesheetService _timesheetService;

        public LeaveService(TimesheetService timesheetService)
        {
            _timesheetService = timesheetService;

            // Seed leave balances
            _balances["Vacation"] = 20.0;
            _balances["Sick"] = 10.0;
            _balances["Parental"] = 60.0; // 12 weeks = 60 working days

            // Seed historical request (e.g. 2 days vacation last month)
            var prevMonth = DateTime.Today.AddMonths(-1);
            var start = prevMonth.ToString("yyyy-MM-dd");
            var end = prevMonth.AddDays(1).ToString("yyyy-MM-dd");

            _requests.Add(new LeaveRequest
            {
                Id = Guid.NewGuid(),
                StartDate = start,
                EndDate = end,
                LeaveType = "Vacation",
                Reason = "Family trip",
                Status = "Approved",
                Days = 2.0
            });
            _balances["Vacation"] -= 2.0;
        }

        [Description("Gets the user's available leave balances for each leave type (Vacation, Sick, Parental).")]
        public Dictionary<string, double> GetLeaveBalances()
        {
            return new Dictionary<string, double>(_balances);
        }

        [Description("Gets the list of all applied leave requests.")]
        public List<LeaveRequest> GetLeaveRequests()
        {
            return _requests.OrderByDescending(r => r.StartDate).ToList();
        }

        [Description("Opens the leave application form on the frontend to allow the user to apply for leave. Call this when the user expresses intent to apply for leave or take time off.")]
        public string ShowLeaveForm(
            [Description("The start date of the leave (YYYY-MM-DD), if specified by the user.")] string startDate = "",
            [Description("The end date of the leave (YYYY-MM-DD), if specified by the user.")] string endDate = "",
            [Description("The type of leave (e.g. Vacation, Sick, Parental), if specified by the user.")] string leaveType = "",
            [Description("The reason or description for the leave, if specified by the user.")] string reason = "")
        {
            return "Leave form opened on frontend.";
        }

        [Description("Applies for a new leave request. Deducts balances and automatically populates the user's timesheet for those dates.")]
        public LeaveRequest ApplyLeave(
            [Description("Start date of leave in YYYY-MM-DD format.")] string startDate,
            [Description("End date of leave in YYYY-MM-DD format.")] string endDate,
            [Description("Leave type (e.g., Vacation, Sick, Parental).")] string leaveType,
            [Description("Reason or description for the leave.")] string reason)
        {
            if (!DateTime.TryParse(startDate, out var start) || !DateTime.TryParse(endDate, out var end))
            {
                throw new ArgumentException("Invalid date format. Please use YYYY-MM-DD.");
            }

            if (end < start)
            {
                throw new ArgumentException("End date cannot be before start date.");
            }

            // Calculate working days (weekdays)
            double days = 0;
            var weekdays = new List<DateTime>();
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                {
                    days += 1.0;
                    weekdays.Add(date);
                }
            }

            if (days == 0)
            {
                throw new ArgumentException("Leave duration must contain at least one weekday (Monday to Friday).");
            }

            // Normalize leave type name to title case
            string normalizedType = char.ToUpper(leaveType[0]) + leaveType.Substring(1).ToLower();

            if (!_balances.TryGetValue(normalizedType, out var balance))
            {
                throw new ArgumentException($"Unknown leave type: {leaveType}. Available types are: Vacation, Sick, Parental.");
            }

            if (balance < days)
            {
                throw new InvalidOperationException($"Insufficient leave balance. Remaining {normalizedType} balance: {balance} days. Requested: {days} days.");
            }

            // Deduct balance
            _balances[normalizedType] = balance - days;

            var request = new LeaveRequest
            {
                Id = Guid.NewGuid(),
                StartDate = startDate,
                EndDate = endDate,
                LeaveType = normalizedType,
                Reason = reason,
                Status = "Approved", // Auto-approved for this copilot demonstration
                Days = days
            };

            _requests.Add(request);

            // Automatically populate timesheet entries for each weekday of the leave
            foreach (var date in weekdays)
            {
                try
                {
                    _timesheetService.AddTimesheetEntry(
                        date: date.ToString("yyyy-MM-dd"),
                        project: "Leave",
                        hours: 8.0,
                        description: $"{normalizedType} Leave: {reason}"
                    );
                }
                catch (Exception ex)
                {
                    // If timesheet is locked/submitted, we output to diagnostic console but don't crash the leave application
                    Console.WriteLine($"--> Auto-timesheet population skipped for {date:yyyy-MM-dd}: {ex.Message}");
                }
            }

            return request;
        }
    }
}
