using System;
using System.Text.Json.Serialization;

namespace ScreenTimeTracker.Models;

public class ActivityRecord
{
    public string Id { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public string Category { get; set; } = "Aplicaciones de Escritorio";
    public string Detail { get; set; } = string.Empty;
    public int SecondsSpent { get; set; }
    public DateTime LastActive { get; set; } = DateTime.Now;

    [JsonIgnore]
    public string FormattedDuration
    {
        get
        {
            var span = TimeSpan.FromSeconds(SecondsSpent);
            if (span.TotalHours >= 1)
            {
                return $"{(int)span.TotalHours}h {span.Minutes:D2}m {span.Seconds:D2}s";
            }
            return $"{span.Minutes:D2}m {span.Seconds:D2}s";
        }
    }
}
