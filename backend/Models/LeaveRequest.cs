using System.Text.Json.Serialization;

namespace backend.Models
{
    public class LeaveRequest
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [JsonPropertyName("startDate")]
        public string StartDate { get; set; } = string.Empty; // Format YYYY-MM-DD

        [JsonPropertyName("endDate")]
        public string EndDate { get; set; } = string.Empty; // Format YYYY-MM-DD

        [JsonPropertyName("leaveType")]
        public string LeaveType { get; set; } = string.Empty; // e.g., Vacation, Sick, Parental

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "Approved"; // We'll auto-approve for simplicity, or "Pending"

        [JsonPropertyName("days")]
        public double Days { get; set; }
    }
}
