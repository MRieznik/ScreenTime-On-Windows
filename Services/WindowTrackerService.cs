using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ScreenTimeTracker.Models;

namespace ScreenTimeTracker.Services;

public class WindowTrackerService
{
    private readonly StorageService _storageService;
    private readonly BrowserAutomationService _browserService;
    private TrackerConfig _config;
    private DailyTrackingData _currentDayData;
    private CancellationTokenSource? _cts;
    private Task? _trackingTask;
    private int _tickCount = 0;

    public event Action<ActivityRecord, DailyTrackingData>? OnActivityTracked;
    public event Action<bool>? OnTrackingStateChanged;

    public bool IsTracking => _cts != null && !_cts.IsCancellationRequested;
    public TrackerConfig Config => _config;
    public DailyTrackingData CurrentDayData => _currentDayData;

    public WindowTrackerService(StorageService storageService, BrowserAutomationService browserService)
    {
        _storageService = storageService;
        _browserService = browserService;
        _config = new TrackerConfig();
        _currentDayData = new DailyTrackingData();
    }

    public async Task InitializeAsync()
    {
        _config = await _storageService.LoadConfigAsync();
        _currentDayData = await _storageService.LoadDailyDataAsync();
    }

    public void Start()
    {
        if (IsTracking) return;

        _cts = new CancellationTokenSource();
        _trackingTask = Task.Run(() => TrackingLoopAsync(_cts.Token));
        OnTrackingStateChanged?.Invoke(true);
    }

    public void SaveCurrentDataSynchronous()
    {
        lock (_currentDayData.Activities)
        {
            _storageService.SaveDailyDataSynchronous(_currentDayData);
        }
    }

    public void StopSynchronous()
    {
        if (_cts == null) return;

        try
        {
            _cts.Cancel();
            _trackingTask?.Wait(500);
        }
        catch { }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SaveCurrentDataSynchronous();
            OnTrackingStateChanged?.Invoke(false);
        }
    }

    public async Task StopAsync()
    {
        if (_cts == null) return;

        _cts.Cancel();
        if (_trackingTask != null)
        {
            try
            {
                await _trackingTask;
            }
            catch (OperationCanceledException) { }
        }

        _cts.Dispose();
        _cts = null;

        SaveCurrentDataSynchronous();
        OnTrackingStateChanged?.Invoke(false);
    }

    private async Task TrackingLoopAsync(CancellationToken ct)
    {
        int interval = Math.Max(1, _config.TrackingIntervalSeconds);

        // Comprobación inicial inmediata para reflejar la ventana activa sin esperar 5s
        try
        {
            TrackCurrentWindow(0);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Tracker] Error en tick inicial: {ex.Message}");
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(interval));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(ct);
                TrackCurrentWindow(interval);

                _tickCount++;
                // Guardar en JSON cada 15 segundos (3 ticks de 5s)
                if (_tickCount % 3 == 0)
                {
                    await _storageService.SaveDailyDataAsync(_currentDayData);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Tracker] Error en bucle: {ex.Message}");
            }
        }
    }

    private void TrackCurrentWindow(int secondsToAdd)
    {
        // Comprobar cambio de día
        string todayStr = DateTime.Today.ToString("yyyy-MM-dd");
        if (_currentDayData.Date != todayStr)
        {
            SaveCurrentDataSynchronous();
            _currentDayData = new DailyTrackingData { Date = todayStr };
        }

        IntPtr hWnd = NativeMethods.GetForegroundWindow();
        if (hWnd == IntPtr.Zero) return;

        uint processId = NativeMethods.GetActiveWindowProcessId(hWnd);
        if (processId == 0) return;

        string processName = string.Empty;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            processName = process.ProcessName;
        }
        catch
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(processName)) return;

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
        if (_config.IsGame(exeFullPath, exeName))
        {
            category = "Juegos";
            detail = _config.ExtractGameName(exeFullPath, exeName, windowTitle);
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

        lock (_currentDayData.Activities)
        {
            if (!_currentDayData.Activities.TryGetValue(recordKey, out var record))
            {
                record = new ActivityRecord
                {
                    Id = recordKey,
                    ProcessName = exeName,
                    WindowTitle = windowTitle,
                    Category = category,
                    Detail = detail,
                    SecondsSpent = 0,
                    LastActive = DateTime.Now
                };
                _currentDayData.Activities[recordKey] = record;
            }

            record.SecondsSpent += secondsToAdd;
            record.LastActive = DateTime.Now;
            record.WindowTitle = windowTitle;
            if (!string.IsNullOrWhiteSpace(detail))
            {
                record.Detail = detail;
            }

            OnActivityTracked?.Invoke(record, _currentDayData);
        }
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
        if (!_config.GameDirectories.Contains(clean))
        {
            _config.GameDirectories.Add(clean);
            await _storageService.SaveConfigAsync(_config);
        }
    }

    public async Task RemoveGameDirectoryAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        string clean = path.Trim();
        if (_config.GameDirectories.Remove(clean))
        {
            await _storageService.SaveConfigAsync(_config);
        }
    }

    public async Task AddGameAsync(string exeName)
    {
        if (string.IsNullOrWhiteSpace(exeName)) return;

        string clean = exeName.Trim().ToLowerInvariant();
        if (!clean.EndsWith(".exe")) clean += ".exe";

        if (!_config.GameExecutables.Contains(clean))
        {
            _config.GameExecutables.Add(clean);
            await _storageService.SaveConfigAsync(_config);
        }
    }

    public async Task RemoveGameAsync(string exeName)
    {
        if (string.IsNullOrWhiteSpace(exeName)) return;

        string clean = exeName.Trim().ToLowerInvariant();
        if (!clean.EndsWith(".exe")) clean += ".exe";

        if (_config.GameExecutables.Remove(clean))
        {
            await _storageService.SaveConfigAsync(_config);
        }
    }
}
