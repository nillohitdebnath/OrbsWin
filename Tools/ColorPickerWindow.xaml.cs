using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using DrawingPoint = System.Drawing.Point;
using MediaColor = System.Windows.Media.Color;

namespace OrbsWin.Tools;

public partial class ColorPickerWindow : Window
{
    private readonly DispatcherTimer _updateTimer;
    private MediaColor _currentColor;
    private bool _isCopied;

    private const int SampleSize = 11; // 11x11 pixel grid sample

    public ColorPickerWindow(DrawingPoint initialPosition)
    {
        InitializeComponent();

        PositionNearCursor(System.Windows.Forms.Cursor.Position);

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(30) // ~30 fps magnifier update
        };
        _updateTimer.Tick += OnUpdateTimerTick;

        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyToolWindowStyle();
        _updateTimer.Start();
        CaptureScreenAtCursor();
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
        // Position popover window offset by 20px down and right from cursor
        double x = cursorPoint.X + 20;
        double y = cursorPoint.Y + 20;

        // Keep within screen bounds if near right/bottom edges
        var screen = System.Windows.Forms.Screen.FromPoint(cursorPoint);
        if (x + Width > screen.Bounds.Right)
        {
            x = cursorPoint.X - Width - 10;
        }
        if (y + Height > screen.Bounds.Bottom)
        {
            y = cursorPoint.Y - Height - 10;
        }

        Left = x;
        Top = y;
    }

    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        if (_isCopied) return;

        DrawingPoint cursorPoint = System.Windows.Forms.Cursor.Position;
        PositionNearCursor(cursorPoint);
        CaptureScreenAtCursor(cursorPoint);
    }

    private void CaptureScreenAtCursor(DrawingPoint? pos = null)
    {
        DrawingPoint cursorPoint = pos ?? System.Windows.Forms.Cursor.Position;

        using Bitmap bmp = new Bitmap(SampleSize, SampleSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(
                cursorPoint.X - (SampleSize / 2),
                cursorPoint.Y - (SampleSize / 2),
                0, 0,
                new System.Drawing.Size(SampleSize, SampleSize),
                CopyPixelOperation.SourceCopy);
        }

        // Get center pixel color
        System.Drawing.Color centerPixel = bmp.GetPixel(SampleSize / 2, SampleSize / 2);
        _currentColor = MediaColor.FromRgb(centerPixel.R, centerPixel.G, centerPixel.B);

        ColorPreviewBrush.Color = _currentColor;
        HexCodeText.Text = $"#{centerPixel.R:X2}{centerPixel.G:X2}{centerPixel.B:X2}";

        MagnifierImage.Source = ConvertBitmapToBitmapImage(bmp);
    }

    private BitmapImage ConvertBitmapToBitmapImage(Bitmap bitmap)
    {
        using MemoryStream memory = new MemoryStream();
        bitmap.Save(memory, ImageFormat.Png);
        memory.Position = 0;

        BitmapImage bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = memory;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.Freeze();
        return bitmapImage;
    }

    public bool IsPinned { get; private set; }

    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        IsPinned = !IsPinned;
        PinIconText.Opacity = IsPinned ? 1.0 : 0.5;
        Topmost = true;
    }

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        _updateTimer.Stop();
        Close();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        string hexCode = HexCodeText.Text;
        try
        {
            System.Windows.Clipboard.SetText(hexCode);
            ToastText.Text = "Copied to Clipboard!";
            ToastText.Foreground = new SolidColorBrush(MediaColor.FromRgb(0, 230, 120));
        }
        catch (Exception ex)
        {
            ToastText.Text = "Failed to copy";
            System.Diagnostics.Debug.WriteLine($"[ColorPickerWindow] Clipboard error: {ex.Message}");
        }

        if (!IsPinned)
        {
            _isCopied = true;
            _updateTimer.Stop();

            // Close after a brief 600ms toast presentation
            DispatcherTimer closeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(600)
            };
            closeTimer.Tick += (s, args) =>
            {
                closeTimer.Stop();
                Close();
            };
            closeTimer.Start();
        }
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _updateTimer.Stop();
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _updateTimer.Stop();
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
