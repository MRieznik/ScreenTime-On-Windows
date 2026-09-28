using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace ScreenTimeTracker.Services;

public class BrowserAutomationService
{
    private static readonly Dictionary<string, string> SupportedBrowsers = new(StringComparer.OrdinalIgnoreCase)
    {
        { "msedge.exe", "Microsoft Edge" },
        { "chrome.exe", "Google Chrome" },
        { "brave.exe", "Brave Browser" },
        { "firefox.exe", "Mozilla Firefox" },
        { "opera.exe", "Opera" },
        { "operagx.exe", "Opera GX" },
        { "vivaldi.exe", "Vivaldi" },
        { "arc.exe", "Arc" },
        { "waterfox.exe", "Waterfox" },
        { "librewolf.exe", "LibreWolf" }
    };

    public bool IsBrowser(string exeName)
    {
        if (string.IsNullOrWhiteSpace(exeName)) return false;
        return SupportedBrowsers.ContainsKey(exeName);
    }

    public string GetBrowserDisplayName(string exeName)
    {
        if (SupportedBrowsers.TryGetValue(exeName, out var name))
        {
            return name;
        }
        return "Navegador Web";
    }

    public string TryGetBrowserUrlOrDomain(IntPtr hWnd, string exeName, string windowTitle)
    {
        if (hWnd == IntPtr.Zero) return ExtractFallbackFromTitle(exeName, windowTitle);

        try
        {
            // UI Automation query with strict 800ms timeout to ensure the tracking loop never blocks
            var task = Task.Run(() => ExtractFromUIAutomation(hWnd));
            if (task.Wait(800) && !string.IsNullOrWhiteSpace(task.Result))
            {
                return task.Result!;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BrowserAutomation] Error UI Automation: {ex.Message}");
        }

        return ExtractFallbackFromTitle(exeName, windowTitle);
    }

    private string? ExtractFromUIAutomation(IntPtr hWnd)
    {
        try
        {
            var element = AutomationElement.FromHandle(hWnd);
            if (element == null) return null;

            // Address bar in modern browsers (Chromium & Gecko) is an Edit control
            var editCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
            var addressBar = element.FindFirst(TreeScope.Descendants, editCondition);

            if (addressBar != null)
            {
                string rawUrl = string.Empty;

                if (addressBar.TryGetCurrentPattern(ValuePattern.Pattern, out object patternObj) &&
                    patternObj is ValuePattern valuePattern)
                {
                    rawUrl = valuePattern.Current.Value;
                }

                if (string.IsNullOrWhiteSpace(rawUrl))
                {
                    rawUrl = addressBar.Current.Name;
                }

                if (!string.IsNullOrWhiteSpace(rawUrl))
                {
                    string domain = CleanAndExtractDomain(rawUrl);
                    if (!string.IsNullOrWhiteSpace(domain))
                    {
                        return domain;
                    }
                }
            }
        }
        catch
        {
            // UI Automation can throw if window changes state or is in a protected context
        }

        return null;
    }

    public static string CleanAndExtractDomain(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        string trimmed = input.Trim();

        // Avoid search queries or non-url text if it doesn't look like a URL
        if (!trimmed.Contains('.') && !trimmed.StartsWith("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "https://" + trimmed;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri) && uri != null)
        {
            string host = uri.Host;
            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                host = host[4..];
            }
            return host.ToLowerInvariant();
        }

        return input.Trim();
    }

    public string ExtractFallbackFromTitle(string exeName, string windowTitle)
    {
        string defaultBrowserName = GetBrowserDisplayName(exeName);
        if (string.IsNullOrWhiteSpace(windowTitle)) return defaultBrowserName;

        string cleaned = windowTitle.Trim();

        // Clean browser-specific suffix patterns
        if (exeName.Equals("msedge.exe", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = Regex.Replace(cleaned, @"\s*-\s*(Personal:\s*|Perfil\s*\d+:\s*)?Microsoft\s*Edge.*$", "", RegexOptions.IgnoreCase);
        }
        else if (exeName.Equals("chrome.exe", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = Regex.Replace(cleaned, @"\s*-\s*Google\s*Chrome.*$", "", RegexOptions.IgnoreCase);
        }
        else if (exeName.Equals("brave.exe", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = Regex.Replace(cleaned, @"\s*-\s*Brave.*$", "", RegexOptions.IgnoreCase);
        }
        else if (exeName.Equals("firefox.exe", StringComparison.OrdinalIgnoreCase) ||
                 exeName.Equals("waterfox.exe", StringComparison.OrdinalIgnoreCase) ||
                 exeName.Equals("librewolf.exe", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = Regex.Replace(cleaned, @"\s*[—\-]\s*Mozilla\s*Firefox.*$|\s*[—\-]\s*Firefox.*$", "", RegexOptions.IgnoreCase);
        }
        else if (exeName.Equals("opera.exe", StringComparison.OrdinalIgnoreCase) ||
                 exeName.Equals("operagx.exe", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = Regex.Replace(cleaned, @"\s*-\s*Opera.*$", "", RegexOptions.IgnoreCase);
        }
        else if (exeName.Equals("vivaldi.exe", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = Regex.Replace(cleaned, @"\s*-\s*Vivaldi.*$", "", RegexOptions.IgnoreCase);
        }
        else if (exeName.Equals("arc.exe", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = Regex.Replace(cleaned, @"\s*-\s*Arc.*$", "", RegexOptions.IgnoreCase);
        }

        cleaned = cleaned.Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? defaultBrowserName : cleaned;
    }
}
