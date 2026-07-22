using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

using MediaColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;
using DrawingPoint = System.Drawing.Point;

namespace OrbsWin;

public partial class WheelWindow : Window
{
    public event EventHandler<WheelItem>? ItemSelected;
    public event EventHandler? SelectionCancelled;

    private readonly List<WheelItem> _items;
    private readonly List<Path> _slicePaths = new();
    private readonly List<TextBlock> _sliceLabels = new();
    
    private int _selectedIndex = -1;

    private const double OuterRadius = 180.0;
    private const double InnerRadius = 45.0;
    private const double CenterX = 200.0;
    private const double CenterY = 200.0;

    private static readonly SolidColorBrush NormalBrush = new(MediaColor.FromArgb(210, 30, 30, 40));
    private static readonly SolidColorBrush HighlightBrush = new(MediaColor.FromArgb(240, 0, 122, 204));
    private static readonly SolidColorBrush StrokeBrush = new(MediaColor.FromArgb(180, 255, 255, 255));
    private static readonly SolidColorBrush TextBrush = new(Colors.White);

    public WheelWindow(DrawingPoint centerScreenPosition, List<WheelItem> items)
    {
        InitializeComponent();

        _items = items ?? new List<WheelItem>();

        // Center window on screen point
        Left = centerScreenPosition.X - (Width / 2.0);
        Top = centerScreenPosition.Y - (Height / 2.0);

        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
        MouseMove += OnWindowMouseMove;
        MouseLeftButtonUp += OnWindowMouseLeftButtonUp;

        RenderSlices();
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

    private void RenderSlices()
    {
        WheelCanvas.Children.Clear();
        _slicePaths.Clear();
        _sliceLabels.Clear();

        int count = _items.Count;
        if (count == 0) return;

        double anglePerSlice = 360.0 / count;

        for (int i = 0; i < count; i++)
        {
            double startAngle = (i * anglePerSlice) - 90.0; // Start at 12 o'clock
            double endAngle = startAngle + anglePerSlice;

            Geometry sliceGeometry = CreateSliceGeometry(startAngle, endAngle);

            Path path = new Path
            {
                Data = sliceGeometry,
                Fill = NormalBrush,
                Stroke = StrokeBrush,
                StrokeThickness = 1.5,
                Tag = i
            };

            WheelCanvas.Children.Add(path);
            _slicePaths.Add(path);

            // Add text label centered in slice
            double midAngle = startAngle + (anglePerSlice / 2.0);
            double midRad = midAngle * (Math.PI / 180.0);
            double labelRadius = (InnerRadius + OuterRadius) / 2.0;

            double labelX = CenterX + (labelRadius * Math.Cos(midRad));
            double labelY = CenterY + (labelRadius * Math.Sin(midRad));

            TextBlock textBlock = new TextBlock
            {
                Text = _items[i].Name,
                Foreground = TextBrush,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                IsHitTestVisible = false
            };

            textBlock.Measure(new WpfSize(double.PositiveInfinity, double.PositiveInfinity));
            WpfSize size = textBlock.DesiredSize;

            Canvas.SetLeft(textBlock, labelX - (size.Width / 2.0));
            Canvas.SetTop(textBlock, labelY - (size.Height / 2.0));

            WheelCanvas.Children.Add(textBlock);
            _sliceLabels.Add(textBlock);
        }

        // Draw inner center circle decoration
        Ellipse centerCircle = new Ellipse
        {
            Width = InnerRadius * 2,
            Height = InnerRadius * 2,
            Fill = new SolidColorBrush(MediaColor.FromArgb(240, 20, 20, 25)),
            Stroke = StrokeBrush,
            StrokeThickness = 1.5,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(centerCircle, CenterX - InnerRadius);
        Canvas.SetTop(centerCircle, CenterY - InnerRadius);
        WheelCanvas.Children.Add(centerCircle);
    }

    private Geometry CreateSliceGeometry(double startAngleDegrees, double endAngleDegrees)
    {
        double startRad = startAngleDegrees * (Math.PI / 180.0);
        double endRad = endAngleDegrees * (Math.PI / 180.0);

        WpfPoint pOuterStart = new WpfPoint(CenterX + (OuterRadius * Math.Cos(startRad)), CenterY + (OuterRadius * Math.Sin(startRad)));
        WpfPoint pOuterEnd = new WpfPoint(CenterX + (OuterRadius * Math.Cos(endRad)), CenterY + (OuterRadius * Math.Sin(endRad)));
        WpfPoint pInnerEnd = new WpfPoint(CenterX + (InnerRadius * Math.Cos(endRad)), CenterY + (InnerRadius * Math.Sin(endRad)));
        WpfPoint pInnerStart = new WpfPoint(CenterX + (InnerRadius * Math.Cos(startRad)), CenterY + (InnerRadius * Math.Sin(startRad)));

        bool isLargeArc = (endAngleDegrees - startAngleDegrees) > 180.0;

        PathFigure figure = new PathFigure
        {
            StartPoint = pOuterStart,
            IsClosed = true,
            IsFilled = true
        };

        figure.Segments.Add(new ArcSegment(pOuterEnd, new WpfSize(OuterRadius, OuterRadius), 0, isLargeArc, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(pInnerEnd, true));
        figure.Segments.Add(new ArcSegment(pInnerStart, new WpfSize(InnerRadius, InnerRadius), 0, isLargeArc, SweepDirection.Counterclockwise, true));

        PathGeometry geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private void OnWindowMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        WpfPoint cursorPos = e.GetPosition(WheelCanvas);
        double dx = cursorPos.X - CenterX;
        double dy = cursorPos.Y - CenterY;

        double distance = Math.Sqrt((dx * dx) + (dy * dy));

        int newIndex = -1;

        if (distance >= InnerRadius && distance <= OuterRadius && _items.Count > 0)
        {
            // Calculate angle relative to 12 o'clock (-90 degrees)
            double angleRad = Math.Atan2(dy, dx);
            double angleDeg = angleRad * (180.0 / Math.PI); // -180 to 180
            
            // Normalize to 0..360 starting from 12 o'clock
            double normalizedAngle = (angleDeg + 90.0 + 360.0) % 360.0;

            double anglePerSlice = 360.0 / _items.Count;
            newIndex = (int)(normalizedAngle / anglePerSlice);

            if (newIndex >= _items.Count)
            {
                newIndex = _items.Count - 1;
            }
        }

        UpdateHighlight(newIndex);
    }

    private void UpdateHighlight(int index)
    {
        if (_selectedIndex == index) return;

        if (_selectedIndex >= 0 && _selectedIndex < _slicePaths.Count)
        {
            _slicePaths[_selectedIndex].Fill = NormalBrush;
        }

        _selectedIndex = index;

        if (_selectedIndex >= 0 && _selectedIndex < _slicePaths.Count)
        {
            _slicePaths[_selectedIndex].Fill = HighlightBrush;
        }
    }

    private void OnWindowMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
        {
            WheelItem item = _items[_selectedIndex];
            ItemSelected?.Invoke(this, item);
        }
        else
        {
            SelectionCancelled?.Invoke(this, EventArgs.Empty);
        }

        Close();
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            SelectionCancelled?.Invoke(this, EventArgs.Empty);
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
