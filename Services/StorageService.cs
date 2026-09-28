using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ScreenTimeTracker.Models;

namespace ScreenTimeTracker.Services;

public class StorageService
{
    private readonly string _dataDirectory;
    private readonly string _configFilePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string DataDirectory => _dataDirectory;

    public StorageService()
    {
        // 1. Intentar usar la carpeta 'registros' dentro del directorio base de instalación / ejecutable
        string preferredDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "registros");
        try
        {
            if (!Directory.Exists(preferredDir))
            {
                Directory.CreateDirectory(preferredDir);
            }

            // Probar permisos reales de escritura
            string testFile = Path.Combine(preferredDir, ".write_test");
            File.WriteAllText(testFile, "ok");
            File.Delete(testFile);

            _dataDirectory = preferredDir;
        }
        catch
        {
            // Fallback de seguridad en AppData local si el directorio de instalación no tiene permisos de escritura (ej. Program Files estricto)
            _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenTimeTracker", "registros");
            if (!Directory.Exists(_dataDirectory))
            {
                Directory.CreateDirectory(_dataDirectory);
            }
        }

        _configFilePath = Path.Combine(_dataDirectory, "config.json");

        // 2. Migración automática de datos existentes desde carpetas 'data' previas
        MigrateLegacyData();
    }

    private void MigrateLegacyData()
    {
        try
        {
            var searchPaths = new List<string>
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "publish", "portable", "data"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "data"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Screen Time Tracker", "data"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenTimeTracker", "data")
            };

            foreach (var candidate in searchPaths)
            {
                try
                {
                    string fullPath = Path.GetFullPath(candidate);
                    if (Directory.Exists(fullPath) && !string.Equals(fullPath, _dataDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var file in Directory.GetFiles(fullPath, "*.json"))
                        {
                            string fileName = Path.GetFileName(file);
                            string destFile = Path.Combine(_dataDirectory, fileName);
                            if (!File.Exists(destFile))
                            {
                                File.Copy(file, destFile, overwrite: false);
                                Debug.WriteLine($"[StorageService] Migrado: {fileName} -> {_dataDirectory}");
                            }
                        }
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageService] Error en migración: {ex.Message}");
        }
    }

    public async Task<TrackerConfig> LoadConfigAsync()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                string json = await File.ReadAllTextAsync(_configFilePath);
                var config = JsonSerializer.Deserialize<TrackerConfig>(json, _jsonOptions);
                if (config != null)
                {
                    if (config.GameDirectories == null || config.GameDirectories.Count == 0)
                    {
                        config.GameDirectories = new TrackerConfig().GameDirectories;
                        await SaveConfigAsync(config);
                    }
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al cargar configuración: {ex.Message}");
        }

        var defaultConfig = new TrackerConfig();
        await SaveConfigAsync(defaultConfig);
        return defaultConfig;
    }

    public async Task SaveConfigAsync(TrackerConfig config)
    {
        try
        {
            string json = JsonSerializer.Serialize(config, _jsonOptions);
            await File.WriteAllTextAsync(_configFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al guardar configuración: {ex.Message}");
        }
    }

    public void SaveConfigSynchronous(TrackerConfig config)
    {
        try
        {
            string json = JsonSerializer.Serialize(config, _jsonOptions);
            File.WriteAllText(_configFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al guardar configuración síncrono: {ex.Message}");
        }
    }

    public async Task<DailyTrackingData> LoadDailyDataAsync(string? dateStr = null)
    {
        string date = dateStr ?? DateTime.Today.ToString("yyyy-MM-dd");
        string filePath = Path.Combine(_dataDirectory, $"screen_time_{date}.json");

        try
        {
            if (File.Exists(filePath))
            {
                string json = await File.ReadAllTextAsync(filePath);
                var data = JsonSerializer.Deserialize<DailyTrackingData>(json, _jsonOptions);
                if (data != null)
                {
                    data.Date = date;
                    data.Activities = data.Activities != null
                        ? new Dictionary<string, ActivityRecord>(data.Activities, StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, ActivityRecord>(StringComparer.OrdinalIgnoreCase);
                    return data;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al cargar datos del día {date}: {ex.Message}");
        }

        return new DailyTrackingData { Date = date };
    }

    public DailyTrackingData LoadDailyData(string? dateStr = null)
    {
        string date = dateStr ?? DateTime.Today.ToString("yyyy-MM-dd");
        string filePath = Path.Combine(_dataDirectory, $"screen_time_{date}.json");

        try
        {
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                var data = JsonSerializer.Deserialize<DailyTrackingData>(json, _jsonOptions);
                if (data != null)
                {
                    data.Date = date;
                    data.Activities = data.Activities != null
                        ? new Dictionary<string, ActivityRecord>(data.Activities, StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, ActivityRecord>(StringComparer.OrdinalIgnoreCase);
                    return data;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al cargar datos del día {date} (síncrono): {ex.Message}");
        }

        return new DailyTrackingData { Date = date };
    }

    public async Task SaveDailyDataAsync(DailyTrackingData data)
    {
        try
        {
            string filePath = Path.Combine(_dataDirectory, $"screen_time_{data.Date}.json");
            string json = JsonSerializer.Serialize(data, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al guardar datos diarios: {ex.Message}");
        }
    }

    public void SaveDailyDataSynchronous(DailyTrackingData data)
    {
        try
        {
            string filePath = Path.Combine(_dataDirectory, $"screen_time_{data.Date}.json");
            string json = JsonSerializer.Serialize(data, _jsonOptions);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al guardar datos diarios síncrono: {ex.Message}");
        }
    }

    public bool HasDailyData(string dateStr)
    {
        string filePath = Path.Combine(_dataDirectory, $"screen_time_{dateStr}.json");
        return File.Exists(filePath);
    }

    public static List<DateTime> GetWeekDates(DateTime referenceDate)
    {
        int diff = (7 + (referenceDate.DayOfWeek - DayOfWeek.Monday)) % 7;
        DateTime monday = referenceDate.Date.AddDays(-diff);
        var dates = new List<DateTime>();
        for (int i = 0; i < 7; i++)
        {
            dates.Add(monday.AddDays(i));
        }
        return dates;
    }

    public async Task<Dictionary<string, DailyTrackingData>> LoadWeekDataAsync(DateTime referenceDate)
    {
        var result = new Dictionary<string, DailyTrackingData>(StringComparer.OrdinalIgnoreCase);
        var weekDates = GetWeekDates(referenceDate);

        foreach (var d in weekDates)
        {
            string dateStr = d.ToString("yyyy-MM-dd");
            var dayData = await LoadDailyDataAsync(dateStr);
            result[dateStr] = dayData;
        }

        return result;
    }
}
