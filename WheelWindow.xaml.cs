using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

using MediaColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;
using DrawingPoint = System.Drawing.Point;

namespace OrbsWin;

public partial class WheelWindow : Window
{
    public event EventHandler<WheelItem>? ItemSelected;
    public event EventHandler? SelectionCancelled;

    private class MenuLevel
    {
        public List<WheelItem> Items { get; }
        public string Title { get; }

        public MenuLevel(List<WheelItem> items, string title = "")
        {
            Items = items;
            Title = title;
        }
    }

    private readonly Stack<MenuLevel> _levelStack = new();
    private List<WheelItem> _currentItems = new();
    private readonly List<Path> _slicePaths = new();
    private readonly List<TextBlock> _sliceLabels = new();
    
    private int _selectedIndex = -1;
    private bool _isCursorInCenter;

    private readonly DispatcherTimer _dwellTimer;
    private int _dwellTargetIndex = -1;
    private const int DwellThresholdMs = 400;

    private string _searchQuery = string.Empty;

    private const double OuterRadius = 180.0;
    private const double InnerRadius = 45.0;
    private const double CenterX = 200.0;
    private const double CenterY = 200.0;

    private static readonly SolidColorBrush NormalBrush = new(MediaColor.FromArgb(230, 19, 19, 21));
    private static readonly SolidColorBrush HighlightBrush = new(MediaColor.FromArgb(230, 99, 102, 241));
    private static readonly SolidColorBrush StrokeBrush = new(MediaColor.FromArgb(90, 255, 255, 255));
    private static readonly SolidColorBrush TextBrush = new(MediaColor.FromRgb(250, 250, 250));

    private Ellipse? _centerCircle;
    private TextBlock? _centerTextBlock;

    public WheelWindow(DrawingPoint centerScreenPosition, List<WheelItem> items)
    {
        InitializeComponent();

        List<WheelItem> rootItems = items ?? new List<WheelItem>();
        _levelStack.Push(new MenuLevel(rootItems));
        _currentItems = rootItems;

        // Center window on screen point
        Left = centerScreenPosition.X - (Width / 2.0);
        Top = centerScreenPosition.Y - (Height / 2.0);

        _dwellTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(DwellThresholdMs)
        };
        _dwellTimer.Tick += OnDwellTimerTick;

        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewTextInput += OnPreviewTextInput;
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

    private void RenderSlices(bool animate = false)
    {
        _dwellTimer.Stop();
        _dwellTargetIndex = -1;

        WheelCanvas.Children.Clear();
        _slicePaths.Clear();
        _sliceLabels.Clear();
        _selectedIndex = -1;

        int count = _currentItems.Count;
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

            string text = _currentItems[i].Name;
            if (_currentItems[i].Children.Count > 0)
            {
                text += " ▶";
            }

            TextBlock textBlock = new TextBlock
            {
                Text = text,
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

        // Draw inner center circle (acts as back button if in nested menu)
        _centerCircle = new Ellipse
        {
            Width = InnerRadius * 2,
            Height = InnerRadius * 2,
            Fill = new SolidColorBrush(MediaColor.FromArgb(240, 20, 20, 25)),
            Stroke = StrokeBrush,
            StrokeThickness = 1.5,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_centerCircle, CenterX - InnerRadius);
        Canvas.SetTop(_centerCircle, CenterY - InnerRadius);
        WheelCanvas.Children.Add(_centerCircle);

        if (_levelStack.Count > 1)
        {
            _centerTextBlock = new TextBlock
            {
                Text = "◀ Back",
                Foreground = TextBrush,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                IsHitTestVisible = false
            };
            _centerTextBlock.Measure(new WpfSize(double.PositiveInfinity, double.PositiveInfinity));
            WpfSize centerTextSize = _centerTextBlock.DesiredSize;
            Canvas.SetLeft(_centerTextBlock, CenterX - (centerTextSize.Width / 2.0));
            Canvas.SetTop(_centerTextBlock, CenterY - (centerTextSize.Height / 2.0));
            WheelCanvas.Children.Add(_centerTextBlock);
        }

        if (animate)
        {
            AnimateWheelFadeIn();
        }

        ApplyFilter();
    }

    private void AnimateWheelFadeIn()
    {
        DoubleAnimation opacityAnim = new DoubleAnimation(0.2, 1.0, TimeSpan.FromMilliseconds(200));
        ScaleTransform scaleTransform = new ScaleTransform(0.85, 0.85, CenterX, CenterY);
        WheelCanvas.RenderTransform = scaleTransform;

        DoubleAnimation scaleAnim = new DoubleAnimation(0.85, 1.0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        WheelCanvas.BeginAnimation(OpacityProperty, opacityAnim);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
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

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text)) return;

        char c = e.Text[0];
        if (!char.IsControl(c))
        {
            _searchQuery += e.Text;
            UpdateSearchUI();
            e.Handled = true;
        }
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Back)
        {
            if (_searchQuery.Length > 0)
            {
                _searchQuery = _searchQuery[..^1];
                UpdateSearchUI();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape)
        {
            if (_searchQuery.Length > 0)
            {
                _searchQuery = string.Empty;
                UpdateSearchUI();
                e.Handled = true;
            }
            else
            {
                SelectionCancelled?.Invoke(this, EventArgs.Empty);
                Close();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter)
        {
            if (_selectedIndex >= 0 && _selectedIndex < _currentItems.Count)
            {
                WheelItem selected = _currentItems[_selectedIndex];
                if (selected.Children.Count > 0)
                {
                    BranchIntoChildLevel(selected);
                }
                else
                {
                    ItemSelected?.Invoke(this, selected);
                    Close();
                }
                e.Handled = true;
            }
        }
    }

    private void UpdateSearchUI()
    {
        if (string.IsNullOrEmpty(_searchQuery))
        {
            SearchBoxBorder.Visibility = Visibility.Collapsed;
            SearchQueryText.Text = string.Empty;
        }
        else
        {
            SearchBoxBorder.Visibility = Visibility.Visible;
            SearchQueryText.Text = _searchQuery;
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (_slicePaths.Count != _currentItems.Count) return;

        List<int> matchingIndices = new();

        for (int i = 0; i < _currentItems.Count; i++)
        {
            bool matches = string.IsNullOrEmpty(_searchQuery) ||
                           _currentItems[i].Name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase);

            if (matches)
            {
                matchingIndices.Add(i);
                _slicePaths[i].Opacity = 1.0;
                _sliceLabels[i].Opacity = 1.0;
            }
            else
            {
                _slicePaths[i].Opacity = 0.2;
                _sliceLabels[i].Opacity = 0.25;
            }
        }

        // If search filters down to exactly 1 match, auto-highlight it
        if (matchingIndices.Count == 1)
        {
            UpdateHighlight(matchingIndices[0]);
        }
        else if (matchingIndices.Count == 0)
        {
            UpdateHighlight(-1);
        }
    }

    private void OnWindowMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        WpfPoint cursorPos = e.GetPosition(WheelCanvas);
        double dx = cursorPos.X - CenterX;
        double dy = cursorPos.Y - CenterY;

        double distance = Math.Sqrt((dx * dx) + (dy * dy));

        int newIndex = -1;
        _isCursorInCenter = distance < InnerRadius;

        if (distance >= InnerRadius && distance <= OuterRadius && _currentItems.Count > 0)
        {
            // Calculate angle relative to 12 o'clock (-90 degrees)
            double angleRad = Math.Atan2(dy, dx);
            double angleDeg = angleRad * (180.0 / Math.PI); // -180 to 180
            
            // Normalize to 0..360 starting from 12 o'clock
            double normalizedAngle = (angleDeg + 90.0 + 360.0) % 360.0;

            double anglePerSlice = 360.0 / _currentItems.Count;
            newIndex = (int)(normalizedAngle / anglePerSlice);

            if (newIndex >= _currentItems.Count)
            {
                newIndex = _currentItems.Count - 1;
            }
        }

        UpdateHighlight(newIndex);
    }

    private void UpdateHighlight(int index)
    {
        // Highlight center back button if cursor is in center and sub-level active
        if (_centerCircle != null)
        {
            if (_isCursorInCenter && _levelStack.Count > 1)
            {
                _centerCircle.Fill = HighlightBrush;
            }
            else
            {
                _centerCircle.Fill = new SolidColorBrush(MediaColor.FromArgb(240, 20, 20, 25));
            }
        }

        if (_selectedIndex == index) return;

        if (_selectedIndex >= 0 && _selectedIndex < _slicePaths.Count)
        {
            _slicePaths[_selectedIndex].Fill = NormalBrush;
        }

        _selectedIndex = index;

        if (_selectedIndex >= 0 && _selectedIndex < _slicePaths.Count)
        {
            _slicePaths[_selectedIndex].Fill = HighlightBrush;

            // Dwell timer check for branching into children
            WheelItem targetItem = _currentItems[_selectedIndex];
            if (targetItem.Children.Count > 0)
            {
                _dwellTargetIndex = _selectedIndex;
                _dwellTimer.Stop();
                _dwellTimer.Start();
            }
            else
            {
                _dwellTimer.Stop();
                _dwellTargetIndex = -1;
            }
        }
        else
        {
            _dwellTimer.Stop();
            _dwellTargetIndex = -1;
        }
    }

    private void OnDwellTimerTick(object? sender, EventArgs e)
    {
        _dwellTimer.Stop();

        if (_dwellTargetIndex >= 0 && _dwellTargetIndex == _selectedIndex && _dwellTargetIndex < _currentItems.Count)
        {
            WheelItem item = _currentItems[_dwellTargetIndex];
            if (item.Children.Count > 0)
            {
                BranchIntoChildLevel(item);
            }
        }
    }

    private void BranchIntoChildLevel(WheelItem item)
    {
        _searchQuery = string.Empty;
        UpdateSearchUI();

        _levelStack.Push(new MenuLevel(item.Children, item.Name));
        _currentItems = item.Children;
        RenderSlices(animate: true);
    }

    private bool NavigateBackLevel()
    {
        if (_levelStack.Count > 1)
        {
            _searchQuery = string.Empty;
            UpdateSearchUI();

            _levelStack.Pop();
            _currentItems = _levelStack.Peek().Items;
            RenderSlices(animate: true);
            return true;
        }
        return false;
    }

    private void OnWindowMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Dead center click inside a nested submenu goes back up one level
        if (_isCursorInCenter && _levelStack.Count > 1)
        {
            NavigateBackLevel();
            return;
        }

        if (_selectedIndex >= 0 && _selectedIndex < _currentItems.Count)
        {
            WheelItem item = _currentItems[_selectedIndex];

            if (item.Children.Count > 0)
            {
                // Immediate click on parent item branches into child menu
                BranchIntoChildLevel(item);
                return;
            }

            ItemSelected?.Invoke(this, item);
        }
        else
        {
            SelectionCancelled?.Invoke(this, EventArgs.Empty);
        }

        Close();
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
