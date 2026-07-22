using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

using DrawingPoint = System.Drawing.Point;

namespace OrbsWin.Tools;

public partial class TimerWindow : Window
{
    private readonly DispatcherTimer _timer;
    private int _remainingSeconds;
    private bool _isRunning;
    private readonly Action<string, string> _notifyCallback;

    public TimerWindow(DrawingPoint initialPosition, Action<string, string> notifyCallback)
    {
        InitializeComponent();
        _notifyCallback = notifyCallback;

        PositionNearCursor(initialPosition);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTimerTick;

        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyToolWindowStyle();
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

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_remainingSeconds > 0)
        {
            _remainingSeconds--;
            UpdateDisplay();
        }

        if (_remainingSeconds <= 0)
        {
            _timer.Stop();
            _isRunning = false;
            StartPauseButton.Content = "Start";
            
            _notifyCallback?.Invoke("Timer Complete!", "Your OrbsWin countdown timer has hit zero.");
        }
    }

    private void UpdateDisplay()
    {
        TimeSpan ts = TimeSpan.FromSeconds(_remainingSeconds);
        TimerDisplay.Text = $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    private void OnPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int seconds))
        {
            _remainingSeconds = seconds;
            UpdateDisplay();
        }
    }

    private void OnStartPauseClick(object sender, RoutedEventArgs e)
    {
        if (_remainingSeconds <= 0) return;

        if (_isRunning)
        {
            _timer.Stop();
            _isRunning = false;
            StartPauseButton.Content = "Start";
        }
        else
        {
            _timer.Start();
            _isRunning = true;
            StartPauseButton.Content = "Pause";
        }
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _isRunning = false;
        _remainingSeconds = 0;
        StartPauseButton.Content = "Start";
        UpdateDisplay();
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

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
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
