using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace OrbsWin.Services;

public class ClipboardMonitorService : IDisposable
{
    public static ClipboardMonitorService Instance { get; } = new();

    public event EventHandler<string>? ClipboardTextAdded;

    private readonly List<string> _history = new();
    public IReadOnlyList<string> History => _history.AsReadOnly();

    private HwndSource? _hwndSource;
    private bool _isListening;
    private string? _lastCapturedText;

    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private const int MaxEntries = 20;

    private ClipboardMonitorService() { }

    public void Start(Window ownerWindow)
    {
        if (_isListening) return;

        WindowInteropHelper helper = new(ownerWindow);
        IntPtr hwnd = helper.Handle;

        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);

        NativeMethods.AddClipboardFormatListener(hwnd);
        _isListening = true;
    }

    public void Stop()
    {
        if (!_isListening || _hwndSource == null) return;

        NativeMethods.RemoveClipboardFormatListener(_hwndSource.Handle);
        _hwndSource.RemoveHook(WndProc);
        _hwndSource = null;
        _isListening = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE)
        {
            TryCaptureClipboard();
        }
        return IntPtr.Zero;
    }

    private void TryCaptureClipboard()
    {
        try
        {
            if (System.Windows.Clipboard.ContainsText())
            {
                string text = System.Windows.Clipboard.GetText();
                if (!string.IsNullOrWhiteSpace(text) && text != _lastCapturedText)
                {
                    _lastCapturedText = text;

                    // Remove duplicate if exists, then add to front
                    _history.Remove(text);
                    _history.Insert(0, text);

                    if (_history.Count > MaxEntries)
                    {
                        _history.RemoveAt(_history.Count - 1);
                    }

                    ClipboardTextAdded?.Invoke(this, text);
                }
            }
        }
        catch
        {
            // Clipboard access locked by another app
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    }
}
