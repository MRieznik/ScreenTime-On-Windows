using System;
using System.Threading;
using System.Windows;
using ScreenTimeTracker.Services;
using ScreenTimeTracker.ViewModels;

namespace ScreenTimeTracker;

public partial class App : System.Windows.Application
{
    private static Mutex? _mutex;
    private static EventWaitHandle? _wakeUpEvent;
    private static CancellationTokenSource? _wakeUpCts;
    private StorageService? _storageService;
    private BrowserAutomationService? _browserService;
    private WindowTrackerService? _trackerService;
    private MainViewModel? _mainViewModel;
    private TrayIconService? _trayIconService;
    private MainWindow? _mainWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        const string mutexName = "ScreenTimeTracker_SingleInstance_App_Mutex";
        const string eventName = "ScreenTimeTracker_WakeUp_Event";
        _mutex = new Mutex(true, mutexName, out bool isNewInstance);

        if (!isNewInstance)
        {
            // Ya hay una instancia ejecutándose: avisarle que se restaure y terminar esta segunda instancia
            try
            {
                if (EventWaitHandle.TryOpenExisting(eventName, out var existingEvent))
                {
                    existingEvent.Set();
                }
            }
            catch { }

            Shutdown();
            return;
        }

        base.OnStartup(e);

        try
        {
            _wakeUpEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            _wakeUpCts = new CancellationTokenSource();
            var token = _wakeUpCts.Token;

            _ = Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (_wakeUpEvent.WaitOne(1000))
                        {
                            Dispatcher.Invoke(() => _mainWindow?.ShowAndRestore());
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch { }
                }
            }, token);
        }
        catch { }

        _storageService = new StorageService();
        _browserService = new BrowserAutomationService();
        _trackerService = new WindowTrackerService(_storageService, _browserService);
        _mainViewModel = new MainViewModel(_trackerService, _storageService);

        _mainWindow = new MainWindow
        {
            DataContext = _mainViewModel
        };

        _mainWindow.OnSaveRequested += () =>
        {
            _trackerService.SaveCurrentDataSynchronous();
        };

        // Guardar también si Windows se apaga o se cierra sesión
        Microsoft.Win32.SystemEvents.SessionEnding += (s, ev) =>
        {
            _trackerService.SaveCurrentDataSynchronous();
        };

        _trayIconService = new TrayIconService();
        _trayIconService.Initialize();

        _trayIconService.OnOpenRequested += () =>
        {
            Dispatcher.Invoke(() => _mainWindow.ShowAndRestore());
        };

        _trayIconService.OnExitRequested += () =>
        {
            Dispatcher.Invoke(() =>
            {
                _mainWindow.ForceClose();
                Shutdown();
            });
        };

        _mainWindow.OnMinimizeToTray += () =>
        {
            _trayIconService.NotifyMinimized();
        };

        _mainWindow.Show();

        await _mainViewModel.InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _wakeUpCts?.Cancel();
        _wakeUpEvent?.Dispose();
        _wakeUpEvent = null;

        if (_trayIconService != null)
        {
            _trayIconService.Dispose();
            _trayIconService = null;
        }

        if (_trackerService != null)
        {
            _trackerService.StopSynchronous();
        }

        if (_mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch { }
            _mutex.Dispose();
            _mutex = null;
        }

        base.OnExit(e);
    }
}
