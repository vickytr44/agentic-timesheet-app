using System;
using System.Collections.Generic;
using System.Linq;
using backend.Models;
using System.ComponentModel;

namespace backend.Services
{
    public class LeaveService
    {
        private readonly List<LeaveRequest> _requests = [];
        private readonly Dictionary<LeaveTypeEnum, double> _balances = [];
        private readonly TimesheetService _timesheetService;

        public LeaveService(TimesheetService timesheetService)
        {
            _timesheetService = timesheetService;

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
            return new Dictionary<LeaveTypeEnum, double>(_balances);
        }

        [Description("Gets the list of all applied leave requests.")]
        public List<LeaveRequest> GetLeaveRequests()
        {
            return _requests.OrderByDescending(r => r.StartDate).ToList();
        }

        [Description("Opens the leave application form. Use this when the user requests to apply for leave or take time off.")]
        public string showLeaveForm(
            [Description("The start date of the leave (YYYY-MM-DD), if known.")] string startDate = null,
            [Description("The end date of the leave (YYYY-MM-DD), if known.")] string endDate = null,
            [Description("The type of leave (e.g. Vacation, Sick, Parental), if known.")] string leaveType = null,
            [Description("The reason or notes for the leave, if known.")] string reason = null)
        {
            Console.WriteLine("--> showLeaveForm dummy method called on backend!");
            return "Form opened";
        }

        [Description("Applies for a new leave request. Deducts balances and automatically populates the user's timesheet for those dates.")]
        public LeaveRequest ApplyLeave(
            [Description("Start date of leave in YYYY-MM-DD format.")] DateOnly startDate,
            [Description("End date of leave in YYYY-MM-DD format.")] DateOnly endDate,
            [Description("Leave type (e.g., Vacation, Sick, Parental).")] LeaveTypeEnum leaveType,
            [Description("Reason or description for the leave.")] string reason)
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
                    // If timesheet is locked/submitted, we output to diagnostic console but don't crash the leave application
                    Console.WriteLine($"--> Auto-timesheet population skipped for {date:yyyy-MM-dd}: {ex.Message}");
                }
            }

            return request;
        }
    }
}
