using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

using DrawingPoint = System.Drawing.Point;

namespace OrbsWin.Tools;

public partial class CalculatorWindow : Window
{
    private double _storedValue;
    private string _pendingOperator = string.Empty;
    private bool _isNewEntry = true;

    public CalculatorWindow(DrawingPoint initialPosition)
    {
        InitializeComponent();
        PositionNearCursor(initialPosition);

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

    private void OnDigitClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string digit)
        {
            if (_isNewEntry || CalcDisplay.Text == "0")
            {
                CalcDisplay.Text = digit;
                _isNewEntry = false;
            }
            else
            {
                CalcDisplay.Text += digit;
            }
        }
    }

    private void OnDecimalClick(object sender, RoutedEventArgs e)
    {
        if (_isNewEntry)
        {
            CalcDisplay.Text = "0.";
            _isNewEntry = false;
        }
        else if (!CalcDisplay.Text.Contains("."))
        {
            CalcDisplay.Text += ".";
        }
    }

    private void OnOperatorClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string op)
        {
            if (double.TryParse(CalcDisplay.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double currentVal))
            {
                if (!string.IsNullOrEmpty(_pendingOperator) && !_isNewEntry)
                {
                    ExecutePendingOperation(currentVal);
                }
                else
                {
                    _storedValue = currentVal;
                }
            }

            _pendingOperator = op;
            _isNewEntry = true;
        }
    }

    private void OnEqualsClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_pendingOperator))
        {
            if (double.TryParse(CalcDisplay.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double currentVal))
            {
                ExecutePendingOperation(currentVal);
                _pendingOperator = string.Empty;
                _isNewEntry = true;
            }
        }
    }

    private void ExecutePendingOperation(double currentVal)
    {
        switch (_pendingOperator)
        {
            case "+": _storedValue += currentVal; break;
            case "-": _storedValue -= currentVal; break;
            case "*": _storedValue *= currentVal; break;
            case "/":
                _storedValue = currentVal != 0 ? _storedValue / currentVal : 0;
                break;
            case "%": _storedValue %= currentVal; break;
        }

        CalcDisplay.Text = _storedValue.ToString(CultureInfo.InvariantCulture);
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        _storedValue = 0;
        _pendingOperator = string.Empty;
        _isNewEntry = true;
        CalcDisplay.Text = "0";
    }

    private void OnNegateClick(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(CalcDisplay.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
        {
            val = -val;
            CalcDisplay.Text = val.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void OnBackspaceClick(object sender, RoutedEventArgs e)
    {
        if (_isNewEntry) return;

        if (CalcDisplay.Text.Length > 1)
        {
            CalcDisplay.Text = CalcDisplay.Text[..^1];
        }
        else
        {
            CalcDisplay.Text = "0";
            _isNewEntry = true;
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
