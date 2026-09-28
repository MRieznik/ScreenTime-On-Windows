using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ScreenTimeTracker.Services;

public class TrayIconService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private bool _hasShownBalloon = false;

    public event Action? OnOpenRequested;
    public event Action? OnExitRequested;

    public void Initialize()
    {
        if (_notifyIcon != null) return;

        Icon? appIcon = LoadAppIcon();

        var contextMenu = new ContextMenuStrip();

        var openMenuItem = new ToolStripMenuItem("Abrir Screen Time Tracker");
        openMenuItem.Font = new Font(openMenuItem.Font, FontStyle.Bold);
        openMenuItem.Click += (s, e) => OnOpenRequested?.Invoke();
        contextMenu.Items.Add(openMenuItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var exitMenuItem = new ToolStripMenuItem("Salir completamente");
        exitMenuItem.Click += (s, e) => OnExitRequested?.Invoke();
        contextMenu.Items.Add(exitMenuItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = appIcon ?? SystemIcons.Application,
            Text = "Screen Time Tracker - Activo en segundo plano",
            Visible = true,
            ContextMenuStrip = contextMenu
        };

        _notifyIcon.DoubleClick += (s, e) => OnOpenRequested?.Invoke();
        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                OnOpenRequested?.Invoke();
            }
        };
    }

    public void NotifyMinimized()
    {
        if (_notifyIcon == null || _hasShownBalloon) return;

        try
        {
            _notifyIcon.ShowBalloonTip(
                2500,
                "Screen Time Tracker",
                "La aplicación continúa rastreando en segundo plano. Haz clic en este icono para abrirla de nuevo.",
                ToolTipIcon.Info
            );
            _hasShownBalloon = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TrayIcon] Error al mostrar globo informativo: {ex.Message}");
        }
    }

    private static Icon? LoadAppIcon()
    {
        try
        {
            // 1. Intentar cargar desde Assets/app_icon.ico en el directorio base
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string icoPath = Path.Combine(baseDir, "Assets", "app_icon.ico");
            if (File.Exists(icoPath))
            {
                return new Icon(icoPath);
            }

            // 2. Intentar buscar en el directorio del proyecto
            string parentIco = Path.Combine(baseDir, "..", "..", "..", "Assets", "app_icon.ico");
            if (File.Exists(parentIco))
            {
                return new Icon(parentIco);
            }

            // 3. Extraer el icono asociado al ejecutable
            string? processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
            {
                return Icon.ExtractAssociatedIcon(processPath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TrayIcon] Error al cargar icono: {ex.Message}");
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
        GC.SuppressFinalize(this);
    }
}
