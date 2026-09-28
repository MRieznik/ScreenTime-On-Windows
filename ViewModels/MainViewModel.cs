using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ScreenTimeTracker.Models;
using ScreenTimeTracker.Services;

namespace ScreenTimeTracker.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly WindowTrackerService _trackerService;
    private readonly StorageService _storageService;

    private bool _isTracking;
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

    private string _activeAppText = "Esperando detección...";
    private string _activeCategory = "Inactivo";
    private string _activeCategoryIcon = "⏸️";

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
                OnPropertyChanged(nameof(ViewModeBadge));
                OnPropertyChanged(nameof(DateNavigationLabel));
                _ = LoadDateDataAsync(_selectedDate);
            }
        }
    }

    public bool IsViewingToday => _selectedDate.Date == DateTime.Today;
    public bool CanGoToNextDay => _selectedDate.Date < DateTime.Today;
    public string ViewModeBadge => IsViewingToday ? "EN VIVO" : "HISTORIAL";

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
    private string _chartColor = "#6366F1";
    private string _chartColorBg = "#232048";
    private string _weekTotalFormatted = "00m 00s";
    private string _weekAverageFormatted = "00m 00s / día";
    private string _weekPeakDay = "Sin actividad";

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

    public string ChartColor
    {
        get => _chartColor;
        set => SetField(ref _chartColor, value);
    }

    public string ChartColorBg
    {
        get => _chartColorBg;
        set => SetField(ref _chartColorBg, value);
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

    public bool HasNoActivities
    {
        get => _hasNoActivities;
        set => SetField(ref _hasNoActivities, value);
    }

    public bool IsTracking
    {
        get => _isTracking;
        set
        {
            if (SetField(ref _isTracking, value))
            {
                OnPropertyChanged(nameof(TrackingStatusText));
                OnPropertyChanged(nameof(TrackingStatusColor));
            }
        }
    }

    public string TrackingStatusText => IsTracking ? "Rastreando en vivo" : "Rastreo en pausa";
    public string TrackingStatusColor => IsTracking ? "#10B981" : "#F59E0B";

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
        set => SetField(ref _activeAppText, value);
    }

    public string ActiveCategory
    {
        get => _activeCategory;
        set => SetField(ref _activeCategory, value);
    }

    public string ActiveCategoryIcon
    {
        get => _activeCategoryIcon;
        set => SetField(ref _activeCategoryIcon, value);
    }

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

        OpenChartCommand = new RelayCommand(async param => await OpenChartExecuteAsync(param as string));
        CloseChartCommand = new RelayCommand(() => IsChartOpen = false);
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
            if (param is DayBarItem bar)
            {
                SelectedDate = bar.Date;
                IsChartOpen = false;
            }
            else if (param is DateTime dt)
            {
                SelectedDate = dt;
                IsChartOpen = false;
            }
        });

        _trackerService.OnActivityTracked += HandleActivityTracked;
        _trackerService.OnTrackingStateChanged += state =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() => IsTracking = state);
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
        if (date.Date == DateTime.Today)
        {
            _viewedDayData = _trackerService.CurrentDayData;
        }
        else
        {
            _viewedDayData = await _storageService.LoadDailyDataAsync(date.ToString("yyyy-MM-dd"));
        }

        UpdateStatsFromData(_viewedDayData);
        RefreshDisplayList();
    }

    public async Task InitializeAsync()
    {
        await _trackerService.InitializeAsync();
        UpdateConfigLists();
        StartWithWindows = StartupService.IsStartupEnabled();
        _viewedDayData = _trackerService.CurrentDayData;
        UpdateDateString();
        UpdateStatsFromData(_viewedDayData);
        RefreshDisplayList();
        _trackerService.Start();
        IsTracking = _trackerService.IsTracking;
    }

    private void HandleActivityTracked(ActivityRecord current, DailyTrackingData data)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            ActiveAppText = string.IsNullOrWhiteSpace(current.Detail) ? current.ProcessName : current.Detail;
            ActiveCategory = current.Category;
            ActiveCategoryIcon = current.Category switch
            {
                "Juegos" => "🎮",
                "Navegación Web" or "Navegación Web (Edge)" => "☁",
                _ => "📁"
            };

            // Solo actualizar las tarjetas y tabla en pantalla si el usuario está visualizando HOY
            if (IsViewingToday)
            {
                _viewedDayData = data;
                UpdateStatsFromData(data);
                RefreshDisplayList();
            }

            if (IsChartOpen && IsCurrentChartWeek)
            {
                _ = RefreshChartDataAsync();
            }
        });
    }

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
        var activeData = _viewedDayData ?? _trackerService.CurrentDayData;
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

    private async Task OpenChartExecuteAsync(string? category)
    {
        _chartReferenceDate = DateTime.Today;
        SelectedChartCategory = string.IsNullOrWhiteSpace(category) ? "Total" : category;
        await RefreshChartDataAsync();
        IsChartOpen = true;
    }

    private async Task SwitchChartCategoryExecuteAsync(string? category)
    {
        SelectedChartCategory = string.IsNullOrWhiteSpace(category) ? "Total" : category;
        await RefreshChartDataAsync();
    }

    public async Task RefreshChartDataAsync()
    {
        var weekDates = StorageService.GetWeekDates(_chartReferenceDate);
        var weekData = await _storageService.LoadWeekDataAsync(_chartReferenceDate);

        // Inyectar datos en vivo del día actual en memoria solo si estamos viendo la semana en curso
        if (IsCurrentChartWeek)
        {
            string todayStr = DateTime.Today.ToString("yyyy-MM-dd");
            weekData[todayStr] = _trackerService.CurrentDayData;
        }

        switch (SelectedChartCategory)
        {
            case "Juegos":
                ChartTitle = "Distribución Semanal: Juegos";
                ChartIcon = "🎮";
                ChartColor = "#2DD4BF";
                ChartColorBg = "#132E2B";
                break;
            case "Aplicaciones de Escritorio":
                ChartTitle = "Distribución Semanal: Escritorio";
                ChartIcon = "📁";
                ChartColor = "#2DD4BF";
                ChartColorBg = "#132E2B";
                break;
            case "Navegación Web":
            case "Navegación Web (Edge)":
                ChartTitle = "Distribución Semanal: Navegación Web";
                ChartIcon = "☁";
                ChartColor = "#2DD4BF";
                ChartColorBg = "#132E2B";
                break;
            default:
                ChartTitle = "Distribución Semanal: Todo";
                ChartIcon = "⏱";
                ChartColor = "#2DD4BF";
                ChartColorBg = "#132E2B";
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
            if (weekData.TryGetValue(dateStr, out var daily))
            {
                sec = SelectedChartCategory switch
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
        const double maxBarHeightPx = 180.0;

        int totalWeekSec = daySecondsList.Sum();
        WeekTotalFormatted = DailyTrackingData.FormatSeconds(totalWeekSec);
        WeekAverageFormatted = $"{DailyTrackingData.FormatSeconds(totalWeekSec / 7)} / día";

        int peakSec = -1;
        string peakDayName = "Sin actividad";

        string[] dayNames = { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };
        string[] dayShortNames = { "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom" };

        var newBars = new List<DayBarItem>();
        for (int i = 0; i < 7; i++)
        {
            var date = weekDates[i];
            int sec = daySecondsList[i];
            bool isToday = date.Date == DateTime.Today;

            double ratio = (double)sec / scaleMax;
            double barHeight = sec > 0 ? Math.Max(12.0, ratio * maxBarHeightPx) : 4.0;

            if (sec > peakSec && sec > 0)
            {
                peakSec = sec;
                peakDayName = $"{dayNames[i]} ({DailyTrackingData.FormatSeconds(sec)})";
            }

            newBars.Add(new DayBarItem
            {
                DayName = dayNames[i],
                DayShort = dayShortNames[i],
                DateString = date.ToString("dd MMM", culture),
                Date = date,
                Seconds = sec,
                BarHeight = barHeight,
                IsToday = isToday,
                BarColor = ChartColor,
                BarBgColor = isToday ? "#283045" : "#1A202E"
            });
        }

        WeekPeakDay = peakDayName;

        WeekBars.Clear();
        foreach (var b in newBars)
        {
            WeekBars.Add(b);
        }

        OnPropertyChanged(nameof(CanGoToNextWeek));
        OnPropertyChanged(nameof(IsCurrentChartWeek));
        OnPropertyChanged(nameof(CanGoToCurrentWeek));
        OnPropertyChanged(nameof(ChartWeekRelativeLabel));
    }
}
