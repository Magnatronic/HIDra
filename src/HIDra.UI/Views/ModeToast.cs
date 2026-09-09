using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace HIDra.UI.Views;

/// <summary>
/// A brief on-screen message confirming a mode change.
///
/// The stick-swap on Y silently changes what both sticks do. The engine has always
/// announced it, but nothing listened, so the only way to discover the swap was to push
/// a stick and see the wrong thing happen - disorienting, and easy to trigger by
/// accident. Feedback has to appear on screen rather than in the HIDra window, because
/// that window is normally hidden.
///
/// The window never takes focus and is click-through, so it cannot interrupt whatever
/// the user is doing or swallow a click aimed at what is underneath it.
/// </summary>
public sealed class ModeToast : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private readonly TextBlock _text;
    private readonly DispatcherTimer _hideTimer;

    public ModeToast()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowActivated = false;
        Focusable = false;
        Topmost = true;
        ShowInTaskbar = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;

        _text = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 28,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center
        };

        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x1E, 0x1E, 0x1E)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(32, 20, 32, 20),
            Child = _text
        };

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            Hide();
        };

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE,
                style | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
        };
    }

    /// <summary>
    /// Show a message for a couple of seconds, centred horizontally and set low on the
    /// screen so it does not cover what the user is working on.
    /// </summary>
    public void ShowMessage(string message)
    {
        _text.Text = message;

        // Ensure the layout is measured before positioning, or the first toast of a
        // session is placed using a stale size.
        Show();
        UpdateLayout();

        Left = (SystemParameters.PrimaryScreenWidth - ActualWidth) / 2;
        Top = SystemParameters.PrimaryScreenHeight * 0.75;

        _hideTimer.Stop();
        _hideTimer.Start();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _hideTimer.Stop();
        base.OnClosing(e);
    }
}
