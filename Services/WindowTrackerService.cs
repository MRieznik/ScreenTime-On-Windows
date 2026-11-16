using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ScreenTimeTracker.Models;

namespace ScreenTimeTracker.Services;

public class WindowTrackerService
{
    public enum TrackingStatus { Waiting, Active, Idle, Paused, Locked, Suspended }

    private sealed record Sample(DateTimeOffset Time, long Timestamp, ActivityRecord? Activity, int Revision);
    private readonly StorageService _storageService;
    private readonly BrowserAutomationService _browserService;
    private readonly object _gate = new();
    private readonly Dictionary<string, DailyTrackingData> _unsavedDays = new();
    private DailyTrackingData _currentDayData = new();
    private CancellationTokenSource? _cts;
    private Task? _trackingTask;
    private TrackingStatus _status;
    private ActivityRecord? _activeActivity;
    private bool _paused;
    private bool _locked;
    private bool _suspended;
    private int _revision;

    public event Action? OnSampleUpdated;

    public TrackerConfig Config { get; private set; } = new();

    public WindowTrackerService(StorageService storageService, BrowserAutomationService browserService)
    {
        _storageService = storageService;
        _browserService = browserService;
    }

    public async Task InitializeAsync()
    {
        Config = await _storageService.LoadConfigAsync();
        var data = await _storageService.LoadDailyDataAsync();
        lock (_gate)
        {
            _currentDayData = data;
        }
    }

    public void Start()
    {
        if (_cts != null) return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _trackingTask = Task.Run(() => TrackingLoopAsync(token));
    }

    public (TrackingStatus Status, ActivityRecord? Activity, DailyTrackingData Day) GetSnapshot()
    {
        lock (_gate)
        {
            var day = new DailyTrackingData { Date = _currentDayData.Date };
            foreach (var entry in _currentDayData.Activities)
            {
                day.Activities.Add(entry.Key, entry.Value.Copy());
            }

            return (_status, _activeActivity?.Copy(), day);
        }
    }

    public void TogglePause()
    {
        lock (_gate)
        {
            _paused = !_paused;
            ResetSample();
        }
        OnSampleUpdated?.Invoke();
    }

    public void SetSessionLocked(bool locked)
    {
        lock (_gate)
        {
            if (_locked == locked) return;
            _locked = locked;
            ResetSample();
        }
        OnSampleUpdated?.Invoke();
    }

    public void SetSuspended(bool suspended)
    {
        lock (_gate)
        {
            if (_suspended == suspended) return;
            _suspended = suspended;
            ResetSample();
        }
        OnSampleUpdated?.Invoke();
    }

    public async Task SetInactivityTimeoutAsync(int minutes)
    {
        lock (_gate)
        {
            Config.InactivityTimeoutMinutes = minutes;
            ResetSample();
        }
        OnSampleUpdated?.Invoke();
        await _storageService.SaveConfigAsync(Config);
    }

    private TrackingStatus GetBlockedStatus()
    {
        if (_paused) return TrackingStatus.Paused;
        if (_locked) return TrackingStatus.Locked;
        if (_suspended) return TrackingStatus.Suspended;
        return TrackingStatus.Waiting;
    }

    private void ResetSample()
    {
        _revision++;
        _activeActivity = null;
        _status = GetBlockedStatus();
    }

    public void SaveCurrentDataSynchronous()
    {
        lock (_gate)
        {
            foreach (var (date, day) in _unsavedDays.ToArray())
            {
                if (_storageService.SaveDailyDataSynchronous(day))
                {
                    _unsavedDays.Remove(date);
                }
            }
        }
    }

    public void StopSynchronous()
    {
        if (_cts == null) return;

        _cts.Cancel();
        try
        {
            _trackingTask?.Wait(1000);
        }
        catch (AggregateException ex)
        {
            Debug.WriteLine($"[Tracker] Error al detener: {ex.Message}");
        }
        _cts.Dispose();
        _cts = null;
        SaveCurrentDataSynchronous();
    }

    private async Task TrackingLoopAsync(CancellationToken ct)
    {
        int interval = Math.Max(1, Config.TrackingIntervalSeconds);
        int tickCount = 0;
        Sample? previous = null;

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(interval));
        try
        {
            do
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    previous = TrackSample(previous, interval, ct);
                    if (++tickCount % 3 == 0)
                    {
                        SaveCurrentDataSynchronous();
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    previous = null;
                    Debug.WriteLine($"[Tracker] Error en el muestreo: {ex.Message}");
                }
            }
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private Sample? TrackSample(Sample? previous, int interval, CancellationToken ct)
    {
        int revision;
        int inactivityMinutes;
        TrackingStatus status;
        lock (_gate)
        {
            revision = _revision;
            inactivityMinutes = Config.InactivityTimeoutMinutes;
            status = GetBlockedStatus();
        }

        var time = DateTimeOffset.Now;
        long timestamp = Stopwatch.GetTimestamp();
        var idleTime = inactivityMinutes > 0 && status == TrackingStatus.Waiting
            ? NativeMethods.GetIdleTime()
            : null;
        var idleExcess = idleTime.GetValueOrDefault() - TimeSpan.FromMinutes(Math.Max(0, inactivityMinutes));
        ActivityRecord? activity = null;
        if (status == TrackingStatus.Waiting)
        {
            if (idleExcess >= TimeSpan.Zero && idleTime.HasValue)
            {
                status = TrackingStatus.Idle;
            }
            else
            {
                activity = ReadForegroundActivity();
                if (activity != null) status = TrackingStatus.Active;
            }
        }

        lock (_gate)
        {
            if (ct.IsCancellationRequested || revision != _revision) return null;

            // El intervalo pertenece a la actividad que estaba en primer plano al comenzar.
            if (previous is { Activity: not null } && previous.Revision == revision &&
                status is TrackingStatus.Active or TrackingStatus.Idle)
            {
                var elapsed = Stopwatch.GetElapsedTime(previous.Timestamp, timestamp);
                var wallElapsed = time - previous.Time;
                if (elapsed.TotalSeconds <= interval * 2.0 &&
                    (wallElapsed - elapsed).Duration() < TimeSpan.FromSeconds(2))
                {
                    long activeEnd = timestamp - (long)(Math.Max(0, idleExcess.TotalSeconds) * Stopwatch.Frequency);
                    int seconds = (int)Math.Max(0, activeEnd / Stopwatch.Frequency - previous.Timestamp / Stopwatch.Frequency);
                    DateTime endTime = time.LocalDateTime.AddSeconds(-Math.Max(0, idleExcess.TotalSeconds));
                    CreditActivity(previous.Activity, previous.Time.LocalDateTime, endTime, seconds);
                }
            }

            SelectDay(time.LocalDateTime.Date);
            _status = status;
            _activeActivity = activity;
        }

        OnSampleUpdated?.Invoke();
        return new Sample(time, timestamp, activity, revision);
    }

    private void CreditActivity(ActivityRecord activity, DateTime startTime, DateTime endTime, int seconds)
    {
        if (seconds == 0) return;

        if (startTime.Date == endTime.Date)
        {
            AddSeconds(activity, endTime, seconds);
            return;
        }

        int secondsToday = Math.Clamp((int)endTime.TimeOfDay.TotalSeconds, 0, seconds);
        AddSeconds(activity, startTime.Date.AddDays(1).AddTicks(-1), seconds - secondsToday);
        AddSeconds(activity, endTime, secondsToday);
    }

    private void AddSeconds(ActivityRecord activity, DateTime endTime, int seconds)
    {
        if (seconds == 0) return;
        SelectDay(endTime.Date);

        var record = activity.Copy();
        record.LastActive = endTime;
        record.SecondsSpent = seconds;
        if (_currentDayData.Activities.TryGetValue(record.Id, out var existing))
        {
            record.SecondsSpent += existing.SecondsSpent;
        }

        _currentDayData.Activities[record.Id] = record;
        _unsavedDays[_currentDayData.Date] = _currentDayData;
    }

    private void SelectDay(DateTime date)
    {
        string dateKey = date.ToString("yyyy-MM-dd");
        if (_currentDayData.Date == dateKey) return;

        // Guardar el día anterior antes de que el historial pase a leerlo desde disco.
        SaveCurrentDataSynchronous();
        _currentDayData = _unsavedDays.TryGetValue(dateKey, out var pending)
            ? pending
            : _storageService.LoadDailyData(dateKey);
    }

    private ActivityRecord? ReadForegroundActivity()
    {
        IntPtr hWnd = NativeMethods.GetForegroundWindow();
        if (hWnd == IntPtr.Zero) return null;

        uint processId = NativeMethods.GetActiveWindowProcessId(hWnd);
        if (processId == 0) return null;

        string processName = string.Empty;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            processName = process.ProcessName;
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(processName)) return null;

        string exeName = processName.ToLowerInvariant();
        if (!exeName.EndsWith(".exe"))
        {
            exeName += ".exe";
        }

        // Obtener ruta completa del ejecutable
        string? exeFullPath = NativeMethods.GetProcessExecutablePath(processId);

        string windowTitle = NativeMethods.GetActiveWindowTitle(hWnd);

        string category;
        string detail;
        string recordKey;

        // Comprobar si es un juego evaluando la ruta completa o lista de juegos
        if (Config.IsGame(exeFullPath, exeName))
        {
            category = "Juegos";
            detail = Config.ExtractGameName(exeFullPath, exeName, windowTitle);
            recordKey = $"game:{Path.GetFileNameWithoutExtension(exeName).ToLowerInvariant()}";
        }
        else if (_browserService.IsBrowser(exeName))
        {
            category = "Navegación Web";
            detail = _browserService.TryGetBrowserUrlOrDomain(hWnd, exeName, windowTitle);
            recordKey = $"web:{detail.ToLowerInvariant()}";
        }
        else
        {
            category = "Aplicaciones de Escritorio";
            detail = CleanAppName(processName, windowTitle);
            recordKey = $"app:{exeName}";
        }

        return new ActivityRecord
        {
            Id = recordKey,
            ProcessName = exeName,
            WindowTitle = windowTitle,
            Category = category,
            Detail = detail
        };
    }

    private static string CleanAppName(string processName, string windowTitle)
    {
        if (processName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(windowTitle) ? "Explorador de Windows" : windowTitle;
        }

        if (processName.Equals("code", StringComparison.OrdinalIgnoreCase))
        {
            return "Visual Studio Code";
        }

        if (processName.Equals("devenv", StringComparison.OrdinalIgnoreCase))
        {
            return "Visual Studio";
        }

        if (processName.Equals("discord", StringComparison.OrdinalIgnoreCase))
        {
            return "Discord";
        }

        if (processName.Equals("spotify", StringComparison.OrdinalIgnoreCase))
        {
            return "Spotify";
        }

        if (processName.Equals("steam", StringComparison.OrdinalIgnoreCase))
        {
            return "Steam (Tienda / Biblioteca)";
        }

        if (processName.Equals("epicgameslauncher", StringComparison.OrdinalIgnoreCase))
        {
            return "Epic Games Launcher";
        }

        if (processName.Equals("battle.net", StringComparison.OrdinalIgnoreCase))
        {
            return "Battle.net";
        }

        if (!string.IsNullOrWhiteSpace(windowTitle) && windowTitle.Length < 40)
        {
            return windowTitle;
        }

        return processName;
    }

    public async Task AddGameDirectoryAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        string clean = path.Trim();
        if (!Config.GameDirectories.Contains(clean))
        {
            Config.GameDirectories.Add(clean);
            await _storageService.SaveConfigAsync(Config);
        }
    }

    public async Task RemoveGameDirectoryAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        string clean = path.Trim();
        if (Config.GameDirectories.Remove(clean))
        {
            await _storageService.SaveConfigAsync(Config);
        }
    }

    public async Task AddGameAsync(string exeName)
    {
        if (string.IsNullOrWhiteSpace(exeName)) return;

        string clean = exeName.Trim().ToLowerInvariant();
        if (!clean.EndsWith(".exe")) clean += ".exe";

        if (!Config.GameExecutables.Contains(clean))
        {
            Config.GameExecutables.Add(clean);
            await _storageService.SaveConfigAsync(Config);
        }
    }

    public async Task RemoveGameAsync(string exeName)
    {
        if (string.IsNullOrWhiteSpace(exeName)) return;

        string clean = exeName.Trim().ToLowerInvariant();
        if (!clean.EndsWith(".exe")) clean += ".exe";

        if (Config.GameExecutables.Remove(clean))
        {
            await _storageService.SaveConfigAsync(Config);
        }
    }
}
