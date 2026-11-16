using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ScreenTimeTracker.Models;
using ScreenTimeTracker.Services;
using TrackingStatus = ScreenTimeTracker.Services.WindowTrackerService.TrackingStatus;

namespace ScreenTimeTracker.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly WindowTrackerService _trackerService;
    private readonly StorageService _storageService;

    private TrackingStatus _trackingStatus;
    private bool _isInitialized;
    private string? _overviewDate;
    private int? _overviewPastSeconds;
    private string _weeklyOverviewAverage = "—";
    private int _chartRequestId;
    private DateTime _lastTrackingDate = DateTime.Today;
    private string _currentDateFormatted = string.Empty;
    private string _totalScreenTime = "00m 00s";
    private int _totalScreenSeconds;

    private string _gamingTime = "00m 00s";
    private int _gamingSeconds;
    private double _gamingPercent;
    private string _topGame = "Sin actividad";

    private string _desktopTime = "00m 00s";
    private int _desktopSeconds;
    private double _desktopPercent;
    private string _topDesktopApp = "Sin actividad";

    private string _webTime = "00m 00s";
    private int _webSeconds;
    private double _webPercent;
    private string _topWebDomain = "Sin actividad";

    private string _activeAppText = string.Empty;

    private string _selectedFilter = "Todas";
    private bool _isSettingsOpen = false;
    private string _newGameInput = string.Empty;
    private string _newDirectoryInput = string.Empty;
    private bool _hasNoActivities = true;
    private DateTime _selectedDate = DateTime.Today;
    private DailyTrackingData? _viewedDayData;

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetField(ref _selectedDate, value.Date))
            {
                UpdateDateString();
                OnPropertyChanged(nameof(IsViewingToday));
                OnPropertyChanged(nameof(CanGoToNextDay));
                OnPropertyChanged(nameof(DateNavigationLabel));
                _ = LoadDateDataAsync(_selectedDate);
            }
        }
    }

    public bool IsViewingToday => _selectedDate.Date == DateTime.Today;
    public bool CanGoToNextDay => _selectedDate.Date < DateTime.Today;

    public string DateNavigationLabel
    {
        get
        {
            int diff = (int)(DateTime.Today - _selectedDate.Date).TotalDays;
            if (diff == 0) return "HOY";
            if (diff == 1) return "AYER";
            return $"HACE {diff} DÍAS";
        }
    }

    public ObservableCollection<ActivityDisplayItem> DisplayActivities { get; } = new();
    public ObservableCollection<string> GameDirectories { get; } = new();
    public ObservableCollection<string> GameList { get; } = new();
    public ObservableCollection<DayBarItem> WeekBars { get; } = new();

    // Propiedades para el modal de gráfico semanal
    private bool _isChartOpen;
    private DateTime _chartReferenceDate = DateTime.Today;
    private string _selectedChartCategory = "Total";
    private string _chartTitle = "Tiempo Semanal";
    private string _chartSubtitle = "Lunes a Domingo";
    private string _chartIcon = "⏳";
    private string _weekTotalFormatted = "00m 00s";
    private string _weekAverageFormatted = "00m 00s / día";
    private string _weekPeakDay = "Sin actividad";
    private string _weekTopAppsFormatted = "Sin datos suficientes";
    private string _weekTrendFormatted = "-";
    private string _weekTrendColor = "#9CA3AF";

    public bool CanGoToNextWeek
    {
        get
        {
            var currentMonday = StorageService.GetWeekDates(DateTime.Today)[0];
            var refMonday = StorageService.GetWeekDates(_chartReferenceDate)[0];
            return refMonday < currentMonday;
        }
    }

    public bool IsCurrentChartWeek
    {
        get
        {
            var currentMonday = StorageService.GetWeekDates(DateTime.Today)[0];
            var refMonday = StorageService.GetWeekDates(_chartReferenceDate)[0];
            return refMonday == currentMonday;
        }
    }

    public bool CanGoToCurrentWeek => !IsCurrentChartWeek;

    public string ChartWeekRelativeLabel
    {
        get
        {
            var currentMonday = StorageService.GetWeekDates(DateTime.Today)[0];
            var refMonday = StorageService.GetWeekDates(_chartReferenceDate)[0];
            int daysDiff = (int)(currentMonday - refMonday).TotalDays;
            int weeksAgo = daysDiff / 7;

            if (weeksAgo == 0) return "SEMANA ACTUAL";
            if (weeksAgo == 1) return "HACE 1 SEMANA";
            if (weeksAgo > 1) return $"HACE {weeksAgo} SEMANAS";
            return "FUTURO";
        }
    }

    public bool IsChartOpen
    {
        get => _isChartOpen;
        set => SetField(ref _isChartOpen, value);
    }

    public string SelectedChartCategory
    {
        get => _selectedChartCategory;
        set
        {
            if (SetField(ref _selectedChartCategory, value))
            {
                OnPropertyChanged(nameof(IsChartTotalSelected));
                OnPropertyChanged(nameof(IsChartGamingSelected));
                OnPropertyChanged(nameof(IsChartDesktopSelected));
                OnPropertyChanged(nameof(IsChartWebSelected));
            }
        }
    }

    public bool IsChartTotalSelected => string.Equals(SelectedChartCategory, "Total", StringComparison.OrdinalIgnoreCase) || string.Equals(SelectedChartCategory, "Todo", StringComparison.OrdinalIgnoreCase);
    public bool IsChartGamingSelected => string.Equals(SelectedChartCategory, "Juegos", StringComparison.OrdinalIgnoreCase);
    public bool IsChartDesktopSelected => string.Equals(SelectedChartCategory, "Aplicaciones de Escritorio", StringComparison.OrdinalIgnoreCase);
    public bool IsChartWebSelected => string.Equals(SelectedChartCategory, "Navegación Web", StringComparison.OrdinalIgnoreCase) || string.Equals(SelectedChartCategory, "Navegación Web (Edge)", StringComparison.OrdinalIgnoreCase);

    public string ChartTitle
    {
        get => _chartTitle;
        set => SetField(ref _chartTitle, value);
    }

    public string ChartSubtitle
    {
        get => _chartSubtitle;
        set => SetField(ref _chartSubtitle, value);
    }

    public string ChartIcon
    {
        get => _chartIcon;
        set => SetField(ref _chartIcon, value);
    }

    public string WeekTotalFormatted
    {
        get => _weekTotalFormatted;
        set => SetField(ref _weekTotalFormatted, value);
    }

    public string WeekAverageFormatted
    {
        get => _weekAverageFormatted;
        set => SetField(ref _weekAverageFormatted, value);
    }

    public string WeekPeakDay
    {
        get => _weekPeakDay;
        set => SetField(ref _weekPeakDay, value);
    }

    public string WeekTopAppsFormatted
    {
        get => _weekTopAppsFormatted;
        set => SetField(ref _weekTopAppsFormatted, value);
    }

    public string WeekTrendFormatted
    {
        get => _weekTrendFormatted;
        set => SetField(ref _weekTrendFormatted, value);
    }

    public string WeekTrendColor
    {
        get => _weekTrendColor;
        set => SetField(ref _weekTrendColor, value);
    }

    public bool HasNoActivities
    {
        get => _hasNoActivities;
        set => SetField(ref _hasNoActivities, value);
    }

    public TrackingStatus TrackingState
    {
        get => _trackingStatus;
        private set
        {
            if (SetField(ref _trackingStatus, value))
            {
                OnPropertyChanged(nameof(TrackingStatusText));
                OnPropertyChanged(nameof(TrackingStatusColor));
                OnPropertyChanged(nameof(PauseButtonText));
            }
        }
    }

    public string TrackingStatusText => TrackingState switch
    {
        TrackingStatus.Active => "Registrando actividad",
        TrackingStatus.Idle => "En pausa por inactividad",
        TrackingStatus.Locked => "En pausa por pantalla bloqueada",
        TrackingStatus.Suspended => "En pausa por suspensión",
        TrackingStatus.Paused => "En pausa manual",
        _ => "Esperando actividad"
    };

    public string TrackingStatusColor => TrackingState == TrackingStatus.Active ? "#2DD4BF" : "#F59E0B";
    public string PauseButtonText => TrackingState == TrackingStatus.Paused ? "Reanudar registro" : "Pausar registro";
    public bool IsInitialized
    {
        get => _isInitialized;
        private set => SetField(ref _isInitialized, value);
    }

    public string WeeklyOverviewAverage
    {
        get => _weeklyOverviewAverage;
        private set => SetField(ref _weeklyOverviewAverage, value);
    }

    public record InactivityOption(int Minutes, string Label);

    public IReadOnlyList<InactivityOption> InactivityOptions { get; } = new[]
    {
        new InactivityOption(2, "2 minutos"),
        new InactivityOption(5, "5 minutos"),
        new InactivityOption(10, "10 minutos"),
        new InactivityOption(15, "15 minutos"),
        new InactivityOption(0, "Desactivada")
    };

    public int InactivityTimeoutMinutes
    {
        get => _trackerService.Config.InactivityTimeoutMinutes;
        set
        {
            if (!IsInitialized || value == InactivityTimeoutMinutes) return;

            _ = _trackerService.SetInactivityTimeoutAsync(value);
            OnPropertyChanged();
        }
    }

    public string CurrentDateFormatted
    {
        get => _currentDateFormatted;
        set => SetField(ref _currentDateFormatted, value);
    }

    public string TotalScreenTime
    {
        get => _totalScreenTime;
        set => SetField(ref _totalScreenTime, value);
    }

    public int TotalScreenSeconds
    {
        get => _totalScreenSeconds;
        set => SetField(ref _totalScreenSeconds, value);
    }

    public string GamingTime
    {
        get => _gamingTime;
        set => SetField(ref _gamingTime, value);
    }

    public int GamingSeconds
    {
        get => _gamingSeconds;
        set => SetField(ref _gamingSeconds, value);
    }

    public double GamingPercent
    {
        get => _gamingPercent;
        set => SetField(ref _gamingPercent, value);
    }

    public string TopGame
    {
        get => _topGame;
        set => SetField(ref _topGame, value);
    }

    public string DesktopTime
    {
        get => _desktopTime;
        set => SetField(ref _desktopTime, value);
    }

    public int DesktopSeconds
    {
        get => _desktopSeconds;
        set => SetField(ref _desktopSeconds, value);
    }

    public double DesktopPercent
    {
        get => _desktopPercent;
        set => SetField(ref _desktopPercent, value);
    }

    public string TopDesktopApp
    {
        get => _topDesktopApp;
        set => SetField(ref _topDesktopApp, value);
    }

    public string WebTime
    {
        get => _webTime;
        set => SetField(ref _webTime, value);
    }

    public int WebSeconds
    {
        get => _webSeconds;
        set => SetField(ref _webSeconds, value);
    }

    public double WebPercent
    {
        get => _webPercent;
        set => SetField(ref _webPercent, value);
    }

    public string TopWebDomain
    {
        get => _topWebDomain;
        set => SetField(ref _topWebDomain, value);
    }

    public string ActiveAppText
    {
        get => _activeAppText;
        private set
        {
            if (SetField(ref _activeAppText, value))
            {
                OnPropertyChanged(nameof(HasActiveApp));
            }
        }
    }

    public bool HasActiveApp => !string.IsNullOrEmpty(ActiveAppText);

    public string SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetField(ref _selectedFilter, value))
            {
                RefreshDisplayList();
            }
        }
    }

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => SetField(ref _isSettingsOpen, value);
    }

    public string NewGameInput
    {
        get => _newGameInput;
        set => SetField(ref _newGameInput, value);
    }

    public string NewDirectoryInput
    {
        get => _newDirectoryInput;
        set => SetField(ref _newDirectoryInput, value);
    }

    private bool _startWithWindows;
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (SetField(ref _startWithWindows, value))
            {
                StartupService.SetStartup(value);
                _trackerService.Config.StartWithWindows = value;
                _ = _storageService.SaveConfigAsync(_trackerService.Config);
            }
        }
    }

    private string _selectedSettingsTab = "General";
    public string SelectedSettingsTab
    {
        get => _selectedSettingsTab;
        set
        {
            if (SetField(ref _selectedSettingsTab, value))
            {
                OnPropertyChanged(nameof(IsGeneralTabSelected));
                OnPropertyChanged(nameof(IsGamesTabSelected));
            }
        }
    }

    public bool IsGeneralTabSelected => SelectedSettingsTab == "General";
    public bool IsGamesTabSelected => SelectedSettingsTab == "Juegos";

    public ICommand SetFilterCommand { get; }
    public ICommand ToggleSettingsCommand { get; }
    public ICommand SwitchSettingsTabCommand { get; }
    public ICommand AddGameCommand { get; }
    public ICommand RemoveGameCommand { get; }
    public ICommand AddDirectoryCommand { get; }
    public ICommand RemoveDirectoryCommand { get; }
    public ICommand BrowseDirectoryCommand { get; }
    public ICommand OpenChartCommand { get; }
    public ICommand OpenCurrentWeekCommand { get; }
    public ICommand TogglePauseCommand { get; }
    public ICommand CloseOverlayCommand { get; }
    public ICommand CloseChartCommand { get; }
    public ICommand SwitchChartCategoryCommand { get; }
    public ICommand PreviousWeekCommand { get; }
    public ICommand NextWeekCommand { get; }
    public ICommand CurrentWeekCommand { get; }
    public ICommand PreviousDayCommand { get; }
    public ICommand NextDayCommand { get; }
    public ICommand GoToTodayCommand { get; }
    public ICommand SelectDayFromChartCommand { get; }

    public MainViewModel(WindowTrackerService trackerService, StorageService storageService)
    {
        _trackerService = trackerService;
        _storageService = storageService;

        UpdateDateString();

        SetFilterCommand = new RelayCommand(param =>
        {
            if (param is string cat) SelectedFilter = cat;
        });
        ToggleSettingsCommand = new RelayCommand(() => IsSettingsOpen = !IsSettingsOpen);
        SwitchSettingsTabCommand = new RelayCommand(param =>
        {
            if (param is string tab) SelectedSettingsTab = tab;
        });
        AddGameCommand = new RelayCommand(async () => await AddGameExecuteAsync());
        RemoveGameCommand = new RelayCommand(async param =>
        {
            if (param is string game) await RemoveGameExecuteAsync(game);
        });

        AddDirectoryCommand = new RelayCommand(async () => await AddDirectoryExecuteAsync());
        RemoveDirectoryCommand = new RelayCommand(async param =>
        {
            if (param is string dir) await RemoveDirectoryExecuteAsync(dir);
        });
        BrowseDirectoryCommand = new RelayCommand(BrowseDirectoryExecute);

        OpenChartCommand = new RelayCommand(async param => await OpenChartExecuteAsync(SelectedDate, param as string));
        OpenCurrentWeekCommand = new RelayCommand(async () => await OpenChartExecuteAsync(DateTime.Today, "Total"));
        TogglePauseCommand = new RelayCommand(() => _trackerService.TogglePause());
        CloseChartCommand = new RelayCommand(CloseChart);
        CloseOverlayCommand = new RelayCommand(() =>
        {
            if (IsChartOpen) CloseChart();
            else IsSettingsOpen = false;
        });
        SwitchChartCategoryCommand = new RelayCommand(async param => await SwitchChartCategoryExecuteAsync(param as string));
        PreviousWeekCommand = new RelayCommand(async () =>
        {
            _chartReferenceDate = _chartReferenceDate.AddDays(-7);
            await RefreshChartDataAsync();
        });
        NextWeekCommand = new RelayCommand(async () =>
        {
            if (CanGoToNextWeek)
            {
                _chartReferenceDate = _chartReferenceDate.AddDays(7);
                await RefreshChartDataAsync();
            }
        });
        CurrentWeekCommand = new RelayCommand(async () =>
        {
            _chartReferenceDate = DateTime.Today;
            await RefreshChartDataAsync();
        });

        // Comandos de navegación diaria del historial
        PreviousDayCommand = new RelayCommand(() =>
        {
            SelectedDate = SelectedDate.AddDays(-1);
        });
        NextDayCommand = new RelayCommand(() =>
        {
            if (CanGoToNextDay)
            {
                SelectedDate = SelectedDate.AddDays(1);
            }
        });
        GoToTodayCommand = new RelayCommand(() =>
        {
            SelectedDate = DateTime.Today;
        });
        SelectDayFromChartCommand = new RelayCommand(param =>
        {
            if (param is DayBarItem bar && bar.Date <= DateTime.Today)
            {
                SelectedDate = bar.Date;
                CloseChart();
            }
        });

        _trackerService.OnSampleUpdated += () =>
        {
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(UpdateFromTracker);
        };
    }

    private void UpdateDateString()
    {
        var culture = new CultureInfo("es-ES");
        string formatted = _selectedDate.ToString("dddd, d 'de' MMMM 'de' yyyy", culture);
        CurrentDateFormatted = char.ToUpper(formatted[0]) + formatted[1..];
    }

    public async Task LoadDateDataAsync(DateTime date)
    {
        var data = date.Date == DateTime.Today
            ? _trackerService.GetSnapshot().Day
            : await _storageService.LoadDailyDataAsync(date.ToString("yyyy-MM-dd"));
        if (SelectedDate != date) return;

        _viewedDayData = data;
        UpdateStatsFromData(data);
        RefreshDisplayList();
    }

    public async Task InitializeAsync()
    {
        await _trackerService.InitializeAsync();
        UpdateConfigLists();
        _startWithWindows = StartupService.IsStartupEnabled();
        OnPropertyChanged(nameof(StartWithWindows));
        UpdateDateString();
        IsInitialized = true;
        OnPropertyChanged(nameof(InactivityTimeoutMinutes));
        UpdateFromTracker();
        _trackerService.Start();
    }

    private void UpdateFromTracker()
    {
        var (status, activity, day) = _trackerService.GetSnapshot();
        TrackingState = status;

        ActiveAppText = activity == null
            ? string.Empty
            : string.IsNullOrWhiteSpace(activity.Detail) ? activity.ProcessName : activity.Detail;

        var today = DateTime.Today;
        if (today != _lastTrackingDate)
        {
            if (SelectedDate == _lastTrackingDate)
            {
                SelectedDate = today;
            }
            _lastTrackingDate = today;
            OnPropertyChanged(nameof(CanGoToNextDay));
            OnPropertyChanged(nameof(DateNavigationLabel));
        }

        if (IsViewingToday && day.Date == today.ToString("yyyy-MM-dd"))
        {
            _viewedDayData = day;
            UpdateStatsFromData(day);
            RefreshDisplayList();
        }

        _ = UpdateWeeklyOverviewAsync(today, day);
        if (IsChartOpen && IsCurrentChartWeek)
        {
            _ = RefreshChartDataAsync();
        }
    }

    private async Task UpdateWeeklyOverviewAsync(DateTime today, DailyTrackingData day)
    {
        string dateKey = today.ToString("yyyy-MM-dd");
        if (_overviewDate != dateKey)
        {
            _overviewDate = dateKey;
            _overviewPastSeconds = null;
            WeeklyOverviewAverage = "—";
            var week = await _storageService.LoadWeekDataAsync(today);
            if (_overviewDate != dateKey) return;

            _overviewPastSeconds = StorageService.GetWeekDates(today)
                .Where(date => date < today)
                .Sum(date => week[date.ToString("yyyy-MM-dd")].TotalSeconds);
            day = _trackerService.GetSnapshot().Day;
        }

        if (_overviewPastSeconds is int pastSeconds && day.Date == dateKey)
        {
            int daysElapsed = StorageService.GetWeekDates(today).Count(date => date <= today);
            WeeklyOverviewAverage = FormatDailyAverage(pastSeconds + day.TotalSeconds, daysElapsed);
        }
    }

    private static string FormatDailyAverage(int seconds, int days) =>
        $"{DailyTrackingData.FormatSeconds(seconds / days)} / día";

    private void UpdateStatsFromData(DailyTrackingData data)
    {
        TotalScreenSeconds = data.TotalSeconds;
        TotalScreenTime = data.FormattedTotalTime;

        GamingSeconds = data.TotalGamingSeconds;
        GamingTime = data.FormattedGamingTime;
        GamingPercent = TotalScreenSeconds > 0 ? (double)GamingSeconds / TotalScreenSeconds * 100 : 0;

        DesktopSeconds = data.TotalDesktopSeconds;
        DesktopTime = data.FormattedDesktopTime;
        DesktopPercent = TotalScreenSeconds > 0 ? (double)DesktopSeconds / TotalScreenSeconds * 100 : 0;

        WebSeconds = data.TotalWebSeconds;
        WebTime = data.FormattedWebTime;
        WebPercent = TotalScreenSeconds > 0 ? (double)WebSeconds / TotalScreenSeconds * 100 : 0;

        // Top Game
        var topGameRecord = data.Activities.Values
            .Where(a => a.Category == "Juegos")
            .OrderByDescending(a => a.SecondsSpent)
            .FirstOrDefault();
        TopGame = topGameRecord != null ? $"{topGameRecord.Detail} ({topGameRecord.FormattedDuration})" : (IsViewingToday ? "Sin juegos hoy" : "Sin juegos este día");

        // Top App
        var topAppRecord = data.Activities.Values
            .Where(a => a.Category == "Aplicaciones de Escritorio")
            .OrderByDescending(a => a.SecondsSpent)
            .FirstOrDefault();
        TopDesktopApp = topAppRecord != null ? $"{topAppRecord.Detail} ({topAppRecord.FormattedDuration})" : (IsViewingToday ? "Sin apps hoy" : "Sin apps este día");

        // Top Web
        var topWebRecord = data.Activities.Values
            .Where(a => a.Category == "Navegación Web" || a.Category == "Navegación Web (Edge)")
            .OrderByDescending(a => a.SecondsSpent)
            .FirstOrDefault();
        TopWebDomain = topWebRecord != null ? $"{topWebRecord.Detail} ({topWebRecord.FormattedDuration})" : (IsViewingToday ? "Sin navegación hoy" : "Sin navegación este día");
    }

    private void RefreshDisplayList()
    {
        var activeData = _viewedDayData ?? _trackerService.GetSnapshot().Day;
        var rawList = activeData.Activities.Values.AsEnumerable();

        if (SelectedFilter != "Todas")
        {
            if (SelectedFilter.Equals("Navegación Web", StringComparison.OrdinalIgnoreCase) ||
                SelectedFilter.Equals("Navegación Web (Edge)", StringComparison.OrdinalIgnoreCase))
            {
                rawList = rawList.Where(a => a.Category.Equals("Navegación Web", StringComparison.OrdinalIgnoreCase) ||
                                             a.Category.Equals("Navegación Web (Edge)", StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                rawList = rawList.Where(a => a.Category.Equals(SelectedFilter, StringComparison.OrdinalIgnoreCase));
            }
        }

        var sorted = rawList.OrderByDescending(a => a.SecondsSpent).ToList();
        int totalSec = TotalScreenSeconds > 0 ? TotalScreenSeconds : 1;

        DisplayActivities.Clear();
        foreach (var item in sorted)
        {
            DisplayActivities.Add(new ActivityDisplayItem
            {
                Id = item.Id,
                ProcessName = item.ProcessName,
                WindowTitle = item.WindowTitle,
                Category = item.Category,
                Detail = string.IsNullOrWhiteSpace(item.Detail) ? item.ProcessName : item.Detail,
                SecondsSpent = item.SecondsSpent,
                PercentageOfTotal = Math.Round((double)item.SecondsSpent / totalSec * 100, 1),
                LastActive = item.LastActive
            });
        }

        HasNoActivities = DisplayActivities.Count == 0;
    }

    private void UpdateConfigLists()
    {
        GameDirectories.Clear();
        foreach (var d in _trackerService.Config.GameDirectories)
        {
            GameDirectories.Add(d);
        }

        GameList.Clear();
        foreach (var g in _trackerService.Config.GameExecutables.OrderBy(x => x))
        {
            GameList.Add(g);
        }
    }

    private void BrowseDirectoryExecute()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Selecciona la carpeta de tu biblioteca de juegos (Steam, Epic, etc.)"
        };

        if (dialog.ShowDialog() == true)
        {
            NewDirectoryInput = dialog.FolderName;
        }
    }

    private async Task AddDirectoryExecuteAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDirectoryInput)) return;

        await _trackerService.AddGameDirectoryAsync(NewDirectoryInput.Trim());
        NewDirectoryInput = string.Empty;
        UpdateConfigLists();
    }

    private async Task RemoveDirectoryExecuteAsync(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) return;

        await _trackerService.RemoveGameDirectoryAsync(dir);
        UpdateConfigLists();
    }

    private async Task AddGameExecuteAsync()
    {
        if (string.IsNullOrWhiteSpace(NewGameInput)) return;

        await _trackerService.AddGameAsync(NewGameInput.Trim());
        NewGameInput = string.Empty;
        UpdateConfigLists();
    }

    private async Task RemoveGameExecuteAsync(string game)
    {
        if (string.IsNullOrWhiteSpace(game)) return;

        await _trackerService.RemoveGameAsync(game);
        UpdateConfigLists();
    }

    private async Task OpenChartExecuteAsync(DateTime date, string? category)
    {
        _chartReferenceDate = date;
        SelectedChartCategory = string.IsNullOrWhiteSpace(category) ? "Total" : category;
        IsChartOpen = true;
        await RefreshChartDataAsync();
    }

    private void CloseChart()
    {
        IsChartOpen = false;
        _chartRequestId++;
    }

    private async Task SwitchChartCategoryExecuteAsync(string? category)
    {
        SelectedChartCategory = string.IsNullOrWhiteSpace(category) ? "Total" : category;
        await RefreshChartDataAsync();
    }

    public async Task RefreshChartDataAsync()
    {
        int requestId = ++_chartRequestId;
        var referenceDate = _chartReferenceDate;
        var category = SelectedChartCategory;
        var today = DateTime.Today;
        var weekDates = StorageService.GetWeekDates(referenceDate);
        var previousWeekDates = StorageService.GetWeekDates(referenceDate.AddDays(-7));
        var weekTask = _storageService.LoadWeekDataAsync(referenceDate);
        var previousWeekTask = _storageService.LoadWeekDataAsync(referenceDate.AddDays(-7));
        await Task.WhenAll(weekTask, previousWeekTask);
        if (requestId != _chartRequestId) return;

        var weekData = await weekTask;
        var previousWeekData = await previousWeekTask;
        if (weekDates.Contains(today))
        {
            var snapshot = _trackerService.GetSnapshot().Day;
            if (snapshot.Date == today.ToString("yyyy-MM-dd"))
            {
                weekData[snapshot.Date] = snapshot;
            }
        }

        switch (category)
        {
            case "Juegos":
                ChartTitle = "Distribución Semanal: Juegos";
                ChartIcon = "🎮";
                break;
            case "Aplicaciones de Escritorio":
                ChartTitle = "Distribución Semanal: Escritorio";
                ChartIcon = "📁";
                break;
            case "Navegación Web":
            case "Navegación Web (Edge)":
                ChartTitle = "Distribución Semanal: Navegación Web";
                ChartIcon = "☁";
                break;
            default:
                ChartTitle = "Distribución Semanal: Todo";
                ChartIcon = "⏱";
                break;
        }

        var culture = new CultureInfo("es-ES");
        DateTime monday = weekDates[0];
        DateTime sunday = weekDates[6];
        ChartSubtitle = $"Del {monday.ToString("d 'de' MMMM", culture)} al {sunday.ToString("d 'de' MMMM 'de' yyyy", culture)}";

        var daySecondsList = new List<int>();
        foreach (var d in weekDates)
        {
            string dateStr = d.ToString("yyyy-MM-dd");
            int sec = 0;
            if (d <= today && weekData.TryGetValue(dateStr, out var daily))
            {
                sec = category switch
                {
                    "Juegos" => daily.TotalGamingSeconds,
                    "Aplicaciones de Escritorio" => daily.TotalDesktopSeconds,
                    "Navegación Web" or "Navegación Web (Edge)" => daily.TotalWebSeconds,
                    _ => daily.TotalSeconds
                };
            }
            daySecondsList.Add(sec);
        }

        int maxSec = daySecondsList.Count > 0 ? daySecondsList.Max() : 0;
        int scaleMax = Math.Max(3600, maxSec); // Escala mínima de 1 hora
        const double maxBarHeightPx = 120.0;

        int totalWeekSec = daySecondsList.Sum();
        WeekTotalFormatted = DailyTrackingData.FormatSeconds(totalWeekSec);
        int elapsedDays = weekDates.Count(date => date <= today);
        WeekAverageFormatted = FormatDailyAverage(totalWeekSec, Math.Max(1, elapsedDays));

        int peakSec = -1;
        string peakDayName = "Sin actividad";

        string[] dayNames = { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };
        string[] dayShortNames = { "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom" };

        // Mantener los botones de la misma semana permite conservar el foco durante las actualizaciones.
        if (WeekBars.Count == 0 || WeekBars[0].Date != monday)
        {
            WeekBars.Clear();
            for (int i = 0; i < weekDates.Count; i++)
            {
                WeekBars.Add(new DayBarItem
                {
                    DayName = dayNames[i],
                    DayShort = dayShortNames[i],
                    DateString = weekDates[i].ToString("dd MMM", culture),
                    Date = weekDates[i]
                });
            }
        }

        for (int i = 0; i < WeekBars.Count; i++)
        {
            int sec = daySecondsList[i];
            double ratio = (double)sec / scaleMax;
            double barHeight = sec > 0 ? Math.Max(12.0, ratio * maxBarHeightPx) : 4.0;

            if (sec > peakSec && sec > 0)
            {
                peakSec = sec;
                peakDayName = $"{dayNames[i]} ({DailyTrackingData.FormatSeconds(sec)})";
            }

            WeekBars[i].UpdateUsage(sec, barHeight, today);
        }

        WeekPeakDay = peakDayName;

        // Tendencia vs semana pasada
        int prevWeekTotalSec = 0;
        foreach (var d in previousWeekDates.Take(elapsedDays))
        {
            if (previousWeekData.TryGetValue(d.ToString("yyyy-MM-dd"), out var daily))
            {
                prevWeekTotalSec += category switch
                {
                    "Juegos" => daily.TotalGamingSeconds,
                    "Aplicaciones de Escritorio" => daily.TotalDesktopSeconds,
                    "Navegación Web" or "Navegación Web (Edge)" => daily.TotalWebSeconds,
                    _ => daily.TotalSeconds
                };
            }
        }

        if (prevWeekTotalSec > 0)
        {
            double diff = totalWeekSec - prevWeekTotalSec;
            double pct = (diff / prevWeekTotalSec) * 100;
            if (pct > 0)
            {
                WeekTrendFormatted = $"▲ {pct:F1}% más que sem. pasada";
                WeekTrendColor = "#EF4444"; // Rojo (más tiempo)
            }
            else if (pct < 0)
            {
                WeekTrendFormatted = $"▼ {Math.Abs(pct):F1}% menos que sem. pasada";
                WeekTrendColor = "#10B981"; // Verde (menos tiempo)
            }
            else
            {
                WeekTrendFormatted = "Sin cambios";
                WeekTrendColor = "#9CA3AF";
            }
        }
        else
        {
            WeekTrendFormatted = "Sin datos semana previa";
            WeekTrendColor = "#9CA3AF";
        }

        // Top 3 Apps
        var allActivities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var date in weekDates.Take(elapsedDays))
        {
            var daily = weekData[date.ToString("yyyy-MM-dd")];
            foreach (var act in daily.Activities.Values)
            {
                bool match = false;
                if (category == "Total" || category == "Todo") match = true;
                else if (category == "Navegación Web" && (act.Category == "Navegación Web" || act.Category == "Navegación Web (Edge)")) match = true;
                else if (act.Category == category) match = true;

                if (match && act.SecondsSpent > 0)
                {
                    string key = string.IsNullOrWhiteSpace(act.Detail) ? act.ProcessName : act.Detail;
                    if (!allActivities.ContainsKey(key)) allActivities[key] = 0;
                    allActivities[key] += act.SecondsSpent;
                }
            }
        }

        var top3 = allActivities.OrderByDescending(kv => kv.Value).Take(3).ToList();
        if (top3.Any())
        {
            var lines = top3.Select((kv, index) => $"{index + 1}. {kv.Key} ({DailyTrackingData.FormatSeconds(kv.Value)})");
            WeekTopAppsFormatted = string.Join("\n", lines);
        }
        else
        {
            WeekTopAppsFormatted = "Sin actividad destacada";
        }

        OnPropertyChanged(nameof(CanGoToNextWeek));
        OnPropertyChanged(nameof(IsCurrentChartWeek));
        OnPropertyChanged(nameof(CanGoToCurrentWeek));
        OnPropertyChanged(nameof(ChartWeekRelativeLabel));
    }
}
