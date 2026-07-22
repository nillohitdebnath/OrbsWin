using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using OrbsWin.Services;

using DrawingPoint = System.Drawing.Point;

namespace OrbsWin.Tools;

public partial class ClipboardHistoryWindow : Window
{
    public ClipboardHistoryWindow(DrawingPoint initialPosition)
    {
        InitializeComponent();
        PositionNearCursor(initialPosition);

        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyToolWindowStyle();
        LoadHistory();
    }

    private void ApplyToolWindowStyle()
    {
        WindowInteropHelper helper = new(this);
        IntPtr hwnd = helper.Handle;

        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_TOOLWINDOW);
    }

    private void PositionNearCursor(DrawingPoint cursorPoint)
    {
        double x = cursorPoint.X + 20;
        double y = cursorPoint.Y + 20;

        var screen = System.Windows.Forms.Screen.FromPoint(cursorPoint);
        if (x + Width > screen.Bounds.Right) x = cursorPoint.X - Width - 10;
        if (y + Height > screen.Bounds.Bottom) y = cursorPoint.Y - Height - 10;

        Left = x;
        Top = y;
    }

    private void LoadHistory()
    {
        IReadOnlyList<string> history = ClipboardMonitorService.Instance.History;
        HistoryListBox.ItemsSource = history;
    }

    private void OnHistoryItemClick(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryListBox.SelectedItem is string selectedText)
        {
            try
            {
                System.Windows.Clipboard.SetText(selectedText);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ClipboardHistoryWindow] Error setting clipboard: {ex.Message}");
            }
            Close();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private static class NativeMethods
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
