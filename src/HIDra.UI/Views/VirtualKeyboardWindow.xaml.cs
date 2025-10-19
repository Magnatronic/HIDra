using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using WindowsInput.Native;

namespace HIDra.UI.Views;

/// <summary>
/// Virtual keyboard window for typing with controller
/// </summary>
public partial class VirtualKeyboardWindow : Window
{
    private bool _shiftPressed = false;
    private bool _ctrlPressed = false;
    private bool _capsLockOn = false;

    // Win32 API constants
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    // Event to send key presses back to the main application
    public event EventHandler<VirtualKeyCode>? KeyPressed;
    public event EventHandler<string>? TextEntered;

    public VirtualKeyboardWindow()
    {
        InitializeComponent();
        
        // Prevent window from taking focus
        this.Focusable = false;
        this.ShowActivated = false;
        
        // Set WS_EX_NOACTIVATE style when window is loaded
        this.SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_NOACTIVATE);
        };
        
        // Position window at bottom of screen
        this.WindowStartupLocation = WindowStartupLocation.Manual;
        this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2;
        this.Top = SystemParameters.PrimaryScreenHeight - this.Height - 50;
    }

    private void KeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button) return;
        if (button.Tag is not string keyStr) return;

        // Get the character to type
        string textToType = keyStr;
        
        // Apply shift or caps lock modifier for letters
        if (keyStr.Length == 1 && char.IsLetter(keyStr[0]))
        {
            bool shouldBeUppercase = _capsLockOn ? !_shiftPressed : _shiftPressed;
            textToType = shouldBeUppercase ? keyStr.ToUpper() : keyStr.ToLower();
            
            // Reset shift after use (but not caps lock)
            if (_shiftPressed)
            {
                _shiftPressed = false;
                UpdateModifierButtons();
                UpdateLetterCase();
                UpdateNumberRowSymbols();
            }
        }
        // Handle number row shift symbols
        else if (_shiftPressed && keyStr.Length == 1)
        {
            textToType = keyStr switch
            {
                "1" => "!",
                "2" => "@",
                "3" => "#",
                "4" => "$",
                "5" => "%",
                "6" => "^",
                "7" => "&",
                "8" => "*",
                "9" => "(",
                "0" => ")",
                "-" => "_",
                "=" => "+",
                _ => keyStr
            };
            _shiftPressed = false;
            UpdateModifierButtons();
            UpdateLetterCase();
            UpdateNumberRowSymbols();
        }

        // Send text to be typed
        TextEntered?.Invoke(this, textToType);
    }

    private void BackspaceButton_Click(object sender, RoutedEventArgs e)
    {
        KeyPressed?.Invoke(this, VirtualKeyCode.BACK);
    }

    private void EnterButton_Click(object sender, RoutedEventArgs e)
    {
        KeyPressed?.Invoke(this, VirtualKeyCode.RETURN);
    }

    private void SpaceButton_Click(object sender, RoutedEventArgs e)
    {
        TextEntered?.Invoke(this, " ");
    }

    private void ShiftButton_Click(object sender, RoutedEventArgs e)
    {
        _shiftPressed = !_shiftPressed;
        UpdateModifierButtons();
        UpdateLetterCase();
        UpdateNumberRowSymbols();
    }

    private void CtrlButton_Click(object sender, RoutedEventArgs e)
    {
        _ctrlPressed = !_ctrlPressed;
        UpdateModifierButtons();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Hide();
    }

    private void UpdateLetterCase()
    {
        // Update all letter key displays based on shift and caps lock state
        var letterKeyNames = new[] {
            "KeyQ", "KeyW", "KeyE", "KeyR", "KeyT", "KeyY", "KeyU", "KeyI", "KeyO", "KeyP",
            "KeyA", "KeyS", "KeyD", "KeyF", "KeyG", "KeyH", "KeyJ", "KeyK", "KeyL",
            "KeyZ", "KeyX", "KeyC", "KeyV", "KeyB", "KeyN", "KeyM"
        };

        foreach (var keyName in letterKeyNames)
        {
            var key = this.FindName(keyName) as Button;
            if (key?.Tag is string tag && tag.Length == 1 && char.IsLetter(tag[0]))
            {
                // Caps lock inverts the effect of shift
                bool shouldBeUppercase = _capsLockOn ? !_shiftPressed : _shiftPressed;
                key.Content = shouldBeUppercase ? tag.ToUpper() : tag.ToLower();
            }
        }
    }

    private void UpdateNumberRowSymbols()
    {
        // Update visual emphasis on number keys and symbol keys based on shift state
        var symbolButtonNames = new[] { 
            "Key1", "Key2", "Key3", "Key4", "Key5", "Key6", "Key7", "Key8", "Key9", "Key0", "KeyMinus", "KeyEquals",
            "KeyBracketOpen", "KeyBracketClose", "KeyBackslash", "KeySlash",
            "KeySemicolon", "KeyQuote", "KeyComma", "KeyPeriod"
        };
        
        foreach (var buttonName in symbolButtonNames)
        {
            var button = this.FindName(buttonName) as Button;
            if (button?.Content is System.Windows.Controls.TextBlock textBlock && textBlock.Inlines.Count >= 3)
            {
                // First Run is the shift symbol (top), Third Run is the number (bottom)
                var topRun = textBlock.Inlines.FirstInline as System.Windows.Documents.Run;
                var bottomRun = textBlock.Inlines.LastInline as System.Windows.Documents.Run;
                
                if (topRun != null && bottomRun != null)
                {
                    if (_shiftPressed)
                    {
                        // Shift pressed: emphasize symbol (top), de-emphasize character (bottom)
                        topRun.FontSize = 18;
                        topRun.FontWeight = FontWeights.Bold;
                        topRun.Foreground = System.Windows.Media.Brushes.White;
                        
                        bottomRun.FontSize = 13;
                        bottomRun.FontWeight = FontWeights.Normal;
                        bottomRun.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 204, 204));
                    }
                    else
                    {
                        // Normal: emphasize character (bottom), de-emphasize symbol (top)
                        topRun.FontSize = 13;
                        topRun.FontWeight = FontWeights.Normal;
                        topRun.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 204, 204));
                        
                        bottomRun.FontSize = 18;
                        bottomRun.FontWeight = FontWeights.Bold;
                        bottomRun.Foreground = System.Windows.Media.Brushes.White;
                    }
                }
            }
        }
    }

    private void UpdateModifierButtons()
    {
        var blueColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212));
        var grayColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 74, 74));
        var greenColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));

        // Visual feedback for Shift key
        var shiftKey = this.FindName("ShiftKey") as Button;
        if (shiftKey != null)
        {
            shiftKey.Background = _shiftPressed ? blueColor : grayColor;
        }

        // Visual feedback for Ctrl key
        var ctrlKey = this.FindName("CtrlKey") as Button;
        if (ctrlKey != null)
        {
            ctrlKey.Background = _ctrlPressed ? blueColor : grayColor;
        }

        // Visual feedback for Caps Lock key (green when on)
        var capsLockKey = this.FindName("CapsLockKey") as Button;
        if (capsLockKey != null)
        {
            capsLockKey.Background = _capsLockOn ? greenColor : grayColor;
        }
    }

    // New button handlers
    private void CapsLockButton_Click(object sender, RoutedEventArgs e)
    {
        _capsLockOn = !_capsLockOn;
        UpdateModifierButtons();
        UpdateLetterCase();
    }

    private void TabButton_Click(object sender, RoutedEventArgs e)
    {
        KeyPressed?.Invoke(this, VirtualKeyCode.TAB);
    }

    private void EscapeButton_Click(object sender, RoutedEventArgs e)
    {
        KeyPressed?.Invoke(this, VirtualKeyCode.ESCAPE);
    }

    private void ArrowUpButton_Click(object sender, RoutedEventArgs e)
    {
        KeyPressed?.Invoke(this, VirtualKeyCode.UP);
    }

    private void ArrowDownButton_Click(object sender, RoutedEventArgs e)
    {
        KeyPressed?.Invoke(this, VirtualKeyCode.DOWN);
    }

    private void ArrowLeftButton_Click(object sender, RoutedEventArgs e)
    {
        KeyPressed?.Invoke(this, VirtualKeyCode.LEFT);
    }

    private void ArrowRightButton_Click(object sender, RoutedEventArgs e)
    {
        KeyPressed?.Invoke(this, VirtualKeyCode.RIGHT);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Don't actually close, just hide
        e.Cancel = true;
        this.Hide();
        base.OnClosing(e);
    }
}
