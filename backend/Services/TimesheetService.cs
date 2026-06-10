using backend.Models;
using System.ComponentModel;

namespace backend.Services
{
    public class TimesheetService
    {
        private readonly List<TimesheetEntry> _entries = new();
        private string _status = "Draft"; // Draft or Submitted

        public TimesheetService()
        {
            // Seed with some sample entries to make the UI look rich and functional on load!
            var today = DateTime.Today;
            _entries.Add(new TimesheetEntry
            {
                Id = Guid.NewGuid(),
                Date = today.AddDays(-2).ToString("yyyy-MM-dd"),
                Project = "Project Antigravity",
                Hours = 7.5,
                Description = "Refactored backend services and defined AG-UI endpoint protocols."
            });
            _entries.Add(new TimesheetEntry
            {
                Id = Guid.NewGuid(),
                Date = today.AddDays(-1).ToString("yyyy-MM-dd"),
                Project = "Admin & Training",
                Hours = 2.0,
                Description = "Attended bi-weekly alignment meeting and reviewed documentation."
            });
        }

        [Description("Gets the complete list of logged timesheet entries.")]
        public List<TimesheetEntry> GetTimesheet()
        {
            return _entries.OrderBy(e => e.Date).ToList();
        }

        [Description("Adds a new timesheet entry. Returns the newly created entry.")]
        public TimesheetEntry AddTimesheetEntry(
            [Description("The date of the work in YYYY-MM-DD format (e.g. 2026-05-23).")] string date,
            [Description("The project name, e.g. 'Project Antigravity' or 'Admin & Training'.")] string project,
            [Description("The number of hours worked (e.g. 7.5 or 8).")] double hours,
            [Description("A brief description of what task was completed.")] string description)
        {
            if (_status == "Submitted")
            {
                throw new InvalidOperationException("Cannot add entry to a submitted timesheet.");
            }

            // Simple date parsing to validate input
            if (!DateTime.TryParse(date, out _))
            {
                throw new ArgumentException("Invalid date format. Please use YYYY-MM-DD.");
            }

            if (hours <= 0 || hours > 24)
            {
                throw new ArgumentException("Hours worked must be greater than 0 and less than or equal to 24.");
            }

            var entry = new TimesheetEntry
            {
                Id = Guid.NewGuid(),
                Date = date,
                Project = project,
                Hours = hours,
                Description = description
            };

            _entries.Add(entry);
            return entry;
        }

        [Description("Removes a specific timesheet entry using its unique identifier (GUID).")]
        public bool RemoveTimesheetEntry(
            [Description("The unique identifier (GUID string) of the timesheet entry to delete.")] string id)
        {
            if (_status == "Submitted")
            {
                throw new InvalidOperationException("Cannot delete entry from a submitted timesheet.");
            }

            if (!Guid.TryParse(id, out var guid))
            {
                return false;
            }

            var entry = _entries.FirstOrDefault(e => e.Id == guid);
            if (entry == null)
            {
                return false;
            }

            return _entries.Remove(entry);
        }

        [Description("Clears all timesheet entries in the current timesheet.")]
        public bool ClearTimesheet()
        {
            if (_status == "Submitted")
            {
                throw new InvalidOperationException("Cannot clear a submitted timesheet.");
            }

            _entries.Clear();
            return true;
        }

        [Description("Submits the current timesheet, marking its status as Submitted. This locks the timesheet from further modifications.")]
        public string SubmitTimesheet()
        {
            _status = "Submitted";
            return "Timesheet successfully submitted!";
        }

        [Description("Unlocks a submitted timesheet, changing its status back to 'Draft' so it can be edited again.")]
        public string UnlockTimesheet()
        {
            _status = "Draft";
            return "Timesheet successfully unlocked and reverted to Draft!";
        }

        [Description("Gets the current submission status of the timesheet (either 'Draft' or 'Submitted').")]
        public string GetStatus()
        {
            return _status;
        }

        [Description("Gets the summary metrics: TotalHours, TotalEntries, Status, and ProjectCount.")]
        public Dictionary<string, object> GetSummary()
        {
            return new Dictionary<string, object>
            {
                { "TotalHours", _entries.Sum(e => e.Hours) },
                { "TotalEntries", _entries.Count },
                { "Status", _status },
                { "ProjectCount", _entries.Select(e => e.Project).Distinct().Count() }
            };
        }

        public void SyncState(List<TimesheetEntry> entries, string status)
        {
            _entries.Clear();
            if (entries != null)
            {
                _entries.AddRange(entries);
            }
            _status = status ?? "Draft";
        }
    }
}
