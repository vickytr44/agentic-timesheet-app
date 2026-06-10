using backend.Models;
using System.Text.Json.Serialization;

namespace Backend;

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
internal sealed partial class TimesheetSerializerContext : JsonSerializerContext;
