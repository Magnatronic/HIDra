using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace HIDra.UI.Views;

/// <summary>
/// A small ring beside the cursor that fills while a dwell click counts down, so the
/// student can see a click coming and move away to cancel it.
///
/// It sits just below and to the right of the cursor rather than on it, so it never
/// hides what is being pointed at, and clicks pass straight through it.
/// </summary>
public sealed class DwellRing : Window
{
    private const double Size = 34;
    private const double Thickness = 5;
    private const double OffsetFromCursor = 18;

    private readonly Ellipse _progress;
    private readonly double _dashLength;

    public DwellRing()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        IsHitTestVisible = false;
        Width = Size;
        Height = Size;

        // Dash lengths are measured in stroke thicknesses, so the full circumference in
        // those units is what one complete ring takes.
        _dashLength = Math.PI * (Size - Thickness) / Thickness;

        _progress = new Ellipse
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
            StrokeThickness = Thickness,
            StrokeDashArray = new DoubleCollection { _dashLength, _dashLength },
            StrokeDashOffset = _dashLength,
            RenderTransformOrigin = new Point(0.5, 0.5),
            // Start filling from twelve o'clock
            RenderTransform = new RotateTransform(-90)
        };

        Content = new Grid
        {
            Children =
            {
                new Ellipse
                {
                    Fill = new SolidColorBrush(Color.FromArgb(0xB0, 0x20, 0x20, 0x20)),
                    Stroke = new SolidColorBrush(Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF)),
                    StrokeThickness = Thickness
                },
                _progress
            }
        };

        SourceInitialized += (_, _) =>
        {
            // Never take focus, never catch a click, never appear in Alt+Tab
            var hwnd = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE,
                style | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        };
    }

    /// <summary>
    /// Show the ring by the cursor and fill it over the time left before the click
    /// </summary>
    public void StartCountdown(double seconds)
    {
        if (GetCursorPos(out var cursor))
        {
            // Cursor position is in physical pixels; the window is placed in DIPs
            var dpi = VisualTreeHelper.GetDpi(this);
            Left = cursor.X / dpi.DpiScaleX + OffsetFromCursor;
            Top = cursor.Y / dpi.DpiScaleY + OffsetFromCursor;
        }

        _progress.BeginAnimation(Shape.StrokeDashOffsetProperty, new DoubleAnimation(
            _dashLength, 0, new Duration(TimeSpan.FromSeconds(Math.Max(0.05, seconds)))));

        Show();
    }

    public void StopCountdown()
    {
        _progress.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
        _progress.StrokeDashOffset = _dashLength;
        Hide();
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X, Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
