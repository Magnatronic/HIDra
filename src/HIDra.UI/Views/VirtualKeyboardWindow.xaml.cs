using System;
using System.Collections.Generic;
using HIDra.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;

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
    public event EventHandler<VirtualKey>? KeyPressed;
    public event EventHandler<string>? TextEntered;

    /// <summary>
    /// Raised for a modifier shortcut such as Ctrl+C, where the keys must be sent as
    /// virtual keys rather than as typed text.
    /// </summary>
    public event EventHandler<VirtualKey[]>? KeyComboPressed;

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

        // Ctrl is handled before anything else, because a shortcut needs the key sent
        // as a virtual key rather than as typed text. Previously Ctrl only lit up the
        // button and was never consulted here, so every shortcut it promised - Ctrl+C,
        // Ctrl+V, Ctrl+S - silently did nothing.
        if (_ctrlPressed && keyStr.Length == 1 && char.IsLetterOrDigit(keyStr[0]))
        {
            // Letters and digits use their ASCII uppercase value as a virtual-key code.
            var key = (VirtualKey)char.ToUpperInvariant(keyStr[0]);

            var combo = _shiftPressed
                ? new[] { VirtualKey.Control, VirtualKey.Shift, key }
                : new[] { VirtualKey.Control, key };

            KeyComboPressed?.Invoke(this, combo);

            // Both modifiers are one-shot, matching how Shift already behaved.
            _ctrlPressed = false;
            _shiftPressed = false;
            UpdateModifierButtons();
            UpdateLetterCase();
            UpdateNumberRowSymbols();
            return;
        }

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
        // Shifted symbols, using the UK layout the keys are labelled with.
        //
        // These were previously the US symbols, so the key drawn with a pound sign
        // typed "#" and the key drawn with a double quote typed "@". The punctuation
        // keys had no shift mapping at all, so Shift+; produced ";" rather than ":".
        else if (_shiftPressed && keyStr.Length == 1)
        {
            textToType = keyStr switch
            {
                "1" => "!",
                "2" => "\"",
                "3" => "£",   // pound sign
                "4" => "$",
                "5" => "%",
                "6" => "^",
                "7" => "&",
                "8" => "*",
                "9" => "(",
                "0" => ")",
                "-" => "_",
                "=" => "+",
                "[" => "{",
                "]" => "}",
                ";" => ":",
                "'" => "@",
                "#" => "~",
                "," => "<",
                "." => ">",
                "/" => "?",
                "\\" => "|",
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
        KeyPressed?.Invoke(this, VirtualKey.Back);
    }

    private void EnterButton_Click(object sender, RoutedEventArgs e)
    {
        KeyPressed?.Invoke(this, VirtualKey.Return);
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
        KeyPressed?.Invoke(this, VirtualKey.Tab);
    }

    private void EscapeButton_Click(object sender, RoutedEventArgs e)
    {
        KeyPressed?.Invoke(this, VirtualKey.Escape);
    }

    private void ArrowUpButton_Click(object sender, RoutedEventArgs e)
    {
        KeyPressed?.Invoke(this, VirtualKey.Up);
    }

    private void ArrowDownButton_Click(object sender, RoutedEventArgs e)
    {
        KeyPressed?.Invoke(this, VirtualKey.Down);
    }

    private void ArrowLeftButton_Click(object sender, RoutedEventArgs e)
    {
        KeyPressed?.Invoke(this, VirtualKey.Left);
    }

    private void ArrowRightButton_Click(object sender, RoutedEventArgs e)
    {
        KeyPressed?.Invoke(this, VirtualKey.Right);
    }

    // ---------------------------------------------------------------------------
    // Direct key navigation
    //
    // A highlight is moved from key to key and confirmed with a single button, instead
    // of driving the mouse cursor onto each key. Keys are found by their real position
    // on screen rather than from a hand-written grid, because the rows are ragged - the
    // bottom row in particular - and a geometric search handles that without a table
    // that would silently rot whenever the layout changed.
    // ---------------------------------------------------------------------------

    private readonly List<Button> _navigableKeys = new();
    private Button? _highlightedKey;
    private Brush? _highlightSavedBorderBrush;
    private Thickness _highlightSavedBorderThickness;

    /// <summary>
    /// Only the border is recoloured for the highlight. Modifier keys show their state
    /// through their background, so leaving backgrounds alone means the two cannot
    /// fight over the same key.
    /// </summary>
    private static readonly Brush HighlightBrush =
        new SolidColorBrush(Color.FromRgb(0xFF, 0xD4, 0x00));

    private const double HighlightBorderThickness = 4;

    /// <summary>
    /// Move the highlight one key in the given direction.
    /// </summary>
    public void MoveHighlight(KeyboardNavigationDirection direction)
    {
        EnsureKeysCollected();

        if (_navigableKeys.Count == 0)
        {
            return;
        }

        if (_highlightedKey == null)
        {
            SetHighlight(_navigableKeys[0]);
            return;
        }

        var from = CentreOf(_highlightedKey);
        Button? best = null;
        double bestScore = double.MaxValue;

        foreach (var candidate in _navigableKeys)
        {
            if (ReferenceEquals(candidate, _highlightedKey))
            {
                continue;
            }

            var to = CentreOf(candidate);
            double dx = to.X - from.X;
            double dy = to.Y - from.Y;

            // Distance along the direction of travel, and how far off-axis the
            // candidate sits. Off-axis distance is weighted so movement stays in the
            // row or column the user is travelling along wherever possible.
            double along;
            double across;

            switch (direction)
            {
                case KeyboardNavigationDirection.Left:
                    if (dx >= -1) continue;
                    along = -dx; across = Math.Abs(dy) * 3; break;
                case KeyboardNavigationDirection.Right:
                    if (dx <= 1) continue;
                    along = dx; across = Math.Abs(dy) * 3; break;
                case KeyboardNavigationDirection.Up:
                    if (dy >= -1) continue;
                    along = -dy; across = Math.Abs(dx) * 1.5; break;
                default:
                    if (dy <= 1) continue;
                    along = dy; across = Math.Abs(dx) * 1.5; break;
            }

            double score = along + across;

            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        // No candidate means the edge of the keyboard. Staying put is better than
        // wrapping somewhere unexpected: the highlight is where the user last left it.
        if (best != null)
        {
            SetHighlight(best);
        }
    }

    /// <summary>
    /// Press the highlighted key, exactly as clicking it would.
    /// </summary>
    public void ActivateHighlight()
    {
        EnsureKeysCollected();

        if (_highlightedKey == null)
        {
            if (_navigableKeys.Count == 0)
            {
                return;
            }

            SetHighlight(_navigableKeys[0]);
            return;
        }

        // Raise the button's own Click so every existing behaviour - shift, caps lock,
        // the Ctrl shortcut path, backspace, the arrow keys - runs unchanged.
        _highlightedKey.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    }

    /// <summary>
    /// Press the highlighted key as though Shift were held, giving the symbol printed
    /// above it - or the capital, for a letter.
    ///
    /// This exists so the shifted character costs one button instead of two journeys
    /// across the keyboard to the Shift key and back. Shift is applied only for this
    /// one press: it behaves as a momentary modifier rather than a latch, so the state
    /// afterwards is always the same no matter which key was pressed.
    /// </summary>
    public void ActivateHighlightShifted()
    {
        EnsureKeysCollected();

        if (_highlightedKey == null)
        {
            if (_navigableKeys.Count > 0)
            {
                SetHighlight(_navigableKeys[0]);
            }

            return;
        }

        _shiftPressed = true;

        try
        {
            _highlightedKey.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }
        finally
        {
            // Always end unshifted, whether or not the key that ran consumed it. Keys
            // such as Backspace and Enter ignore Shift entirely and would otherwise
            // leave it stuck on for the following keystroke.
            _shiftPressed = false;
            UpdateModifierButtons();
            UpdateLetterCase();
            UpdateNumberRowSymbols();
        }
    }

    /// <summary>Is a key currently highlighted?</summary>
    public bool HasHighlight => _highlightedKey != null;

    /// <summary>
    /// What the highlighted key is, for diagnostics. Prefers the key's own label so it
    /// reads the way the user sees it.
    /// </summary>
    public string? HighlightedKeyDescription
    {
        get
        {
            if (_highlightedKey == null)
            {
                return null;
            }

            if (_highlightedKey.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
            {
                return tag;
            }

            return _highlightedKey.Name is { Length: > 0 } name
                ? name
                : _highlightedKey.Content?.ToString();
        }
    }

    private void SetHighlight(Button key)
    {
        ClearHighlight();

        _highlightSavedBorderBrush = key.BorderBrush;
        _highlightSavedBorderThickness = key.BorderThickness;

        key.BorderBrush = HighlightBrush;
        key.BorderThickness = new Thickness(HighlightBorderThickness);

        _highlightedKey = key;
    }

    private void ClearHighlight()
    {
        if (_highlightedKey == null)
        {
            return;
        }

        _highlightedKey.BorderBrush = _highlightSavedBorderBrush;
        _highlightedKey.BorderThickness = _highlightSavedBorderThickness;
        _highlightedKey = null;
    }

    private void EnsureKeysCollected()
    {
        if (_navigableKeys.Count > 0)
        {
            return;
        }

        // Layout must have run at least once or every key reports a zero size and the
        // geometric search has nothing to work with.
        UpdateLayout();
        CollectKeys(this);
    }

    private void CollectKeys(DependencyObject parent)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);

        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is Button button && button.ActualWidth > 0 && button.ActualHeight > 0)
            {
                _navigableKeys.Add(button);
            }

            CollectKeys(child);
        }
    }

    private Point CentreOf(Button key)
    {
        var topLeft = key.TransformToAncestor(this).Transform(new Point(0, 0));
        return new Point(topLeft.X + key.ActualWidth / 2, topLeft.Y + key.ActualHeight / 2);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Don't actually close, just hide
        e.Cancel = true;
        this.Hide();
        base.OnClosing(e);
    }
}
