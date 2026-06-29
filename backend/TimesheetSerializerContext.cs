using backend.Models;
using System.Text.Json.Serialization;

namespace backend;

[JsonSerializable(typeof(TimesheetStateSnapshot))]
[JsonSerializable(typeof(TimesheetEntry))]
[JsonSerializable(typeof(List<TimesheetEntry>))]
[JsonSerializable(typeof(backend.Models.LeaveRequest))]
[JsonSerializable(typeof(List<backend.Models.LeaveRequest>))]
[JsonSerializable(typeof(Dictionary<string, double>))]
[JsonSerializable(typeof(LeaveRequestInput))]
internal sealed partial class TimesheetSerializerContext : JsonSerializerContext;
