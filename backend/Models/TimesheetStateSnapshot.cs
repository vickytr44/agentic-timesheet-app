using System.Text.Json.Serialization;

namespace backend.Models;

/// <summary>
/// Represents the serializable state snapshot exchanged between the frontend (AG-UI)
/// and the backend Timesheet agent for bidirectional state synchronization.
/// </summary>
public class TimesheetStateSnapshot
{
    [JsonPropertyName("entries")]
    public List<TimesheetEntry> Entries { get; set; } = [];

    [JsonPropertyName("status")]
    public string Status { get; set; } = TimesheetStatus.Draft;
}
