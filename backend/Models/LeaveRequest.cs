using System.Text.Json.Serialization;

namespace backend.Models;

public class LeaveRequest
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("startDate")]
    public DateOnly StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateOnly EndDate { get; set; }

    [JsonPropertyName("leaveType")]
    public LeaveTypeEnum LeaveType { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; } 

    [JsonPropertyName("status")]
    public LeaveStatusEnum Status { get; set; } = LeaveStatusEnum.Pending;

    [JsonPropertyName("days")]
    public double Days { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter<LeaveTypeEnum>))]
public enum LeaveTypeEnum
{
    Vacation,
    Sick,
    Parental
}

[JsonConverter(typeof(JsonStringEnumConverter<LeaveStatusEnum>))]
public enum LeaveStatusEnum
{
    Pending,
    Approved,
    Rejected
}
