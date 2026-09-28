using System;
using System.Collections.Generic;
using System.IO;

namespace ScreenTimeTracker.Models;

public class TrackerConfig
{
    public int TrackingIntervalSeconds { get; set; } = 5;
    public bool StartWithWindows { get; set; } = false;

    /// <summary>
    /// Rutas de bibliotecas de juegos (Steam, Battle.net, Epic, Hytale, etc.)
    /// </summary>
    public List<string> GameDirectories { get; set; } = new()
    {
        @"C:\Program Files (x86)\Steam\steamapps\common",
        @"D:\Juegos\BattleNet",
        @"D:\Juegos\Epic",
        @"D:\Juegos\Hytale",
        @"D:\Juegos\SteamLibrary\steamapps\common"
    };

    /// <summary>
    /// Lista de ejecutables individuales como respaldo adicional
    /// </summary>
    public List<string> GameExecutables { get; set; } = new()
    {
        "overwatch.exe",
        "hl2.exe",
        "cs2.exe",
        "valorant.exe",
        "valorant-win64-shipping.exe",
        "league of legends.exe",
        "fortniteclient-win64-shipping.exe",
        "minecraft.exe",
        "javaw.exe",
        "eldenring.exe"
    };

    /// <summary>
    /// Clientes o launchers que residen en esas carpetas pero deben tratarse como Apps de Escritorio
    /// </summary>
    public List<string> IgnoredLaunchers { get; set; } = new()
    {
        "steam.exe",
        "steamwebhelper.exe",
        "epicgameslauncher.exe",
        "battle.net.exe",
        "riotclientservices.exe",
        "galaxyclient.exe",
        "upc.exe"
    };

    public bool IsGame(string? fullExePath, string processName)
    {
        string normExe = NormalizeExe(processName);

        // Ignorar clientes/tiendas
        foreach (var launcher in IgnoredLaunchers)
        {
            if (string.Equals(normExe, NormalizeExe(launcher), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // 1. Detección por ruta de directorio de biblioteca
        if (!string.IsNullOrWhiteSpace(fullExePath))
        {
            string cleanFullPath = Path.GetFullPath(fullExePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            foreach (var gameDir in GameDirectories)
            {
                if (string.IsNullOrWhiteSpace(gameDir)) continue;

                string cleanDir = Path.GetFullPath(gameDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Comprobar si el archivo está contenido en la carpeta (ej. D:\Juegos\Epic\Game\game.exe)
                if (cleanFullPath.StartsWith(cleanDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    cleanFullPath.StartsWith(cleanDir + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        // 2. Detección por lista de ejecutables de respaldo
        foreach (var game in GameExecutables)
        {
            if (string.Equals(normExe, NormalizeExe(game), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public string ExtractGameName(string? fullExePath, string processName, string windowTitle)
    {
        // Si tenemos la ruta completa y está dentro de una biblioteca, intentamos extraer la carpeta del juego
        if (!string.IsNullOrWhiteSpace(fullExePath))
        {
            string cleanFullPath = Path.GetFullPath(fullExePath);

            foreach (var gameDir in GameDirectories)
            {
                if (string.IsNullOrWhiteSpace(gameDir)) continue;

                string cleanDir = Path.GetFullPath(gameDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

                if (cleanFullPath.StartsWith(cleanDir, StringComparison.OrdinalIgnoreCase))
                {
                    string relative = cleanFullPath.Substring(cleanDir.Length);
                    string[] parts = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                    {
                        return parts[0]; // Nombre de la carpeta principal del juego
                    }
                }
            }
        }

        // Si el título de la ventana no está vacío y es diferente al ejecutable, usarlo
        if (!string.IsNullOrWhiteSpace(windowTitle) && !windowTitle.Equals(processName, StringComparison.OrdinalIgnoreCase))
        {
            return windowTitle.Trim();
        }

        string withoutExt = Path.GetFileNameWithoutExtension(processName);
        return char.ToUpper(withoutExt[0]) + withoutExt[1..];
    }

    private static string NormalizeExe(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        string trimmed = input.Trim().ToLowerInvariant();
        if (!trimmed.EndsWith(".exe")) trimmed += ".exe";
        return trimmed;
    }
}
