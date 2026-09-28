using System;

namespace ScreenTimeTracker.Models;

public class DayBarItem
{
    public string DayName { get; set; } = string.Empty;
    public string DayShort { get; set; } = string.Empty;
    public string DateString { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int Seconds { get; set; }
    public double Hours => Math.Round((double)Seconds / 3600.0, 1);
    public string FormattedTime => DailyTrackingData.FormatSeconds(Seconds);
    public string HoursLabel => Hours > 0 ? $"{Hours:0.#}h" : "0h";
    public double BarHeight { get; set; }
    public bool IsToday { get; set; }
    public string BarColor { get; set; } = "#3B82F6";
    public string BarBgColor { get; set; } = "#1E2536";
}
