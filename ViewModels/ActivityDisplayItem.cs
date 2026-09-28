using System;

namespace ScreenTimeTracker.ViewModels;

public class ActivityDisplayItem : ViewModelBase
{
    private string _id = string.Empty;
    private string _processName = string.Empty;
    private string _windowTitle = string.Empty;
    private string _category = string.Empty;
    private string _detail = string.Empty;
    private int _secondsSpent;
    private double _percentageOfTotal;
    private DateTime _lastActive;

    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public string ProcessName
    {
        get => _processName;
        set => SetField(ref _processName, value);
    }

    public string WindowTitle
    {
        get => _windowTitle;
        set => SetField(ref _windowTitle, value);
    }

    public string Category
    {
        get => _category;
        set
        {
            if (SetField(ref _category, value))
            {
                OnPropertyChanged(nameof(CategoryIcon));
                OnPropertyChanged(nameof(CategoryColor));
                OnPropertyChanged(nameof(CategoryBadgeBg));
            }
        }
    }

    public string Detail
    {
        get => _detail;
        set => SetField(ref _detail, value);
    }

    public int SecondsSpent
    {
        get => _secondsSpent;
        set
        {
            if (SetField(ref _secondsSpent, value))
            {
                OnPropertyChanged(nameof(FormattedDuration));
            }
        }
    }

    public double PercentageOfTotal
    {
        get => _percentageOfTotal;
        set => SetField(ref _percentageOfTotal, value);
    }

    public DateTime LastActive
    {
        get => _lastActive;
        set
        {
            if (SetField(ref _lastActive, value))
            {
                OnPropertyChanged(nameof(LastActiveText));
            }
        }
    }

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

    public string LastActiveText => LastActive.ToString("HH:mm:ss");

    public string CategoryIcon => Category switch
    {
        "Juegos" => "🎮",
        "Navegación Web" or "Navegación Web (Edge)" => "☁",
        _ => "📁"
    };

    public string CategoryColor => "#2DD4BF";

    public string CategoryBadgeBg => "#18181B";
}
