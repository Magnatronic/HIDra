using System;
using System.Collections.Generic;
using System.Linq;
using HIDra.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
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
    private const int WS_EX_TRANSPARENT = 0x00000020;

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

        _fadeIdleTimer.Tick += (_, _) => FadeIdleElapsed();
        _fadeStepTimer.Tick += (_, _) => FadeStep();
        
        // Position window at bottom of screen
        this.WindowStartupLocation = WindowStartupLocation.Manual;
        MoveToEdge(atTop: false);

        // While hidden the student may click somewhere else entirely, so whatever was
        // being typed before is no longer the text next to the cursor.
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                StartFresh();
                NotifyActivity();
            }
            else
            {
                ResetPrediction();
                StopDwell();
                _fadeIdleTimer.Stop();
            }
        };

        _dwellTimer.Tick += (_, _) =>
        {
            StopDwell();
            ActivateHighlight();
        };

        _dwellRingDelay.Tick += (_, _) => ShowDwellRing();
    }

    /// <summary>
    /// Move the keyboard to the top or bottom of the screen, so it does not sit over
    /// the text the student is typing into.
    /// </summary>
    public void MoveToEdge(bool atTop)
    {
        var workArea = SystemParameters.WorkArea;
        this.Left = workArea.Left + (workArea.Width - this.Width) / 2;
        this.Top = atTop ? workArea.Top + 10 : workArea.Bottom - this.Height - 10;
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

            // A shortcut such as Ctrl+V or Ctrl+Z changes the text in ways the
            // keyboard cannot follow, so the current word is no longer known.
            ResetPrediction();

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
            bool shouldBeUppercase = (_capsLockOn || CapitaliseNextLetter) ? !_shiftPressed : _shiftPressed;
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
        RaiseTextEntered(textToType);
    }

    private void BackspaceButton_Click(object sender, RoutedEventArgs e)
    {
        RaiseKeyPressed(VirtualKey.Back);
    }

    private void EnterButton_Click(object sender, RoutedEventArgs e)
    {
        RaiseKeyPressed(VirtualKey.Return);
    }

    private void SpaceButton_Click(object sender, RoutedEventArgs e)
    {
        RaiseTextEntered(" ");
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
                bool shouldBeUppercase = (_capsLockOn || CapitaliseNextLetter) ? !_shiftPressed : _shiftPressed;
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
            "KeySemicolon", "KeyQuote", "KeyHash", "KeyComma", "KeyPeriod"
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
        // The resting colour has to be the one FunctionKeyStyle paints, or a modifier key
        // quietly changes shade the first time its state is refreshed.
        var grayColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(86, 86, 86));
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
        RaiseKeyPressed(VirtualKey.Tab);
    }

    private void EscapeButton_Click(object sender, RoutedEventArgs e)
    {
        RaiseKeyPressed(VirtualKey.Escape);
    }

    private void WindowsKeyButton_Click(object sender, RoutedEventArgs e)
    {
        RaiseKeyPressed(VirtualKey.LeftWindows);
    }

    // Home and End matter more here than on an ordinary keyboard: without them the only
    // way to reach the start or end of a line is to press an arrow key once per
    // character, and every one of those presses costs a deliberate movement.
    private void HomeButton_Click(object sender, RoutedEventArgs e)
    {
        RaiseKeyPressed(VirtualKey.Home);
    }

    private void EndButton_Click(object sender, RoutedEventArgs e)
    {
        RaiseKeyPressed(VirtualKey.End);
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        RaiseKeyPressed(VirtualKey.Delete);
    }

    private void PageUpButton_Click(object sender, RoutedEventArgs e)
    {
        RaiseKeyPressed(VirtualKey.PageUp);
    }

    private void PageDownButton_Click(object sender, RoutedEventArgs e)
    {
        RaiseKeyPressed(VirtualKey.PageDown);
    }

    private void ArrowUpButton_Click(object sender, RoutedEventArgs e)
    {
        RaiseKeyPressed(VirtualKey.Up);
    }

    private void ArrowDownButton_Click(object sender, RoutedEventArgs e)
    {
        RaiseKeyPressed(VirtualKey.Down);
    }

    private void ArrowLeftButton_Click(object sender, RoutedEventArgs e)
    {
        RaiseKeyPressed(VirtualKey.Left);
    }

    private void ArrowRightButton_Click(object sender, RoutedEventArgs e)
    {
        RaiseKeyPressed(VirtualKey.Right);
    }

    // ---------------------------------------------------------------------------
    // Word prediction, automatic capitals and phrases
    //
    // The keyboard cannot read the document being typed into, so it follows what it
    // has typed itself: the word in progress and the few before it. Anything that
    // moves the text cursor or edits in ways it cannot follow - arrows, Home, Tab, a
    // Ctrl shortcut, hiding the keyboard - starts afresh rather than guessing.
    // ---------------------------------------------------------------------------

    private const int SuggestionCount = 6;
    private const int ContextWords = 3;

    private readonly WordPredictor _predictor = new();
    private readonly List<string> _previousWords = new();
    private readonly string?[] _suggestions = new string?[SuggestionCount];
    private string _currentWord = "";
    private int _suggestionRequest;

    /// <summary>
    /// Capitalise the first letter of a sentence, and "i" on its own
    /// </summary>
    public bool AutoCapitalise { get; set; } = true;

    /// <summary>
    /// The student's own phrases, offered in place of the suggestions by the Phrases key
    /// </summary>
    public IReadOnlyList<string> Phrases { get; set; } = Array.Empty<string>();

    // Whether the next letter starts a sentence, and whether the word in progress did -
    // so backspacing a whole first word brings the capital back.
    private bool _atSentenceStart;
    private bool _wordBeganSentence;
    private bool _showingPhrases;

    private bool CapitaliseNextLetter => AutoCapitalise && _atSentenceStart && _currentWord.Length == 0;

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '\'';

    private void RaiseTextEntered(string text)
    {
        // Phrases are a one-off choice, not a mode: typing anything means words again.
        // Otherwise one accidental press - easy with type-by-resting - left the row
        // showing phrases while the student typed on, looking as if prediction had died.
        _showingPhrases = false;

        // "i" on its own, or starting a contraction such as i'm or i'll, is always a
        // capital. It is only known once the word ends, so it is corrected then.
        if (AutoCapitalise && text.Length > 0 && !IsWordChar(text[0]) &&
            (_currentWord == "i" || _currentWord.StartsWith("i'")))
        {
            for (int i = 0; i < _currentWord.Length; i++)
            {
                KeyPressed?.Invoke(this, VirtualKey.Back);
            }

            _currentWord = "I" + _currentWord[1..];
            TextEntered?.Invoke(this, _currentWord);
        }

        foreach (char c in text)
        {
            if (IsWordChar(c))
            {
                if (_currentWord.Length == 0)
                {
                    _wordBeganSentence = _atSentenceStart;
                }

                _currentWord += c;
                _atSentenceStart = false;
            }
            else
            {
                EndWord(c);
            }
        }

        TextEntered?.Invoke(this, text);
        AfterContextChanged();
    }

    private void RaiseKeyPressed(VirtualKey key)
    {
        _showingPhrases = false;

        switch (key)
        {
            case VirtualKey.Back:
                // Backspace inside a word is easy to follow. Past its start, the
                // keyboard would be guessing what is now before the cursor.
                if (_currentWord.Length > 0)
                {
                    _currentWord = _currentWord[..^1];
                    if (_currentWord.Length == 0)
                    {
                        _atSentenceStart = _wordBeganSentence;
                    }
                }
                else
                {
                    ClearContext();
                }
                break;

            case VirtualKey.Return:
                EndWord('\n');
                break;

            default:
                ClearContext();
                break;
        }

        KeyPressed?.Invoke(this, key);
        AfterContextChanged();
    }

    private void EndWord(char separator)
    {
        if (_currentWord.Length > 0)
        {
            _previousWords.Add(_currentWord);
            if (_previousWords.Count > ContextWords)
            {
                _previousWords.RemoveAt(0);
            }

            _currentWord = "";
        }

        // A new sentence or line owes nothing to the words before it, and starts with
        // a capital
        if (separator is '.' or '!' or '?' or '\n')
        {
            _previousWords.Clear();
            _atSentenceStart = true;
        }
    }

    /// <summary>
    /// The text cursor may be anywhere now, so nothing is assumed - not even that a
    /// capital is due.
    /// </summary>
    private void ClearContext()
    {
        _currentWord = "";
        _previousWords.Clear();
        _atSentenceStart = false;
    }

    private void ResetPrediction()
    {
        ClearContext();
        AfterContextChanged();
    }

    /// <summary>
    /// A freshly opened keyboard is usually about to start something new, so it offers
    /// a capital. Shift or B still types a lower-case letter if that is wrong.
    /// </summary>
    private void StartFresh()
    {
        ClearContext();
        _atSentenceStart = true;
        _showingPhrases = false;
        AfterContextChanged();
    }

    private void AfterContextChanged()
    {
        UpdateLetterCase();
        RefreshSuggestions();
    }

    /// <summary>
    /// Fill the row with suggestions, or with phrases while the Phrases key is on.
    /// Suggestion answers can arrive out of order when keys are pressed quickly, so only
    /// the answer to the latest request is shown.
    /// </summary>
    private async void RefreshSuggestions()
    {
        int request = ++_suggestionRequest;

        IReadOnlyList<string> items;
        if (_showingPhrases)
        {
            items = Phrases.Where(p => !string.IsNullOrWhiteSpace(p)).Take(SuggestionCount).ToList();
        }
        else
        {
            var words = await _predictor.PredictAsync(_currentWord, _previousWords.ToArray(), SuggestionCount);
            items = words.Select(MatchCase).ToList();
        }

        if (request != _suggestionRequest)
        {
            return;
        }

        // An empty row after pressing Phrases looks broken, so explain it instead. The
        // explanation is only a label: nothing is stored for it, so it cannot be typed.
        string? emptyPhrasesHint = _showingPhrases && items.Count == 0
            ? "No phrases saved yet - add them on the HIDra screen"
            : null;

        for (int i = 0; i < SuggestionCount; i++)
        {
            _suggestions[i] = i < items.Count ? items[i] : null;

            if (FindName($"Suggestion{i}") is Button button)
            {
                // Phrases can be long; trim them to the key rather than let them spill
                button.Content = new TextBlock
                {
                    Text = _suggestions[i] ?? "",
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontSize = _showingPhrases ? 16 : 20,
                    Margin = new Thickness(6, 0, 6, 0)
                };

                if (i == 0 && emptyPhrasesHint != null)
                {
                    // Spread across the row so it reads as one message, not six keys
                    button.Content = new TextBlock
                    {
                        Text = emptyPhrasesHint,
                        FontSize = 15,
                        FontStyle = FontStyles.Italic,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(6, 0, 6, 0)
                    };
                }
            }
        }

        if (FindName("PhrasesKey") is Button phrasesKey)
        {
            phrasesKey.Content = _showingPhrases ? "Words" : "Phrases";
        }
    }

    /// <summary>
    /// Show suggestions in the case being typed, so a capitalised start gets
    /// capitalised suggestions and Caps Lock gets capitals throughout.
    /// </summary>
    private string MatchCase(string word)
    {
        if (_capsLockOn)
        {
            return word.ToUpperInvariant();
        }

        if (CapitaliseNextLetter || (_currentWord.Length > 0 && char.IsUpper(_currentWord[0])))
        {
            return char.ToUpperInvariant(word[0]) + word[1..];
        }

        return word;
    }

    /// <summary>
    /// The first key of the row swaps it between word suggestions and the student's
    /// saved phrases.
    /// </summary>
    private void PhrasesKey_Click(object sender, RoutedEventArgs e)
    {
        _showingPhrases = !_showingPhrases;
        RefreshSuggestions();
    }

    private void SuggestionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } || !int.TryParse(tag, out int index))
        {
            return;
        }

        string? word = _suggestions[index];
        if (word == null)
        {
            return;
        }

        if (_showingPhrases)
        {
            // A phrase is typed whole, and the row goes back to suggestions for what
            // comes after it
            _showingPhrases = false;
            RaiseTextEntered(word + " ");
            return;
        }

        // B (or Shift) gives the word a capital, the same as it does for a letter
        if (_shiftPressed)
        {
            word = char.ToUpperInvariant(word[0]) + word[1..];
            _shiftPressed = false;
            UpdateModifierButtons();
            UpdateLetterCase();
            UpdateNumberRowSymbols();
        }

        // Only the rest of the word is typed: what is already there stays exactly as
        // the student typed it, whatever case the suggestion came back in.
        string rest = word.Length > _currentWord.Length ? word[_currentWord.Length..] : "";

        // A suggestion taken at the start of a sentence arrives already capitalised
        // from MatchCase, so it counts as the sentence's first word.
        if (_currentWord.Length == 0)
        {
            _wordBeganSentence = _atSentenceStart;
        }

        RaiseTextEntered(rest + " ");
    }

    // ---------------------------------------------------------------------------
    // Keyboard size
    // ---------------------------------------------------------------------------

    private const double BaseWidth = 1050;
    private const double BaseHeight = 424;

    /// <summary>
    /// Scale the whole keyboard, keys and text alike. It never grows wider than the
    /// screen, so the largest setting still fits a small laptop.
    /// </summary>
    public void SetScale(double scale)
    {
        var workArea = SystemParameters.WorkArea;
        scale = Math.Min(scale, workArea.Width / BaseWidth);

        KeyboardRoot.LayoutTransform = new ScaleTransform(scale, scale);
        Width = BaseWidth * scale;
        Height = BaseHeight * scale;

        // Keys have moved and changed size, so the highlight's map of them is stale
        _navigableKeys.Clear();
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
        NotifyActivity();
        EnsureKeysCollected();

        if (_navigableKeys.Count == 0)
        {
            return;
        }

        if (_highlightedKey == null)
        {
            SetHighlight(DefaultKey());
            return;
        }

        bool horizontal = direction is KeyboardNavigationDirection.Left
                                    or KeyboardNavigationDirection.Right;

        // Movement looks first at the keys the current one actually lines up with:
        // sideways, the keys sharing its row; up and down, the keys sharing its column.
        // Without that, a wide key is judged by the distance to its centre, so stepping
        // left off the arrow keys preferred a near key on the row above over the space
        // bar sitting right beside it. Ranking by centre distance alone cannot express
        // "the key I am touching".
        //
        // The same held vertically, and for the same reason: with the space bar starting
        // one column in, pressing down from Z reached Ctrl and down from B reached Win,
        // even though both letters sit squarely above the space bar. Overlap decides it
        // on either axis, and keeps deciding it if the layout changes again.
        var candidates = new List<Button>();

        var fromBounds = BoundsOf(_highlightedKey);

        foreach (var candidate in _navigableKeys)
        {
            if (ReferenceEquals(candidate, _highlightedKey))
            {
                continue;
            }

            var bounds = BoundsOf(candidate);

            // Overlap on the axis being crossed is what "same row" and "same column"
            // mean when rows are ragged and keys differ in size. The one pixel of slack
            // keeps two keys that merely abut from counting as aligned.
            bool aligned = horizontal
                ? bounds.Bottom > fromBounds.Top + 1 && bounds.Top < fromBounds.Bottom - 1
                : bounds.Right > fromBounds.Left + 1 && bounds.Left < fromBounds.Right - 1;

            if (aligned)
            {
                candidates.Add(candidate);
            }
        }

        if (candidates.Count == 0)
        {
            candidates.AddRange(_navigableKeys);
        }

        var from = CentreOf(_highlightedKey);
        Button? best = null;
        double bestScore = double.MaxValue;

        foreach (var candidate in candidates)
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
            StartDwell();
        }
    }

    /// <summary>
    /// Press the highlighted key, exactly as clicking it would.
    /// </summary>
    public void ActivateHighlight()
    {
        StopDwell();
        NotifyActivity();
        EnsureKeysCollected();

        if (_highlightedKey == null)
        {
            if (_navigableKeys.Count == 0)
            {
                return;
            }

            SetHighlight(DefaultKey());
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
        StopDwell();
        NotifyActivity();
        EnsureKeysCollected();

        if (_highlightedKey == null)
        {
            if (_navigableKeys.Count > 0)
            {
                SetHighlight(DefaultKey());
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

    /// <summary>
    /// Where the highlight starts, and where it falls back to if it is ever lost.
    ///
    /// This used to be whichever key the visual tree happened to yield first, which
    /// was harmless only for as long as that key was Esc. It is Close now, and opening
    /// the keyboard with the highlight already sitting on Close would make the first
    /// press shut it again. Naming the key removes the dependency on layout order:
    /// the middle of the home row is also the shortest average journey to anywhere.
    /// </summary>
    private Button DefaultKey()
    {
        return this.FindName("KeyG") as Button ?? _navigableKeys[0];
    }

    // ---------------------------------------------------------------------------
    // Keyboard dwell
    //
    // Resting the highlight on a key types it, for anyone who finds pressing A while
    // steering hard. Only a move the student makes starts the count: opening the
    // keyboard onto its default key must not type that key by itself.
    //
    // A ring around the key's label fills until it is typed - the same ring the
    // cursor shows for a dwell click. It appears only once the highlight has settled,
    // so steering across the keyboard does not flash a ring on every key passed; the
    // key is still typed at the full dwell time. Moving on cancels it.
    // ---------------------------------------------------------------------------

    private readonly System.Windows.Threading.DispatcherTimer _dwellTimer = new();
    private readonly System.Windows.Threading.DispatcherTimer _dwellRingDelay = new();
    private DwellKeyRing? _dwellRing;

    /// <summary>How long the highlight must settle before the ring appears</summary>
    private const double DwellRingDelaySeconds = 0.3;

    /// <summary>Type the highlighted key after it has been rested on</summary>
    public bool DwellEnabled { get; set; }

    public double DwellSeconds { get; set; } = 1.5;

    private void StartDwell()
    {
        StopDwell();

        if (!DwellEnabled || _highlightedKey == null)
        {
            return;
        }

        double total = Math.Max(0.3, DwellSeconds);
        double delay = Math.Min(DwellRingDelaySeconds, total);

        _dwellTimer.Interval = TimeSpan.FromSeconds(total);
        _dwellTimer.Start();

        _dwellRingDelay.Interval = TimeSpan.FromSeconds(delay);
        _dwellRingDelay.Start();
    }

    private void ShowDwellRing()
    {
        _dwellRingDelay.Stop();

        if (_highlightedKey == null || AdornerLayer.GetAdornerLayer(_highlightedKey) is not AdornerLayer layer)
        {
            return;
        }

        double total = Math.Max(0.3, DwellSeconds);
        double remaining = Math.Max(0.05, total - Math.Min(DwellRingDelaySeconds, total));

        _dwellRing = new DwellKeyRing(_highlightedKey);
        layer.Add(_dwellRing);
        _dwellRing.BeginAnimation(DwellKeyRing.ProgressProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(remaining))));
    }

    private void StopDwell()
    {
        _dwellTimer.Stop();
        _dwellRingDelay.Stop();

        if (_dwellRing != null)
        {
            AdornerLayer.GetAdornerLayer(_dwellRing.AdornedElement)?.Remove(_dwellRing);
            _dwellRing = null;
        }
    }

    // ---------------------------------------------------------------------------
    // Fading when resting
    //
    // When the controller has been left alone for a while, the keyboard fades so the
    // text behind it shows through - for when it covers what is being typed and moving
    // it is not enough. Any use of the controller brings it straight back. It never
    // fades while a dwell is counting down, never below a visible minimum, and while
    // faded clicks pass through it to whatever is underneath.
    // ---------------------------------------------------------------------------

    private readonly System.Windows.Threading.DispatcherTimer _fadeIdleTimer = new();
    private readonly System.Windows.Threading.DispatcherTimer _fadeStepTimer =
        new() { Interval = TimeSpan.FromMilliseconds(16) };

    private double _opacity = 1.0;
    private double _targetOpacity = 1.0;
    private double _opacityStep;

    private const double FadeOutSeconds = 0.6;
    private const double FadeInSeconds = 0.12;

    public bool FadeEnabled { get; set; }

    public double FadeSeconds { get; set; } = 2.0;

    /// <summary>How visible the keyboard stays when faded</summary>
    public double FadeOpacity { get; set; } = 0.3;

    /// <summary>
    /// The controller was used: show the keyboard fully and start the idle count again
    /// </summary>
    public void NotifyActivity()
    {
        if (_targetOpacity < 1.0)
        {
            AnimateOpacityTo(1.0, FadeInSeconds);
        }

        _fadeIdleTimer.Stop();

        if (FadeEnabled && IsVisible)
        {
            _fadeIdleTimer.Interval = TimeSpan.FromSeconds(Math.Max(0.5, FadeSeconds));
            _fadeIdleTimer.Start();
        }
    }

    /// <summary>
    /// Apply a change to the fade settings straight away
    /// </summary>
    public void RefreshFade()
    {
        if (!FadeEnabled)
        {
            _fadeIdleTimer.Stop();
            AnimateOpacityTo(1.0, FadeInSeconds);
            return;
        }

        NotifyActivity();
    }

    private void FadeIdleElapsed()
    {
        _fadeIdleTimer.Stop();

        // Resting on a key to type it by dwell is not "left alone" - fading would hide
        // the very ring being watched. Look again once it has finished.
        if (_dwellTimer.IsEnabled)
        {
            _fadeIdleTimer.Start();
            return;
        }

        AnimateOpacityTo(Math.Clamp(FadeOpacity, 0.2, 0.6), FadeOutSeconds);
    }

    private void AnimateOpacityTo(double target, double seconds)
    {
        _targetOpacity = target;
        double frames = Math.Max(1, seconds * 1000 / _fadeStepTimer.Interval.TotalMilliseconds);
        _opacityStep = Math.Abs(target - _opacity) / frames;

        // Clicks pass through only while faded, so the text behind can be clicked into;
        // the moment it starts coming back it catches them again.
        SetClickThrough(target < 1.0);

        if (!_fadeStepTimer.IsEnabled)
        {
            _fadeStepTimer.Start();
        }
    }

    private void FadeStep()
    {
        _opacity = _opacity < _targetOpacity
            ? Math.Min(_targetOpacity, _opacity + _opacityStep)
            : Math.Max(_targetOpacity, _opacity - _opacityStep);

        Opacity = _opacity;

        if (Math.Abs(_opacity - _targetOpacity) < 0.001)
        {
            _fadeStepTimer.Stop();
        }
    }

    private void SetClickThrough(bool on)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        int style = GetWindowLong(hwnd, GWL_EXSTYLE);
        int updated = on ? style | WS_EX_TRANSPARENT : style & ~WS_EX_TRANSPARENT;
        if (updated != style)
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, updated);
        }
    }

    /// <summary>Current visibility, 0 to 1, for tests and diagnostics</summary>
    public double CurrentOpacity => _opacity;

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

    private Rect BoundsOf(Button key)
    {
        var topLeft = key.TransformToAncestor(this).Transform(new Point(0, 0));
        return new Rect(topLeft.X, topLeft.Y, key.ActualWidth, key.ActualHeight);
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
