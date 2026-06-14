using backend.Models;
using System.Text.Json.Serialization;

namespace backend;

public class TimesheetStateSnapshot
{
    [JsonPropertyName("entries")]
    public List<TimesheetEntry> Entries { get; set; } = [];

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Draft";
}

[JsonSerializable(typeof(TimesheetStateSnapshot))]
[JsonSerializable(typeof(TimesheetEntry))]
[JsonSerializable(typeof(List<TimesheetEntry>))]
[JsonSerializable(typeof(backend.Models.LeaveRequest))]
[JsonSerializable(typeof(List<backend.Models.LeaveRequest>))]
[JsonSerializable(typeof(Dictionary<string, double>))]
[JsonSerializable(typeof(LeaveRequestInput))]
internal sealed partial class TimesheetSerializerContext : JsonSerializerContext;
