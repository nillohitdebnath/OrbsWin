using System;
using System.Diagnostics;
using System.Windows;
using OrbsWin.Tools;

using DrawingPoint = System.Drawing.Point;

namespace OrbsWin.Services;

public static class ActionExecutor
{
    public static void Execute(WheelItem item, DrawingPoint targetPosition, Action<string, string> notifyCallback, Action toggleCaffeineCallback)
    {
        if (item == null) return;

        Debug.WriteLine($"[ActionExecutor] Executing item '{item.Name}' of type '{item.ItemType}' with value '{item.Value}'");

        switch (item.ItemType.ToLowerInvariant())
        {
            case "builtin_tool":
                ExecuteBuiltinTool(item.Value ?? item.Name, targetPosition, notifyCallback, toggleCaffeineCallback);
                break;

            case "app_launch":
                ExecuteAppLaunch(item.Value);
                break;

            case "shell_command":
                ExecuteShellCommand(item.Value);
                break;

            case "snippet":
                ExecuteSnippet(item.Value);
                break;

            case "submenu":
                // Handled in WheelWindow geometry
                break;

            default:
                Debug.WriteLine($"[ActionExecutor] Unknown item type: {item.ItemType}");
                break;
        }
    }

    private static void ExecuteBuiltinTool(string toolName, DrawingPoint position, Action<string, string> notifyCallback, Action toggleCaffeineCallback)
    {
        switch (toolName.ToLowerInvariant())
        {
            case "color picker":
            case "colorpicker":
                var colorPicker = new ColorPickerWindow(position);
                colorPicker.Show();
                colorPicker.Activate();
                break;

            case "timer":
                var timerWin = new TimerWindow(position, notifyCallback);
                timerWin.Show();
                timerWin.Activate();
                break;

            case "calculator":
                var calcWin = new CalculatorWindow(position);
                calcWin.Show();
                calcWin.Activate();
                break;

            case "clipboard":
            case "clipboard history":
                var clipWin = new ClipboardHistoryWindow(position);
                clipWin.Show();
                clipWin.Activate();
                break;

            case "caffeine":
                toggleCaffeineCallback?.Invoke();
                break;

            default:
                Debug.WriteLine($"[ActionExecutor] Unrecognized built-in tool: {toolName}");
                break;
        }
    }

    private static void ExecuteAppLaunch(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ActionExecutor] App launch failed: {ex.Message}");
        }
    }

    private static void ExecuteShellCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ActionExecutor] Shell command failed: {ex.Message}");
        }
    }

    private static void ExecuteSnippet(string? snippetText)
    {
        if (string.IsNullOrEmpty(snippetText)) return;

        try
        {
            System.Windows.Clipboard.SetText(snippetText);
            Debug.WriteLine($"[ActionExecutor] Snippet copied to clipboard: {snippetText}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ActionExecutor] Snippet copy failed: {ex.Message}");
        }
    }
}
