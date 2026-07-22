using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;
using WpfPoint = System.Windows.Point;

namespace OrbsWin.Tools;

public partial class ScreenshotWindow : Window
{
    private Bitmap? _fullScreenBitmap;
    private WpfPoint _dragStartPoint;
    private bool _isDragging;
    private bool _isCaptured;
    private readonly bool _saveToFile;
    private readonly string? _customSaveDirectory;

    public ScreenshotWindow(bool saveToFile = false, string? customSaveDirectory = null)
    {
        InitializeComponent();

        _saveToFile = saveToFile;
        _customSaveDirectory = customSaveDirectory;

        // Position window across full Virtual Screen bounds
        Rectangle bounds = SystemInformation.VirtualScreen;
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;

        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyToolWindowStyle();
        CaptureVirtualScreen();
    }

    private void ApplyToolWindowStyle()
    {
        WindowInteropHelper helper = new(this);
        IntPtr hwnd = helper.Handle;

        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_TOOLWINDOW);
    }

    private void CaptureVirtualScreen()
    {
        Rectangle bounds = SystemInformation.VirtualScreen;
        _fullScreenBitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        using (Graphics g = Graphics.FromImage(_fullScreenBitmap))
        {
            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
        }

        ScreenImage.Source = ConvertBitmapToBitmapImage(_fullScreenBitmap);
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

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_isCaptured || e.ChangedButton != MouseButton.Left) return;

        _isDragging = true;
        _dragStartPoint = e.GetPosition(SelectionCanvas);

        Canvas.SetLeft(SelectionBorder, _dragStartPoint.X);
        Canvas.SetTop(SelectionBorder, _dragStartPoint.Y);
        SelectionBorder.Width = 0;
        SelectionBorder.Height = 0;
        SelectionBorder.Visibility = Visibility.Visible;
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging) return;

        WpfPoint currentPoint = e.GetPosition(SelectionCanvas);

        double x = Math.Min(_dragStartPoint.X, currentPoint.X);
        double y = Math.Min(_dragStartPoint.Y, currentPoint.Y);
        double w = Math.Abs(currentPoint.X - _dragStartPoint.X);
        double h = Math.Abs(currentPoint.Y - _dragStartPoint.Y);

        Canvas.SetLeft(SelectionBorder, x);
        Canvas.SetTop(SelectionBorder, y);
        SelectionBorder.Width = w;
        SelectionBorder.Height = h;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging || _isCaptured) return;
        _isDragging = false;

        WpfPoint endPoint = e.GetPosition(SelectionCanvas);

        int x = (int)Math.Min(_dragStartPoint.X, endPoint.X);
        int y = (int)Math.Min(_dragStartPoint.Y, endPoint.Y);
        int w = (int)Math.Abs(endPoint.X - _dragStartPoint.X);
        int h = (int)Math.Abs(endPoint.Y - _dragStartPoint.Y);

        // Require minimum 5x5 region to prevent accidental single clicks
        if (w >= 5 && h >= 5 && _fullScreenBitmap != null)
        {
            _isCaptured = true;
            CropAndProcessScreenshot(x, y, w, h);
        }
        else
        {
            SelectionBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void CropAndProcessScreenshot(int x, int y, int width, int height)
    {
        // Clamp crop rectangle to bitmap dimensions
        x = Math.Clamp(x, 0, _fullScreenBitmap!.Width);
        y = Math.Clamp(y, 0, _fullScreenBitmap.Height);
        width = Math.Min(width, _fullScreenBitmap.Width - x);
        height = Math.Min(height, _fullScreenBitmap.Height - y);

        DrawingRectangle cropRect = new DrawingRectangle(x, y, width, height);
        using Bitmap croppedBmp = _fullScreenBitmap.Clone(cropRect, _fullScreenBitmap.PixelFormat);

        // 1. Copy to clipboard
        try
        {
            BitmapSource bmpSource = ConvertBitmapToBitmapSource(croppedBmp);
            System.Windows.Clipboard.SetImage(bmpSource);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScreenshotWindow] Error setting clipboard image: {ex.Message}");
        }

        // 2. Optional Save to file
        if (_saveToFile)
        {
            SaveScreenshotToFile(croppedBmp);
        }

        // Show Toast
        ToastBorder.Visibility = Visibility.Visible;

        DispatcherTimer closeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(700)
        };
        closeTimer.Tick += (s, args) =>
        {
            closeTimer.Stop();
            Close();
        };
        closeTimer.Start();
    }

    private BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
    {
        BitmapData data = bitmap.LockBits(
            new DrawingRectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            bitmap.PixelFormat);

        try
        {
            return BitmapSource.Create(
                bitmap.Width, bitmap.Height,
                96, 96,
                System.Windows.Media.PixelFormats.Bgra32,
                null,
                data.Scan0,
                data.Stride * bitmap.Height,
                data.Stride);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private void SaveScreenshotToFile(Bitmap bitmap)
    {
        try
        {
            string folder = !string.IsNullOrWhiteSpace(_customSaveDirectory)
                ? _customSaveDirectory
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "OrbsWin Screenshots");

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string filename = $"Screenshot_{DateTime.Now:yyyy-MM-dd_HHmmss}.png";
            string fullPath = Path.Combine(folder, filename);

            bitmap.Save(fullPath, ImageFormat.Png);
            System.Diagnostics.Debug.WriteLine($"[ScreenshotWindow] Saved screenshot to: {fullPath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScreenshotWindow] Failed to save screenshot file: {ex.Message}");
        }
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
        _fullScreenBitmap?.Dispose();
        _fullScreenBitmap = null;
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
