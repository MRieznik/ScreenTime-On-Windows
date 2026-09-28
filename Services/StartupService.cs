using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ScreenTimeTracker.Services;

public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "ScreenTimeTracker";

    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StartupService] Error al verificar registro: {ex.Message}");
            return false;
        }
    }

    public static bool SetStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null) return false;

            if (enable)
            {
                string directExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ScreenTimeTracker.exe");
                string exePath = File.Exists(directExe) ? directExe : (Environment.ProcessPath ?? directExe);

                key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                if (key.GetValue(AppName) != null)
                {
                    key.DeleteValue(AppName, false);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StartupService] Error al configurar inicio con Windows: {ex.Message}");
            return false;
        }
    }
}
