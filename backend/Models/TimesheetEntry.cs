using System;
using System.Text.Json.Serialization;

namespace TimesheetCopilotApp.Backend.Models
{
    public class TimesheetEntry
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty; // Format YYYY-MM-DD

        [JsonPropertyName("project")]
        public string Project { get; set; } = string.Empty;

        [JsonPropertyName("hours")]
        public double Hours { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }
}
