using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace OrbsWin.Services;

public static class StartupService
{
    private const string AppName = "OrbsWin";
    private const string RunRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsStartWithWindowsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
            object? val = key?.GetValue(AppName);
            return val != null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StartupService] Error reading registry: {ex.Message}");
            return false;
        }
    }

    public static void SetStartWithWindows(bool enable)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
            if (key == null) return;

            if (enable)
            {
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                    Debug.WriteLine($"[StartupService] Enabled autostart: {exePath}");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
                Debug.WriteLine("[StartupService] Disabled autostart.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StartupService] Error writing registry: {ex.Message}");
        }
    }
}
