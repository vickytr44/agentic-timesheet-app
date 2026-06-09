using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using TimesheetCopilotApp.Backend.Models;

namespace TimesheetCopilotApp.Backend;

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
