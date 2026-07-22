using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace OrbsWin.Services;

public class WheelConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string ConfigFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OrbsWin", "config.json");

    public static List<WheelItem> LoadConfig()
    {
        try
        {
            string filePath = ConfigFilePath;
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                List<WheelItem>? items = JsonSerializer.Deserialize<List<WheelItem>>(json, JsonOptions);
                if (items != null && items.Count > 0)
                {
                    return items;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WheelConfigService] Error loading config.json: {ex.Message}");
        }

        // Fallback to default config and save it
        List<WheelItem> defaultConfig = GetDefaultConfig();
        SaveConfig(defaultConfig);
        return defaultConfig;
    }

    public static void SaveConfig(List<WheelItem> items)
    {
        try
        {
            string filePath = ConfigFilePath;
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(items, JsonOptions);
            File.WriteAllText(filePath, json);
            Debug.WriteLine($"[WheelConfigService] Config saved to: {filePath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WheelConfigService] Error saving config.json: {ex.Message}");
        }
    }

    public static List<WheelItem> GetDefaultConfig()
    {
        return new List<WheelItem>
        {
            new WheelItem("Calculator", "builtin_tool", "Calculator"),
            new WheelItem("Timer", "builtin_tool", "Timer"),
            new WheelItem("Clipboard", "builtin_tool", "Clipboard"),
            new WheelItem("Color Picker", "builtin_tool", "Color Picker"),
            new WheelItem("Caffeine", "builtin_tool", "Caffeine"),
            new WheelItem("Notes", "submenu", null, null, new List<WheelItem>
            {
                new WheelItem("New Note", "snippet", "TODO: Write note here..."),
                new WheelItem("Recent Notes", "snippet", "Recent notes placeholder"),
                new WheelItem("Search Notes", "shell_command", "echo Searching notes...")
            }),
            new WheelItem("Settings", "builtin_tool", "Settings")
        };
    }
}
