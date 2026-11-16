using System;
using System.ComponentModel;

namespace ScreenTimeTracker.Models;

public class DayBarItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string DayName { get; init; } = string.Empty;
    public string DayShort { get; init; } = string.Empty;
    public string DateString { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public bool CanOpen { get; private set; }
    public int Seconds { get; private set; }
    public string FormattedTime => DailyTrackingData.FormatSeconds(Seconds);
    public string HoursLabel => Seconds switch
    {
        < 60 => $"{Seconds} s",
        < 3600 => $"{Seconds / 60} min",
        _ => $"{Seconds / 3600.0:0.#} h"
    };
    public double BarHeight { get; private set; }
    public bool IsToday { get; private set; }

    public void UpdateUsage(int seconds, double barHeight, DateTime today)
    {
        if (Seconds == seconds && BarHeight == barHeight && CanOpen == (Date <= today) && IsToday == (Date == today)) return;

        Seconds = seconds;
        BarHeight = barHeight;
        CanOpen = Date <= today;
        IsToday = Date == today;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
