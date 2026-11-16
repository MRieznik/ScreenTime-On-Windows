using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ScreenTimeTracker;

public partial class MainWindow : Window
{
    private bool _isExplicitExit = false;
    private IInputElement? _focusBeforeOverlay;

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

    private void OverlayVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not FrameworkElement overlay) return;

        if (overlay.IsVisible)
        {
            _focusBeforeOverlay = Keyboard.FocusedElement;
            _ = Dispatcher.InvokeAsync(() =>
            {
                if (overlay.IsVisible) overlay.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }, DispatcherPriority.Input);
        }
        else if (_focusBeforeOverlay is { } previousFocus)
        {
            _focusBeforeOverlay = null;
            Keyboard.Focus(previousFocus);
        }
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
