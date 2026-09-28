using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace ScreenTimeTracker.Models;

public class DailyTrackingData
{
    public string Date { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
    public Dictionary<string, ActivityRecord> Activities { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public int TotalGamingSeconds => Activities.Values
        .Where(a => a.Category == "Juegos")
        .Sum(a => a.SecondsSpent);

    [JsonIgnore]
    public int TotalDesktopSeconds => Activities.Values
        .Where(a => a.Category == "Aplicaciones de Escritorio")
        .Sum(a => a.SecondsSpent);

    [JsonIgnore]
    public int TotalWebSeconds => Activities.Values
        .Where(a => a.Category == "Navegación Web" || a.Category == "Navegación Web (Edge)")
        .Sum(a => a.SecondsSpent);

    [JsonIgnore]
    public int TotalSeconds => Activities.Values.Sum(a => a.SecondsSpent);

    [JsonIgnore]
    public string FormattedTotalTime => FormatSeconds(TotalSeconds);

    [JsonIgnore]
    public string FormattedGamingTime => FormatSeconds(TotalGamingSeconds);

    [JsonIgnore]
    public string FormattedDesktopTime => FormatSeconds(TotalDesktopSeconds);

    [JsonIgnore]
    public string FormattedWebTime => FormatSeconds(TotalWebSeconds);

    public static string FormatSeconds(int totalSeconds)
    {
        var span = TimeSpan.FromSeconds(totalSeconds);
        if (span.TotalHours >= 1)
        {
            return $"{(int)span.TotalHours}h {span.Minutes:D2}m {span.Seconds:D2}s";
        }
        return $"{span.Minutes:D2}m {span.Seconds:D2}s";
    }
}
