using System;
using System.ComponentModel;
using System.Windows;

namespace ScreenTimeTracker;

public partial class MainWindow : Window
{
    private bool _isExplicitExit = false;

    public event Action? OnMinimizeToTray;
    public event Action? OnSaveRequested;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void ShowAndRestore()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        Focus();
    }

    public void ForceClose()
    {
        _isExplicitExit = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        OnSaveRequested?.Invoke();

        if (!_isExplicitExit)
        {
            e.Cancel = true;
            Hide();
            OnMinimizeToTray?.Invoke();
            return;
        }

        base.OnClosing(e);
    }
}
