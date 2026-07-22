using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using OrbsWin.Tools;

using DrawingPoint = System.Drawing.Point;

namespace OrbsWin.Services;

public static class ToolWindowManager
{
    private static readonly Dictionary<Type, Window> ActiveToolWindows = new();

    public static void ShowOrFocusToolWindow<T>(Func<T> windowFactory) where T : Window
    {
        Type toolType = typeof(T);

        if (ActiveToolWindows.TryGetValue(toolType, out Window? existingWindow) && existingWindow != null && existingWindow.IsLoaded)
        {
            Debug.WriteLine($"[ToolWindowManager] Bringing existing instance of {toolType.Name} to focus.");
            if (existingWindow.WindowState == WindowState.Minimized)
            {
                existingWindow.WindowState = WindowState.Normal;
            }
            existingWindow.Activate();
            return;
        }

        T newWindow = windowFactory();
        ActiveToolWindows[toolType] = newWindow;

        newWindow.Closed += (s, args) =>
        {
            if (ActiveToolWindows.TryGetValue(toolType, out Window? current) && current == newWindow)
            {
                ActiveToolWindows.Remove(toolType);
            }
        };

        newWindow.Show();
        newWindow.Activate();
    }
}
