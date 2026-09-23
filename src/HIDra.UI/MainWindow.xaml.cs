using System;
using System.Windows;
using System.Windows.Media;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using HIDra.Core;
using HIDra.Core.Configuration;
using HIDra.Models;
using HIDra.UI.Views;

namespace HIDra.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private HIDraEngine? _engine;
        private VirtualKeyboardWindow? _virtualKeyboard;
        private TrayIcon? _trayIcon;
        private ModeToast? _modeToast;
        private DwellRing? _dwellRing;
        private System.Windows.Threading.DispatcherTimer? _startRetryTimer;

        /// <summary>
        /// This student's own settings, kept between sessions. Replaced whole when
        /// settings exported from another student are imported.
        /// </summary>
        private UserSettings _userSettings = UserSettingsStore.Load();

        /// <summary>
        /// Set only when the user genuinely chooses to exit. Until then, closing the
        /// window hides it instead of shutting down - exiting would take away the only
        /// pointing device the user has.
        /// </summary>
        private bool _exitConfirmed;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        /// <summary>
        /// Create hardcoded settings optimized for accessibility
        /// No config files needed - everything is built-in for reliability
        /// </summary>
        private (InputSettings settings, Dictionary<string, ButtonMapping> buttonMappings) CreateHardcodedConfiguration()
        {
            // Hardcoded settings optimized for accessibility
            var settings = new InputSettings
            {
                CursorSensitivity = _userSettings.CursorSensitivity, // Saved per student (default 0.15)
                ScrollSensitivity = _userSettings.ScrollSensitivity, // Saved per student (default 0.5)
                PrecisionModeSensitivity = _userSettings.SlowPointerPercent / 100f, // Slow pointer, as a share of normal
                Deadzone = 0.05f,                   // Low deadzone (5%) for maximum control
                PollRateMs = 10,                    // 100Hz polling rate
                StickCalibrationMax = 0.90f,        // Compensate for worn controllers
                TriggerThreshold = 0.3f,            // 30% trigger press to activate
                EnableGrid3AutoSuspend = _userSettings.EnableGrid3AutoSuspend, // Saved per student (default on)
                EnableDwellClick = _userSettings.DwellClickEnabled,
                DwellClickSeconds = _userSettings.DwellClickSeconds,
                GentleCurve = _userSettings.GentleCurve,
                StickSmoothingSeconds = SmoothingSeconds[Math.Clamp(_userSettings.StickSmoothing, 0, UserSettings.MaxStickSmoothing)],
                IgnoreRepeatSeconds = _userSettings.IgnoreRepeatSeconds
            };

            // Each button's job, from a fixed list: this student's own choice where staff
            // have made one on the Controller tab, the standard job otherwise
            var buttonMappings = ButtonJobCatalogue.ToMappings(_userSettings.ButtonJobs);

            // The Left Trigger is handled apart from these: it moves the keyboard while
            // that is open, and otherwise does the job chosen for this student
            // (UseLeftTrigger). The engine reports it as a trigger press, not a button.

            return (settings, buttonMappings);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            FitToScreen();

            if (WindowsTouchKeyboard.Apply(_userSettings))
            {
                UserSettingsStore.Save(_userSettings);
            }

            RefreshSettingsDisplay();
            InitializeGuide();
            InitializeTryItOut();
            InitializeTrayIcon();
            StartEngine();
        }

        /// <summary>
        /// The window is laid out for a normal screen. On a small laptop it shrinks to the
        /// working area instead of running off the bottom: the controller drawing scales
        /// and the settings scroll.
        /// </summary>
        private void FitToScreen()
        {
            var workArea = SystemParameters.WorkArea;
            Width = Math.Min(Width, workArea.Width);
            Height = Math.Min(Height, workArea.Height);
            Left = workArea.Left + (workArea.Width - Width) / 2;
            Top = workArea.Top + (workArea.Height - Height) / 2;
        }

        private void InitializeTrayIcon()
        {
            _trayIcon = new TrayIcon();
            _trayIcon.ShowRequested += (_, _) => Dispatcher.Invoke(RestoreWindow);
            _trayIcon.ExitRequested += (_, _) => Dispatcher.Invoke(ExitApplication);
        }

        /// <summary>
        /// Create the engine and begin supervising the controller.
        ///
        /// This does not fail when no controller is plugged in. HIDra starts at logon,
        /// which is routinely before a member of staff has connected the controller, so
        /// "not there yet" is a normal state to sit in rather than an error to report.
        /// </summary>
        private void StartEngine()
        {
            try
            {
                var (settings, buttonMappings) = CreateHardcodedConfiguration();

                _engine = new HIDraEngine(settings, buttonMappings);
                _engine.ConnectionChanged += OnConnectionChanged;
                _engine.ErrorOccurred += OnErrorOccurred;
                _engine.VirtualKeyboardToggleRequested += OnVirtualKeyboardToggleRequested;
                _engine.KeyboardPositionToggleRequested += OnKeyboardPositionToggleRequested;
                _engine.DwellCountdownStarted += OnDwellCountdownStarted;
                _engine.DwellCountdownEnded += OnDwellCountdownEnded;
                _engine.InputActivity += OnInputActivity;
                _engine.BatteryChanged += OnBatteryChanged;
                _engine.ShowWindowRequested += OnShowWindowRequested;
                _engine.StickModeChanged += OnStickModeChanged;
                _engine.UnusableControllerDetected += OnUnusableControllerDetected;
                _engine.PausedChanged += OnPausedChanged;
                _engine.KeyboardNavigateRequested += OnKeyboardNavigate;
                _engine.KeyboardSelectRequested += OnKeyboardSelect;
                _engine.KeyboardSelectShiftedRequested += OnKeyboardSelectShifted;
                _engine.KeyboardQuickKeyRequested += OnKeyboardQuickKey;
                _engine.ActiveControlsChanged += OnActiveControlsChanged;

                InitializeVirtualKeyboard();

                _engine.KeyboardNavigationActive = _virtualKeyboard?.IsVisible == true;

                _engine.Start();

                if (_engine.DetectController())
                {
                    UpdateStatus(ConnectionStatus.Connected, "Controller connected and active - ready to use");
                }
                else
                {
                    UpdateStatus(ConnectionStatus.Connecting,
                        "Waiting for a controller - plug one in and it will connect on its own");
                }

                StopButton.IsEnabled = true;
                _trayIcon?.UpdateStatus(_engine.Controller?.IsConnected == true, _engine.Battery);
            }
            catch (Exception ex)
            {
                // Retry rather than offering a button. Whoever needs HIDra to start is,
                // by definition, the person who cannot click anything to make it happen.
                UpdateStatus(ConnectionStatus.Error,
                    $"Could not start: {ex.Message}. Trying again...");
                ScheduleEngineRetry();
            }
        }

        /// <summary>
        /// Try to start the engine again shortly. Keeps trying for as long as it keeps
        /// failing, in the same spirit as the controller supervision loop.
        /// </summary>
        private void ScheduleEngineRetry()
        {
            _engine?.Dispose();
            _engine = null;

            _startRetryTimer ??= new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };

            _startRetryTimer.Tick -= OnStartRetryTick;
            _startRetryTimer.Tick += OnStartRetryTick;
            _startRetryTimer.Start();
        }

        private void OnStartRetryTick(object? sender, EventArgs e)
        {
            _startRetryTimer?.Stop();
            StartEngine();
        }


        /// <summary>
        /// Pause or resume controller input.
        ///
        /// This deliberately does not tear the engine down. Stopping outright left the
        /// controller dead, including the recovery chord - so whoever pressed it had no
        /// way to undo it without a mouse. Pausing keeps the chord alive.
        /// </summary>
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_engine == null)
            {
                StartEngine();
                return;
            }

            if (_engine.IsPaused)
            {
                _engine.Resume();
            }
            else
            {
                _engine.Pause();
            }
        }

        // Handlers below are raised from the controller supervision thread and use
        // BeginInvoke rather than Invoke on purpose. Invoke blocks the calling thread
        // until the UI thread is free, and shutdown has the UI thread waiting on the
        // supervision task - so the two could sit waiting on each other.
        private void OnConnectionChanged(object? sender, ControllerInfo info)
        {
            Dispatcher.BeginInvoke(() =>
            {
                // A deliberate pause outranks connection news: reporting the controller
                // as "active" while input is suppressed would be untrue.
                if (_engine?.IsPaused == true)
                {
                    _trayIcon?.UpdateStatus(info.IsConnected, _engine?.Battery);
                    return;
                }

                if (info.IsConnected)
                {
                    UpdateStatus(ConnectionStatus.Connected, $"{info.Name} connected and active");
                }
                else
                {
                    // Not an error state and not the end of the session: the engine is
                    // still running and will reattach by itself.
                    UpdateStatus(ConnectionStatus.Connecting,
                        "Controller lost - searching for it, reconnect or recharge it");
                }

                _trayIcon?.UpdateStatus(info.IsConnected, _engine?.Battery);
            });
        }

        private void OnBatteryChanged(object? sender, ControllerBattery battery)
        {
            Dispatcher.BeginInvoke(() =>
            {
                BatteryText.Text = battery.PowerType == BatteryPowerType.Unknown
                    ? string.Empty
                    : battery.Description;

                BatteryText.Foreground = battery.NeedsAttention
                    ? (Brush)FindResource("WarningBrush")
                    : new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));

                _trayIcon?.UpdateStatus(_engine?.Controller?.IsConnected == true, battery);
                _trayIcon?.ReportBattery(battery);
            });
        }

        /// <summary>
        /// A controller is plugged in but XInput cannot drive it. Say so plainly:
        /// otherwise this looks identical to having no controller at all, and nobody
        /// stood at the machine can tell the difference.
        /// </summary>
        private void OnUnusableControllerDetected(object? sender, HIDra.Core.Controllers.NonXInputController controller)
        {
            Dispatcher.BeginInvoke(() =>
            {
                ControllerInfo.Text = controller.Explanation;
                BatteryText.Text = controller.TechnicalDetail;
                BatteryText.Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));

                UpdateStatus(ConnectionStatus.Error, controller.Explanation);

                _trayIcon?.ShowMessage("HIDra - controller not usable", controller.Explanation);
            });
        }

        /// <summary>
        /// Reflect a pause in the window, the button and the tray, saying plainly how
        /// to undo it from the controller itself.
        /// </summary>
        private void OnPausedChanged(object? sender, bool paused)
        {
            Dispatcher.BeginInvoke(() =>
            {
                StopButton.Content = paused ? "Resume" : "Pause";

                if (paused)
                {
                    UpdateStatus(ConnectionStatus.Disconnected, "Paused");
                    ControllerInfo.Text =
                        "Controller input is paused. Press Resume, or hold Back and Start "
                        + "together on the controller for one second.";
                    _trayIcon?.ShowMessage("HIDra paused",
                        "Controller input is paused. Hold Back and Start together to resume.");
                }
                else
                {
                    bool connected = _engine?.Controller?.IsConnected == true;
                    UpdateStatus(
                        connected ? ConnectionStatus.Connected : ConnectionStatus.Connecting,
                        connected
                            ? "Controller connected and active - ready to use"
                            : "Waiting for a controller - plug one in and it will connect on its own");
                }
            });
        }

        private void OnShowWindowRequested(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(RestoreWindow);
        }

        /// <summary>
        /// Confirm on screen which stick now does what. Pressing Y changes the meaning
        /// of both sticks, and without this the change is invisible until something
        /// unexpected happens.
        /// </summary>
        private void OnStickModeChanged(object? sender, bool rightStickIsCursor)
        {
            Dispatcher.BeginInvoke(() =>
            {
                _sticksSwapped = rightStickIsCursor;
                UpdateGuideText();

                _modeToast ??= new ModeToast();
                _modeToast.ShowMessage(rightStickIsCursor
                    ? "Cursor: RIGHT stick\nScroll: LEFT stick"
                    : "Cursor: LEFT stick\nScroll: RIGHT stick");
            });
        }

        /// <summary>
        /// Bring the window back into view, whether it was hidden to the tray or just
        /// minimised behind something.
        /// </summary>
        private void RestoreWindow()
        {
            Show();
            WindowState = WindowState.Maximized;
            Activate();
            Topmost = true;
            Topmost = false;
        }

        /// <summary>
        /// Someone launched HIDra again while it was already running. Show the window
        /// they were presumably looking for, and say why a second copy did not appear -
        /// otherwise clicking the shortcut looks like it did nothing at all.
        /// </summary>
        public void RestoreFromAnotherInstance()
        {
            RestoreWindow();
            _trayIcon?.ShowMessage("HIDra is already running",
                "Only one copy can run at a time, so this window has been brought back "
                + "instead of starting another.");
        }

        private void ExitApplication()
        {
            _exitConfirmed = true;
            Close();
        }

        private void OnErrorOccurred(object? sender, string error)
        {
            // Deliberately not a modal dialog. A message box steals focus and sits there
            // until dismissed, and dismissing it needs the pointer that has just stopped
            // working - so an error about the controller could block recovery from it.
            Dispatcher.BeginInvoke(() =>
            {
                ControllerInfo.Text = error;
                _trayIcon?.ShowMessage("HIDra", error);
            });
        }

        private void UpdateStatus(ConnectionStatus status, string message)
        {
            StatusText.Text = status switch
            {
                ConnectionStatus.Connected => "Connected",
                ConnectionStatus.Connecting => "Connecting...",
                ConnectionStatus.Disconnected => "Disconnected",
                ConnectionStatus.Error => "Error",
                _ => "Unknown"
            };

            var fillColor = status switch
            {
                ConnectionStatus.Connected => Color.FromRgb(0x4C, 0xAF, 0x50), // Green #4CAF50
                ConnectionStatus.Connecting => Color.FromRgb(0xF2, 0xC1, 0x2E), // Yellow - orange is the accent
                ConnectionStatus.Disconnected => Color.FromRgb(0xDC, 0x35, 0x45), // Red #DC3545
                ConnectionStatus.Error => Color.FromRgb(0xDC, 0x35, 0x45), // Red
                _ => Color.FromRgb(0x66, 0x66, 0x66) // Gray
            };

            StatusIndicator.Fill = new SolidColorBrush(fillColor);
            
            // Update glow effect color
            if (StatusIndicator.Effect is DropShadowEffect glow)
            {
                glow.Color = fillColor;
                glow.Opacity = status == ConnectionStatus.Connected ? 0.8 : 0.6;
            }

            ControllerInfo.Text = message;
        }
        
        private void InitializeVirtualKeyboard()
        {
            if (_virtualKeyboard == null)
            {
                _virtualKeyboard = new VirtualKeyboardWindow
                {
                    DwellEnabled = _userSettings.KeyboardDwellEnabled,
                    DwellSeconds = _userSettings.KeyboardDwellSeconds,
                    AutoCapitalise = _userSettings.AutoCapitalise,
                    Phrases = _userSettings.Phrases,
                    FadeEnabled = _userSettings.KeyboardFadeEnabled,
                    FadeSeconds = _userSettings.KeyboardFadeSeconds,
                    FadeOpacity = _userSettings.KeyboardFadeOpacity,
                    ShowShortcuts = _userSettings.ShowShortcuts,
                    AppKeys = _userSettings.AppKeys
                };
                _virtualKeyboard.SetScale(_userSettings.KeyboardScale);
                _virtualKeyboard.SetShortcutKeys(_userSettings.ShortcutKeys);
                _virtualKeyboard.KeyPressed += OnVirtualKeyboardKeyPressed;
                _virtualKeyboard.TextEntered += OnVirtualKeyboardTextEntered;
                _virtualKeyboard.KeyComboPressed += OnVirtualKeyboardKeyCombo;
                _virtualKeyboard.ReadAloudRequested += OnReadAloudRequested;

                // Tie D-pad routing to whether the keyboard is actually on screen.
                // Setting the flag only where it is toggled would strand it: the
                // keyboard's own close button hides the window without going through
                // that path, leaving the D-pad driving something nobody can see.
                _virtualKeyboard.IsVisibleChanged += (_, _) =>
                {
                    if (_engine != null)
                    {
                        _engine.KeyboardNavigationActive = _virtualKeyboard?.IsVisible == true;
                    }

                    // The guide shows what the buttons do right now
                    SetGuideMode(typing: _virtualKeyboard?.IsVisible == true);
                    PracticeKeyboardChanged(open: _virtualKeyboard?.IsVisible == true);
                };
            }
        }
        
        private void OnVirtualKeyboardToggleRequested(object? sender, EventArgs e)
        {
            // Use Dispatcher to ensure we're on the UI thread
            Dispatcher.BeginInvoke(() =>
            {
                InitializeVirtualKeyboard();

                if (_virtualKeyboard!.IsVisible)
                {
                    _virtualKeyboard.Hide();
                    return;
                }

                // If a phrase box on this window still has the typing cursor, whatever the
                // student types would land in it whenever this window is in front - a
                // Backspace could quietly erase a saved phrase. Keep the edit, lose the cursor.
                // The Practice word box is the exception: it is there to be typed into.
                if (System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.TextBox box
                    && box != WordTypingBox)
                {
                    UserSettingsStore.Save(_userSettings);
                    System.Windows.Input.Keyboard.ClearFocus();
                }

                _virtualKeyboard.MoveToEdge(_userSettings.KeyboardAtTop);
                _virtualKeyboard.Show();

                // Start with a key highlighted, so the first press types
                // something rather than only revealing where the highlight is.
                if (!_virtualKeyboard.HasHighlight)
                {
                    _virtualKeyboard.MoveHighlight(KeyboardNavigationDirection.Right);
                }
            });
        }

        /// <summary>
        /// LT while the keyboard is closed: whatever job staff have given it for this
        /// student. Zoom and Slow pointer say what they did in the same message as
        /// swapping the sticks, so nothing changes without the student seeing why.
        /// </summary>
        private void UseLeftTrigger()
        {
            if (_engine == null)
            {
                return;
            }

            var job = Jobs[ButtonJobCatalogue.LeftTrigger];
            switch (job.Id)
            {
                case "zoom":
                    bool zoomed = System.Diagnostics.Process.GetProcessesByName("Magnify") is { Length: > 0 } running
                        && DisposeAll(running);
                    _engine.SendKeyCombo(zoomed
                        ? new[] { VirtualKey.LeftWindows, VirtualKey.Escape }
                        : new[] { VirtualKey.LeftWindows, VirtualKey.OemPlus });
                    ShowToast(zoomed ? "Zoom off" : "Zoomed in\nLT again to zoom out");
                    break;

                case "slow-pointer":
                    _engine.SlowPointer = !_engine.SlowPointer;
                    ShowToast(_engine.SlowPointer ? "Slow pointer on\nLT again for normal speed" : "Slow pointer off");
                    break;

                default:
                    _engine.RunAction(job.ToMapping());
                    break;
            }
        }

        private static bool DisposeAll(System.Diagnostics.Process[] processes)
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
            return true;
        }

        private void ShowToast(string message)
        {
            _modeToast ??= new ModeToast();
            _modeToast.ShowMessage(message);
        }

        private void OnKeyboardPositionToggleRequested(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_virtualKeyboard?.IsVisible != true)
                {
                    UseLeftTrigger();
                    return;
                }

                _userSettings.KeyboardAtTop = !_userSettings.KeyboardAtTop;
                UserSettingsStore.Save(_userSettings);
                _virtualKeyboard.MoveToEdge(_userSettings.KeyboardAtTop);
                RefreshSettingsDisplay();
            });
        }

        private void OnDwellCountdownStarted(object? sender, double seconds)
        {
            Dispatcher.BeginInvoke(() =>
            {
                _dwellRing ??= new DwellRing();
                _dwellRing.StartCountdown(seconds);
            });
        }

        private void OnDwellCountdownEnded(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(() => _dwellRing?.StopCountdown());
        }

        /// <summary>
        /// Any use of the controller brings a faded keyboard straight back
        /// </summary>
        private void OnInputActivity(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_virtualKeyboard?.IsVisible == true)
                {
                    _virtualKeyboard.NotifyActivity();
                }
            });
        }

        // ---------------------------------------------------------------------------
        // The guide: what each button does
        //
        // The labels on the controller drawing say what each button does now - using
        // the pointer, or typing while the keyboard is open, which changes LB, RB, Y,
        // A, B, X and the D-pad. The switch follows the keyboard, so the screen always
        // matches the controller in the student's hands, and staff can flip it to look
        // ahead. Y swapping the sticks swaps their labels too.
        //
        // Each control, and its label, lights up orange while it is in use, so the
        // student can press a button and see what it is for.
        // ---------------------------------------------------------------------------

        private bool _guideTyping;
        private bool _sticksSwapped;

        private void InitializeGuide()
        {
            JobsDrawing.EnableEditing();
            JobsDrawing.PartChosen += (_, part) => ChooseJobPart(part);
            SetUpHowTo();

            SetGuideMode(typing: _virtualKeyboard?.IsVisible == true);
            ShowPage("Guide");
        }

        // ---------------------------------------------------------------------------
        // Pages
        //
        // Guide and Practice are the student's; Controller and Keyboard are settings, for
        // staff. The Guide is what opens.
        // ---------------------------------------------------------------------------

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string page })
            {
                ShowPage(page);
            }
        }

        private void ShowPage(string page)
        {
            (UIElement Page, Button Tab)[] pages =
            {
                (GuidePage, GuideTabButton),
                (PracticePage, PracticeTabButton),
                (ButtonsPage, ButtonsTabButton),
                (PointerPage, PointerTabButton),
                (KeyboardPage, KeyboardTabButton),
            };

            var accent = (Brush)FindResource("AccentBrush");
            foreach (var (element, tab) in pages)
            {
                bool showing = (string)tab.Tag == page;
                element.Visibility = showing ? Visibility.Visible : Visibility.Collapsed;

                // The page showing has the orange bar under its name
                tab.Background = showing ? accent : Brushes.Transparent;
                tab.Foreground = showing ? Brushes.White : new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xBB));
            }
        }

        private void ModePointer_Click(object sender, RoutedEventArgs e) => SetGuideMode(typing: false);

        private void ModeTyping_Click(object sender, RoutedEventArgs e) => SetGuideMode(typing: true);

        private void SetGuideMode(bool typing)
        {
            _guideTyping = typing;
            ModePointerButton.Background = typing ? OffBrush : OnBrush;
            ModeTypingButton.Background = typing ? OnBrush : OffBrush;
            UpdateGuideText();
        }

        // "How do I..." shows one answer at a time: click a question to open it. All the
        // answers at once needed a scroll bar on a laptop screen, and a scroll bar is one
        // more thing to aim at.
        private readonly Dictionary<StackPanel, Border?> _openHowTo = new();

        private void SetUpHowTo()
        {
            var accent = (Brush)FindResource("AccentBrush");
            foreach (var list in new[] { HowToPointer, HowToTyping })
            {
                foreach (var child in list.Children)
                {
                    if (child is Border card)
                    {
                        card.Cursor = System.Windows.Input.Cursors.Hand;
                        card.MouseEnter += (_, _) => card.BorderBrush = accent;
                        card.MouseLeave += (_, _) => card.BorderBrush = Brushes.Transparent;
                        card.MouseLeftButtonUp += (_, _) => OpenHowTo(list, card);
                    }
                }
                OpenHowTo(list, null);
            }
        }

        /// <summary>
        /// Open one card and close the rest. Null keeps the open card if it is still
        /// showing, or opens the first one that is.
        /// </summary>
        private void OpenHowTo(StackPanel list, Border? open)
        {
            if (open == null && _openHowTo.TryGetValue(list, out var current) && current?.Visibility == Visibility.Visible)
            {
                open = current;
            }

            var cards = new List<Border>();
            foreach (var child in list.Children)
            {
                if (child is Border { Child: StackPanel { Children.Count: >= 2 } } card)
                {
                    cards.Add(card);
                }
            }
            open ??= cards.Find(card => card.Visibility == Visibility.Visible);
            _openHowTo[list] = open;

            var accent = (Brush)FindResource("AccentBrush");
            foreach (var card in cards)
            {
                var parts = ((StackPanel)card.Child).Children;
                bool isOpen = card == open;
                parts[1].Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
                ((TextBlock)parts[0]).Foreground = isOpen ? accent : Brushes.White;
                }
        }

        /// <summary>This student's job for every button that can be given one</summary>
        private Dictionary<string, ButtonJob> Jobs => ButtonJobCatalogue.Resolve(_userSettings.ButtonJobs);

        private static readonly (string Name, string Label)[] DPadParts =
        {
            ("DpadUp", "Up"), ("DpadDown", "Down"), ("DpadLeft", "Left"), ("DpadRight", "Right")
        };

        private static readonly (string Name, string Label)[] StickPressParts =
        {
            ("LeftStickClick", "Left stick"), ("RightStickClick", "Right stick")
        };


        private void UpdateGuideText()
        {
            HowToPointer.Visibility = _guideTyping ? Visibility.Collapsed : Visibility.Visible;
            HowToTyping.Visibility = _guideTyping ? Visibility.Visible : Visibility.Collapsed;
            HowToModeText.Text = _guideTyping ? "While the keyboard is open" : "While using the pointer";

            // A card of its own for LT's jobs that are about seeing and aiming; any other
            // job it has is named on the card for that job
            (string title, string text) = Jobs[ButtonJobCatalogue.LeftTrigger].Id switch
            {
                "zoom" => ("See small things", "zooms in around the pointer. Press it again to zoom out."),
                "slow-pointer" => ("Hit small things", "makes the pointer slow. Press it again for normal speed."),
                _ => ("", "")
            };
            HowToLeftTriggerTitle.Text = title;
            HowToLeftTriggerText.Text = text;
            HowToLeftTrigger.Visibility = title.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

            bool hasRead = Array.Exists(ShortcutCatalogue.Resolve(_userSettings.ShortcutKeys),
                key => key?.Kind == ShortcutKind.ReadAloud);
            HowToReadAloud.Visibility = hasRead ? Visibility.Visible : Visibility.Collapsed;

            FillDrawing(GuideDrawing, _guideTyping);

            // The drawing on the Controller tab shows the jobs with the pointer, since
            // those are what is chosen there
            FillDrawing(JobsDrawing, typing: false);

            WriteHowTo();

            // A card may have been hidden or shown by the buttons this student has
            if (_openHowTo.Count > 0)
            {
                OpenHowTo(HowToPointer, null);
                OpenHowTo(HowToTyping, null);
            }
        }

        /// <summary>
        /// Label every control with what it does for this student, using the pointer or
        /// typing
        /// </summary>
        private void FillDrawing(ControllerDrawing drawing, bool typing)
        {
            var jobs = Jobs;

            // The button that opens the keyboard is the one that closes it
            string Job(string name) =>
                typing && jobs[name].Id == ButtonJobCatalogue.Keyboard ? "Close the keyboard" : jobs[name].Label;

            // The D-pad and the stick presses are more than one button under one label:
            // one job for them all is said once, different jobs each get a line
            string Several((string Name, string Label)[] parts, string? standard)
            {
                if (standard != null && Array.TrueForAll(parts, p => jobs[p.Name].Id == ButtonJobCatalogue.Button(p.Name)!.StandardJob))
                {
                    return standard;
                }

                if (Array.TrueForAll(parts, p => jobs[p.Name] == jobs[parts[0].Name]))
                {
                    return Job(parts[0].Name);
                }

                // Two to a line, and "window" left out, so four fit in one label
                var each = Array.ConvertAll(parts, p => $"{Arrow(p.Name)} {Job(p.Name).Replace(" window", "")}");
                return parts.Length == 4
                    ? $"{each[0]}   {each[1]}\n{each[2]}   {each[3]}"
                    : string.Join("\n", each);
            }

            static string Arrow(string name) => name switch
            {
                "DpadUp" => "\u2191",
                "DpadDown" => "\u2193",
                "DpadLeft" => "\u2190",
                "DpadRight" => "\u2192",
                "LeftStickClick" => "Left:",
                _ => "Right:"
            };

            drawing.ActLT.Text = typing ? "Keyboard to top or bottom" : Job(ButtonJobCatalogue.LeftTrigger);
            drawing.ActRT.Text = "Hold to click and drag";
            drawing.ActLB.Text = typing ? "Backspace" : Job("LeftBumper");
            drawing.ActRB.Text = typing ? "Space" : Job("RightBumper");
            drawing.ActY.Text = typing ? "Enter" : Job("ButtonY");
            drawing.ActB.Text = typing ? "Capital, or the symbol on top" : Job("ButtonB");
            drawing.ActX.Text = Job("ButtonX");
            drawing.ActA.Text = typing ? "Type the key" : Job("ButtonA");
            drawing.ActBack.Text = Job("Back");
            drawing.ActStart.Text = Job("Start");
            drawing.ActDPad.Text = typing ? "Move the orange box" : Several(DPadParts, "Maximise, minimise, snap");
            drawing.ActStickPress.Text = Several(StickPressParts, null);

            // Four jobs in one label need a smaller size to fit
            drawing.ActDPad.FontSize = drawing.ActDPad.Text.Contains('\n') ? 14 : 18;
            drawing.ActStickPress.FontSize = drawing.ActStickPress.Text.Contains('\n') ? 14 : 18;

            // While typing, the left stick always steers the keyboard. Otherwise the
            // sticks follow the swap.
            drawing.ActLeftStick.Text = typing ? "Move the orange box" : _sticksSwapped ? "Scroll" : "Move the pointer";
            drawing.ActRightStick.Text = _sticksSwapped ? "Move the pointer" : "Scroll";
        }

        /// <summary>
        /// The "How do I..." cards that name buttons, written from this student's
        /// buttons, so a card never tells them to press something that now does another
        /// job. A card whose job no button has is left out.
        /// </summary>
        private void WriteHowTo()
        {
            var jobs = Jobs;
            var names = new List<string>();
            foreach (var button in ButtonJobCatalogue.Buttons)
            {
                names.Add(button.Name);
            }

            // Every button with this job, drawn as badges
            List<object> Badges(string jobId)
            {
                var badges = new List<object>();
                foreach (var name in names)
                {
                    if (jobs[name].Id == jobId)
                    {
                        badges.Add(ButtonBadge(name));
                    }
                }
                return badges;
            }

            bool Has(string jobId) => Badges(jobId).Count > 0;

            // Click on something
            var click = new List<object> { "Move the pointer with the left stick.", Environment.NewLine };
            var clicks = new List<(string Job, string Verb)> { ("click", "clicks"), ("right-click", "right-clicks"), ("double-click", "double-clicks") }
                .FindAll(c => Has(c.Job));
            for (int i = 0; i < clicks.Count; i++)
            {
                click.AddRange(Badges(clicks[i].Job));
                click.Add(clicks[i].Verb + (i < clicks.Count - 1 ? ", " : ". "));
            }
            click.AddRange(new object[] { "Hold ", TextBadge("RT"), "to drag." });
            WriteText(HowToClickText, click);

            // Scroll a page
            var scroll = new List<object> { "Push the right stick the way you want to go." };
            if (Has("swap-sticks"))
            {
                scroll.Add(Environment.NewLine);
                scroll.AddRange(Badges("swap-sticks"));
                scroll.Add("swaps the sticks, if the other hand is easier.");
            }
            WriteText(HowToScrollText, scroll);

            // Arrange windows: the old words while the D-pad is standard, one line a job otherwise
            bool standardDPad = Array.TrueForAll(DPadParts, p => jobs[p.Name].Id == ButtonJobCatalogue.Button(p.Name)!.StandardJob);
            var windows = new List<object>();
            if (standardDPad)
            {
                windows.AddRange(new object[] { "D-pad up makes a window full screen, down hides it.", Environment.NewLine,
                    "Left or right puts it on that half of the screen." });
            }
            else
            {
                foreach (var (job, words) in new[] { ("maximise", "makes a window full screen."), ("minimise", "hides it."),
                    ("snap-left", "puts it on the left half."), ("snap-right", "puts it on the right half.") })
                {
                    if (Has(job))
                    {
                        if (windows.Count > 0) windows.Add(Environment.NewLine);
                        windows.AddRange(Badges(job));
                        windows.Add(words);
                    }
                }
            }
            WriteText(HowToWindowsText, windows);
            HowToWindows.Visibility = windows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            // Switch programs. A and B confirm and cancel whatever they are otherwise.
            var switching = Badges("switch-programs");
            if (switching.Count > 0)
            {
                switching.AddRange(new object[] { "shows them; press it again to move along. ", TextBadge("A", "BadgeA"), "picks one, ",
                    TextBadge("B", "BadgeB"), "goes back." });
            }
            WriteText(HowToSwitchText, switching);
            HowToSwitch.Visibility = switching.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            // The keyboard, whichever button opens it. A badge can only be in one place,
            // so each card gets its own.
            List<object> Keyboard() => Badges(ButtonJobCatalogue.Keyboard);

            var open = new List<object>(Keyboard()) { "for the keyboard, then go to Apps at the top left. ", TextBadge("A", "BadgeA"), "on a program opens it." };
            WriteText(HowToOpenText, open);

            var startTyping = new List<object> { "Click where the words should go, then press " };
            startTyping.AddRange(Keyboard());
            startTyping.Add("for the keyboard.");
            WriteText(HowToStartTypingText, startTyping);

            var inTheWay = new List<object> { TextBadge("LT"), "moves it to the top or bottom of the screen. " };
            inTheWay.AddRange(Keyboard());
            inTheWay.Add("closes it.");
            WriteText(HowToKeyboardWayText, inTheWay);

            var keyboardHint = new List<object>(Keyboard()) { "opens and closes it" };
            WriteText(KeyboardButtonHint, keyboardHint);

            var openStep = new List<object> { "Open the keyboard: press " };
            openStep.AddRange(Keyboard());
            WriteText(KeyboardStepOpenText, openStep);

            var closeStep = new List<object> { "Now close it: press " };
            closeStep.AddRange(Keyboard());
            WriteText(KeyboardStepCloseText, closeStep);

            var practiceHint = new List<object> { "Click in the box, then " };
            practiceHint.AddRange(Keyboard());
            practiceHint.Add("for the keyboard");
            WriteText(WordTypingHint, practiceHint);
        }

        /// <summary>
        /// Fill a TextBlock from words and badges. Environment.NewLine starts a new line.
        /// </summary>
        private static void WriteText(TextBlock text, List<object> parts)
        {
            text.Inlines.Clear();
            foreach (var part in parts)
            {
                text.Inlines.Add(part switch
                {
                    Inline inline => inline,
                    string s when s == Environment.NewLine => new LineBreak(),
                    _ => new Run(part.ToString())
                });
            }
        }

        /// <summary>
        /// A controller button drawn as it looks on the controller, to sit in a sentence
        /// </summary>
        private Inline ButtonBadge(string buttonName) => buttonName switch
        {
            "ButtonA" => TextBadge("A", "BadgeA"),
            "ButtonB" => TextBadge("B", "BadgeB"),
            "ButtonX" => TextBadge("X", "BadgeX"),
            "ButtonY" => TextBadge("Y", "BadgeY"),
            _ => TextBadge(ButtonJobCatalogue.Button(buttonName)?.Label ?? buttonName)
        };

        private Inline TextBadge(string label, string style = "Badge") =>
            new InlineUIContainer(new ContentControl { Style = (Style)FindResource(style), Content = label })
            {
                BaselineAlignment = BaselineAlignment.Center
            };

        private void OnActiveControlsChanged(object? sender, ControllerControls active)
        {
            Dispatcher.BeginInvoke(() => ShowActiveControls(active));
        }

        private void ShowActiveControls(ControllerControls active)
        {
            GuideDrawing.ShowActive(active);

            // Staff choosing jobs can press a button to find it on the drawing
            JobsDrawing.ShowActive(active);
        }

        // ---------------------------------------------------------------------------
        // Button jobs, chosen per student
        //
        // The Guide's drawing again, on the Controller tab: click a label, then click the
        // job it should have. The jobs come from a fixed list. What cannot be changed
        // never is: Back and Start held together always bring HIDra back (the engine
        // checks for that before any job), the last button that clicks keeps clicking,
        // and the last button that opens the keyboard keeps doing it.
        // ---------------------------------------------------------------------------

        // The label chosen on the drawing: a button name, or LeftTrigger, DPad or StickPress
        private string? _jobPart;

        // The button being given a job - for the D-pad and stick presses, which one
        private string? _jobButton;

        private void ChooseJobPart(string part)
        {
            // Clicking the label being changed again puts the list away
            if (part == _jobPart)
            {
                _jobPart = _jobButton = null;
            }
            else
            {
                _jobPart = part;
                _jobButton = part switch
                {
                    "DPad" => DPadParts[0].Name,
                    "StickPress" => StickPressParts[0].Name,
                    _ => part
                };
            }

            BuildJobChooser();
        }

        private void BuildJobChooser()
        {
            JobsDrawing.Select(_jobPart);
            JobParts.Children.Clear();
            JobChoices.Children.Clear();
            JobLockedText.Visibility = Visibility.Collapsed;

            if (_jobPart == null)
            {
                JobChooserTitle.Text = "Choose a button";
                JobChooserHint.Text = "Click a label, or a button on the drawing, to choose what it does for this student while the keyboard is closed. "
                    + "The sticks and RT cannot be changed.\n\n"
                    + "Holding Back and Start together always brings HIDra back, whatever Back and Start are given here. "
                    + "Some button always clicks, and some button always opens the keyboard.";
                return;
            }

            // The D-pad and the stick presses: which of them
            var parts = _jobPart switch
            {
                "DPad" => DPadParts,
                "StickPress" => StickPressParts,
                _ => Array.Empty<(string Name, string Label)>()
            };
            foreach (var (name, label) in parts)
            {
                var pick = new Button
                {
                    Style = (Style)FindResource("ToggleButtonStyle"),
                    Width = 118,
                    Height = 40,
                    Margin = new Thickness(0, 0, 8, 0),
                    Content = label,
                    Background = name == _jobButton ? OnBrush : OffBrush
                };
                pick.Click += (_, _) =>
                {
                    _jobButton = name;
                    BuildJobChooser();
                };
                JobParts.Children.Add(pick);
            }

            var button = ButtonJobCatalogue.Button(_jobButton!)!;
            var current = Jobs[button.Name];
            string? locked = ButtonJobCatalogue.WhyLocked(_userSettings.ButtonJobs, button.Name);

            JobChooserTitle.Text = $"What {button.Label} does:  {current.Label}";
            JobChooserHint.Text = current.Description + ". " + (button.Name == ButtonJobCatalogue.LeftTrigger
                ? "While the keyboard is open, LT always moves it to the top or bottom instead."
                : button.KeepsJobWhileTyping
                    ? "It does this while the keyboard is open, too."
                    : "While the keyboard is open it types instead.")
                + $" Standard: {ButtonJobCatalogue.Find(button.StandardJob)!.Label}.";

            if (locked != null)
            {
                JobLockedText.Text = locked;
                JobLockedText.Visibility = Visibility.Visible;
            }

            // A row or two for each kind of job, so the one wanted is quick to find. Jobs only
            // one button can have are left out for the rest; the keyboard is shown but
            // greyed where it could not be closed again.
            foreach (var (group, ids) in JobGroups)
            {
                var tiles = new WrapPanel();
                foreach (var id in ids)
                {
                    var job = ButtonJobCatalogue.Find(id)!;
                    if (job.OnlyOn != null && job.OnlyOn != button.Name)
                    {
                        continue;
                    }

                    bool allowed = ButtonJobCatalogue.Allowed(button, job);
                    var choice = EditorKey("JobChoice", job.Label);
                    choice.ToolTip = allowed ? job.Description : "Only on X, Back, Start or a stick press, so it can close the keyboard too";
                    choice.Background = job == current ? OnBrush : OffBrush;
                    choice.IsEnabled = allowed && (locked == null || job == current);
                    choice.Opacity = choice.IsEnabled ? 1 : 0.5;
                    choice.Click += (_, _) => SetButtonJob(button, job);
                    tiles.Children.Add(choice);
                }

                if (tiles.Children.Count == 0)
                {
                    continue;
                }

                var line = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
                var name = new TextBlock
                {
                    Text = group,
                    Width = 86,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 12, 6, 0)
                };
                DockPanel.SetDock(name, Dock.Left);
                line.Children.Add(name);
                line.Children.Add(tiles);
                JobChoices.Children.Add(line);
            }
        }

        private static readonly (string Group, string[] Ids)[] JobGroups =
        {
            ("Clicks", new[] { "click", "right-click", "double-click" }),
            ("Keyboard and pointer", new[] { "keyboard", "swap-sticks", "zoom", "slow-pointer" }),
            ("Windows", new[] { "switch-programs", "start-menu", "all-windows", "close-window",
                "maximise", "minimise", "snap-left", "snap-right" }),
            ("Editing", new[] { "undo", "redo", "copy", "paste" }),
            ("Keys", new[] { "escape", "enter", "tab", "nothing" }),
        };

        private void SetButtonJob(RemappableButton button, ButtonJob job)
        {
            var chosen = new Dictionary<string, string>(_userSettings.ButtonJobs ?? new Dictionary<string, string>())
            {
                [button.Name] = job.Id
            };

            // The choices shown already keep a click and the keyboard; this is the backstop
            if (!ButtonJobCatalogue.IsSafe(chosen))
            {
                return;
            }

            ChangeSettings(s => s.ButtonJobs = ButtonJobCatalogue.Clean(chosen));
        }

        private void ButtonsStandard_Click(object sender, RoutedEventArgs e)
        {
            _jobPart = _jobButton = null;
            ChangeSettings(s => s.ButtonJobs = null);
        }

        // ---------------------------------------------------------------------------
        // Practice
        //
        // Three activities, each getting a little harder as the student gets better:
        // clicking a circle that shrinks, typing a word, opening and closing the
        // keyboard. Also for staff to try a setting straight after changing it. Every
        // hit, miss and word is counted into today's line of the student's record, which
        // staff can look back on, or save as a spreadsheet, for a review. Feedback is
        // all on screen - some students cannot hear it.
        // ---------------------------------------------------------------------------

        private readonly Random _random = new();
        private PracticeProgress _progress = PracticeProgressStore.Load();
        private System.Windows.Threading.DispatcherTimer? _progressSaveTimer;
        private string _activity = "Circles";

        private void InitializeTryItOut()
        {
            _progress.CircleLevel = Math.Clamp(_progress.CircleLevel, 0, CircleSizes.Length - 1);

            _target = new Border
            {
                Background = OffBrush,
                BorderBrush = (Brush)FindResource("AccentBrush"),
                BorderThickness = new Thickness(4),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _target.MouseEnter += (_, _) => _target.Background = OnBrush;
            _target.MouseLeave += (_, _) => _target.Background = OffBrush;
            _target.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                CircleClicked(hit: true);
            };
            TargetCanvas.Children.Add(_target);
            SizeTarget();

            for (int i = 1; i <= 40; i++)
            {
                TryScrollList.Children.Add(new TextBlock
                {
                    Text = $"Line {i}",
                    FontSize = 17,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 3, 0, 3)
                });
            }

            ShowTargetScore();
            NextWord();
            ShowKeyboardStep();
            ShowActivity(_activity);
            ShowProgress();
        }

        private void Activity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string activity })
            {
                ShowActivity(activity);
            }
        }

        private void ShowActivity(string activity)
        {
            _activity = activity;
            (UIElement Page, Button Button, string Name)[] activities =
            {
                (CirclesActivity, ActivityCirclesButton, "Circles"),
                (WordActivity, ActivityWordButton, "Word"),
                (KeyboardActivity, ActivityKeyboardButton, "Keyboard"),
            };

            foreach (var (page, button, name) in activities)
            {
                page.Visibility = name == activity ? Visibility.Visible : Visibility.Collapsed;
                button.Background = name == activity ? OnBrush : OffBrush;
            }
        }

        /// <summary>
        /// Count something into today's line, and save a moment later - not on every
        /// click, as the file may be on a network drive
        /// </summary>
        private void Record(Action<PracticeDay> change)
        {
            change(PracticeProgressStore.Today(_progress));
            ShowProgress();

            if (_progressSaveTimer == null)
            {
                _progressSaveTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                _progressSaveTimer.Tick += (_, _) =>
                {
                    _progressSaveTimer.Stop();
                    PracticeProgressStore.Save(_progress);
                };
            }

            _progressSaveTimer.Stop();
            _progressSaveTimer.Start();
        }

        // --- Circles ---

        /// <summary>
        /// The circle's width at each level. The first is easy to hit with a very slow
        /// pointer; the last is the size of a small button in an ordinary program.
        /// </summary>
        private static readonly double[] CircleSizes = { 150, 124, 102, 84, 70, 58, 48, 40 };

        /// <summary>Hits in a round, before the circle may get smaller</summary>
        private const int CircleRound = 5;

        private Border _target = new();
        private int _roundHits;
        private int _roundMisses;
        private int _sessionHits;
        private int _sessionMisses;
        private System.Windows.Threading.DispatcherTimer? _circleMessageTimer;

        private void CircleClicked(bool hit)
        {
            if (hit)
            {
                _roundHits++;
                _sessionHits++;
                Record(day => day.CircleHits++);
            }
            else
            {
                _roundMisses++;
                _sessionMisses++;
                Record(day => day.CircleMisses++);
            }

            // Five hits with no more than one miss: smaller. More misses than hits in a
            // round: bigger, so a hard patch never becomes a wall.
            if (_roundHits >= CircleRound)
            {
                if (_roundMisses <= 1 && _progress.CircleLevel < CircleSizes.Length - 1)
                {
                    ChangeCircleLevel(+1, "Well done! A smaller circle");
                }
                _roundHits = _roundMisses = 0;
            }
            else if (_roundMisses > CircleRound)
            {
                if (_progress.CircleLevel > 0)
                {
                    ChangeCircleLevel(-1, "A bigger circle");
                }
                _roundHits = _roundMisses = 0;
            }

            if (hit)
            {
                _target.Background = OffBrush;
                Place(_target);
            }

            ShowTargetScore();
        }

        private void ChangeCircleLevel(int change, string message)
        {
            _progress.CircleLevel = Math.Clamp(_progress.CircleLevel + change, 0, CircleSizes.Length - 1);
            Record(day => day.SmallestCircle = Math.Max(day.SmallestCircle, _progress.CircleLevel));
            SizeTarget();
            ShowMessage(CircleMessage, ref _circleMessageTimer, message);
        }

        /// <summary>
        /// Show a message for a couple of seconds
        /// </summary>
        private static void ShowMessage(TextBlock text, ref System.Windows.Threading.DispatcherTimer? timer, string message)
        {
            text.Text = message;
            timer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            var clearing = timer;
            clearing.Stop();
            clearing.Tick -= ClearMessage;
            clearing.Tag = text;
            clearing.Tick += ClearMessage;
            clearing.Start();
        }

        private static void ClearMessage(object? sender, EventArgs e)
        {
            if (sender is System.Windows.Threading.DispatcherTimer { Tag: TextBlock text } timer)
            {
                timer.Stop();
                text.Text = "";
            }
        }

        private void SizeTarget()
        {
            double size = CircleSizes[_progress.CircleLevel];
            _target.Width = _target.Height = size;
            _target.CornerRadius = new CornerRadius(size / 2);
            Place(_target);

            CircleLevelDots.Children.Clear();
            for (int level = 0; level < CircleSizes.Length; level++)
            {
                double dot = 26 - level * 2;
                CircleLevelDots.Children.Add(new Ellipse
                {
                    Width = dot,
                    Height = dot,
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Fill = level <= _progress.CircleLevel ? OnBrush : OffBrush,
                    Stroke = (Brush)FindResource("AccentBrush"),
                    StrokeThickness = level == _progress.CircleLevel ? 2 : 0
                });
            }
        }

        private void TargetCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => Place(_target);

        private void TargetArea_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
            CircleClicked(hit: false);

        /// <summary>
        /// Back to the biggest circle, for a new student or a bad day. Counts already
        /// made are kept.
        /// </summary>
        private void TargetReset_Click(object sender, RoutedEventArgs e)
        {
            _roundHits = _roundMisses = _sessionHits = _sessionMisses = 0;
            _progress.CircleLevel = 0;
            PracticeProgressStore.Save(_progress);
            SizeTarget();
            ShowTargetScore();
        }

        private void ShowTargetScore() =>
            TargetScore.Text = $"Hits {_sessionHits}    Misses {_sessionMisses}    Circle {_progress.CircleLevel + 1} of {CircleSizes.Length}";

        /// <summary>
        /// Somewhere new in the box, away from where it was
        /// </summary>
        private void Place(Border target)
        {
            double width = TargetCanvas.ActualWidth, height = TargetCanvas.ActualHeight;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            double oldX = Canvas.GetLeft(target), oldY = Canvas.GetTop(target);
            for (int attempt = 0; attempt < 30; attempt++)
            {
                double x = _random.NextDouble() * Math.Max(0, width - target.Width);
                double y = _random.NextDouble() * Math.Max(0, height - target.Height);

                // A new place a fair way off, so every hit means moving the pointer
                bool farEnough = double.IsNaN(oldX) || Math.Abs(x - oldX) + Math.Abs(y - oldY) > target.Width * 1.5;
                if (farEnough || attempt == 29)
                {
                    Canvas.SetLeft(target, x);
                    Canvas.SetTop(target, y);
                    return;
                }
            }
        }

        // --- Type the word ---

        /// <summary>
        /// Short, everyday words, shortest first. The words offered grow longer as more
        /// are typed.
        /// </summary>
        private static readonly string[] PracticeWords =
        {
            "cat", "dog", "sun", "red", "hat", "cup", "bus", "yes", "no", "hi",
            "blue", "home", "book", "cake", "fish", "tree", "rain", "milk", "shop", "game",
            "hello", "happy", "music", "water", "phone", "apple", "chair", "pizza", "horse", "smile",
            "orange", "school", "friend", "garden", "family", "dinner", "pencil", "monkey", "summer", "yellow",
            "holiday", "picture", "chicken", "weekend", "birthday", "computer", "sandwich", "football",
        };

        private string _word = "";
        private int _sessionWords;
        private int _sessionWrongLetters;
        private int _lastTypedLength;
        private System.Windows.Threading.DispatcherTimer? _wordMessageTimer;
        private System.Windows.Threading.DispatcherTimer? _nextWordTimer;

        private void NextWord()
        {
            // Three-letter words first; one letter longer for every ten typed
            int typed = 0;
            foreach (var day in _progress.Days)
            {
                typed += day.WordsTyped;
            }
            int longest = 3 + typed / 10;

            var pool = Array.FindAll(PracticeWords, w => w.Length <= longest && w != _word);
            _word = pool[_random.Next(pool.Length)];

            _lastTypedLength = 0;
            if (WordTypingBox.Text.Length > 0)
            {
                WordTypingBox.Text = "";
            }
            ShowWord();
        }

        /// <summary>
        /// The word, with the letters typed right so far in orange
        /// </summary>
        private void ShowWord()
        {
            string typed = WordTypingBox.Text.TrimEnd();
            int right = 0;
            while (right < typed.Length && right < _word.Length
                && char.ToLowerInvariant(typed[right]) == _word[right])
            {
                right++;
            }

            WordTarget.Inlines.Clear();
            WordTarget.Inlines.Add(new Run(_word[..right]) { Foreground = (Brush)FindResource("AccentBrush") });
            WordTarget.Inlines.Add(new Run(_word[right..]));

            WordScore.Text = $"Words {_sessionWords}    Wrong letters {_sessionWrongLetters}";
        }

        private void WordTypingBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = WordTypingBox.Text;

            // A letter added that does not fit the word is a wrong letter. Taking one away
            // is not counted, and nor is a space after the word, which a word suggestion adds.
            string trimmed = text.TrimEnd();
            if (text.Length > _lastTypedLength && trimmed.Length == text.Length
                && !_word.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                _sessionWrongLetters++;
                Record(day => day.WrongLetters++);
            }
            _lastTypedLength = text.Length;

            ShowWord();

            if (trimmed.Equals(_word, StringComparison.OrdinalIgnoreCase) && _nextWordTimer?.IsEnabled != true)
            {
                _sessionWords++;
                Record(day => day.WordsTyped++);
                ShowWord();
                ShowMessage(WordMessage, ref _wordMessageTimer, "Well done!");

                // A moment to see it finished, then the next one
                _nextWordTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
                _nextWordTimer.Tick -= NextWordTick;
                _nextWordTimer.Tick += NextWordTick;
                _nextWordTimer.Start();
            }
        }

        private void NextWordTick(object? sender, EventArgs e)
        {
            _nextWordTimer?.Stop();
            NextWord();
        }

        private void WordSkip_Click(object sender, RoutedEventArgs e)
        {
            _nextWordTimer?.Stop();
            NextWord();
        }

        // --- Keyboard on and off ---

        private bool _keyboardStepClose;
        private int _sessionKeyboardRounds;
        private readonly System.Diagnostics.Stopwatch _keyboardRoundTimer = new();
        private System.Windows.Threading.DispatcherTimer? _keyboardMessageTimer;

        /// <summary>
        /// The keyboard opened or closed. Counts only while the activity is on screen, so
        /// ordinary typing elsewhere is not mistaken for practice.
        /// </summary>
        private void PracticeKeyboardChanged(bool open)
        {
            if (PracticePage.Visibility != Visibility.Visible || _activity != "Keyboard")
            {
                return;
            }

            if (open && !_keyboardStepClose)
            {
                _keyboardStepClose = true;
                _keyboardRoundTimer.Restart();
                ShowMessage(KeyboardMessage, ref _keyboardMessageTimer, "Open! Now close it");
            }
            else if (!open && _keyboardStepClose)
            {
                _keyboardStepClose = false;
                _sessionKeyboardRounds++;
                Record(day => day.KeyboardRounds++);
                ShowMessage(KeyboardMessage, ref _keyboardMessageTimer,
                    $"Well done! {_keyboardRoundTimer.Elapsed.TotalSeconds:0.0} seconds");
            }

            ShowKeyboardStep();
        }

        private void ShowKeyboardStep()
        {
            var accent = (Brush)FindResource("AccentBrush");
            var cardBorder = (Brush)FindResource("CardBorderBrush");

            KeyboardStepOpen.BorderBrush = _keyboardStepClose ? cardBorder : accent;
            KeyboardStepClose.BorderBrush = _keyboardStepClose ? accent : cardBorder;
            KeyboardStepOpen.Opacity = _keyboardStepClose ? 0.6 : 1;
            KeyboardStepClose.Opacity = _keyboardStepClose ? 1 : 0.6;
            KeyboardStepOpenMark.Text = _keyboardStepClose ? "\u2713" : "1";

            KeyboardScore.Text = $"Done {_sessionKeyboardRounds} times";
        }

        // --- The record ---

        /// <summary>
        /// The last week of practice, newest first, as a small table
        /// </summary>
        private void ShowProgress()
        {
            ProgressTable.Children.Clear();
            ProgressTable.RowDefinitions.Clear();
            ProgressTable.ColumnDefinitions.Clear();

            string[] headings = { "Day", "Circles hit", "Missed", "Smallest", "Words", "Wrong letters", "Keyboard" };
            foreach (var _ in headings)
            {
                ProgressTable.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }
            ProgressTable.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);

            void Cell(int row, int column, string text, bool heading = false)
            {
                var cell = new TextBlock
                {
                    Text = text,
                    FontSize = heading ? 12.5 : 15,
                    FontWeight = heading ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = heading ? new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)) : Brushes.White,
                    TextAlignment = column == 0 ? TextAlignment.Left : TextAlignment.Right,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = column == 0 ? double.PositiveInfinity : 62,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(column == 0 ? 0 : 10, 3, 0, 3)
                };
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, column);
                ProgressTable.Children.Add(cell);
            }

            ProgressTable.RowDefinitions.Add(new RowDefinition());
            for (int column = 0; column < headings.Length; column++)
            {
                Cell(0, column, headings[column], heading: true);
            }

            var days = _progress.Days;
            if (days.Count == 0)
            {
                ProgressTable.RowDefinitions.Add(new RowDefinition());
                Cell(1, 0, "No practice yet");
                return;
            }

            int row = 1;
            for (int i = days.Count - 1; i >= 0 && row <= 7; i--, row++)
            {
                var day = days[i];
                ProgressTable.RowDefinitions.Add(new RowDefinition());
                Cell(row, 0, DayName(day.Date));
                Cell(row, 1, day.CircleHits.ToString());
                Cell(row, 2, day.CircleMisses.ToString());
                Cell(row, 3, $"{day.SmallestCircle + 1} of {CircleSizes.Length}");
                Cell(row, 4, day.WordsTyped.ToString());
                Cell(row, 5, day.WrongLetters.ToString());
                Cell(row, 6, day.KeyboardRounds.ToString());
            }
        }

        private static string DayName(string date)
        {
            if (!DateTime.TryParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var day))
            {
                return date;
            }

            return day.Date == DateTime.Today ? "Today"
                : day.Date == DateTime.Today.AddDays(-1) ? "Yesterday"
                : day.ToString("ddd d MMM");
        }

        /// <summary>
        /// Every day of practice as a spreadsheet, for a review
        /// </summary>
        private void ExportProgress_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save this student's practice",
                FileName = "HIDra practice.csv",
                Filter = "Spreadsheet (*.csv)|*.csv",
                DefaultExt = ".csv"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            ProgressStatus.Text = PracticeProgressStore.ExportCsv(_progress, dialog.FileName, level => $"{level + 1} of {CircleSizes.Length}")
                ? $"Saved to {dialog.FileName}"
                : "Could not write that file. Try another folder.";
        }

        // ---------------------------------------------------------------------------
        // Shortcut keys and the Apps key, chosen per student
        //
        // A copy of the keyboard's four shortcut rows, and of the Apps row: click a place
        // to see what can go there, then click a choice. Choices come from a fixed list
        // of keys and the programs on the Start menu, never typed-in key combinations.
        // Choosing a key already elsewhere in the row swaps the two, rather than having
        // it twice.
        // ---------------------------------------------------------------------------

        private static readonly string[] ShortcutRowNames = { "Edit", "Select", "Style", "Tools" };

        private int _editingShortcutSlot = -1;
        private int _editingAppSlot = -1;

        private const int AppSlots = 6;

        private Button EditorKey(string styleKey, string label, char? icon = null, ImageSource? logo = null, string? description = null)
        {
            var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            if (icon is char glyph)
            {
                content.Children.Add(new TextBlock
                {
                    Text = glyph.ToString(),
                    FontFamily = ShortcutCatalogue.IconFont,
                    FontSize = 19,
                    FontWeight = FontWeights.Normal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 2)
                });
            }
            else if (logo != null)
            {
                content.Children.Add(new Image { Source = logo, Width = 24, Height = 24, Margin = new Thickness(0, 0, 0, 2) });
            }

            content.Children.Add(new TextBlock
            {
                Text = label,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            });

            if (description != null)
            {
                content.Children.Add(new TextBlock
                {
                    Text = description,
                    FontSize = 11.5,
                    FontWeight = FontWeights.Normal,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xBB)),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 3, 0, 0)
                });
            }

            return new Button { Style = (Style)FindResource(styleKey), Content = content };
        }

        private void BuildShortcutEditor()
        {
            ShortcutEditor.Children.Clear();
            var keys = ShortcutCatalogue.Resolve(_userSettings.ShortcutKeys);

            for (int row = 0; row < ShortcutCatalogue.Rows; row++)
            {
                var line = new DockPanel();
                var name = new TextBlock
                {
                    Text = ShortcutRowNames[row],
                    Width = 64,
                    FontSize = 15,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(name, Dock.Left);
                line.Children.Add(name);

                var grid = new UniformGrid { Columns = ShortcutCatalogue.RowLength };
                for (int column = 0; column < ShortcutCatalogue.RowLength; column++)
                {
                    int slot = row * ShortcutCatalogue.RowLength + column;
                    var key = keys[slot];
                    var button = key != null ? EditorKey("EditorKey", key.Label, key.Icon) : EditorKey("EditorKey", "Empty");
                    button.Background = slot == _editingShortcutSlot ? OnBrush : OffBrush;
                    button.Click += (_, _) => ChooseShortcutSlot(slot);
                    grid.Children.Add(button);
                }
                line.Children.Add(grid);
                ShortcutEditor.Children.Add(line);
            }
        }

        private void ChooseShortcutSlot(int slot)
        {
            _editingShortcutSlot = slot;
            _editingAppSlot = -1;
            BuildShortcutEditor();
            BuildAppEditor();

            int row = slot / ShortcutCatalogue.RowLength;
            var current = ShortcutCatalogue.Resolve(_userSettings.ShortcutKeys)[slot];

            var choices = new List<Button>();
            foreach (var key in ShortcutCatalogue.InGroup(ShortcutCatalogue.GroupOfRow(row)))
            {
                var choice = EditorKey("ChoiceKey", key.Label, key.Icon, description: key.Description);
                choice.Background = key == current ? OnBrush : OffBrush;
                choice.Click += (_, _) => SetShortcut(slot, key.Id);
                choices.Add(choice);
            }

            var empty = EditorKey("ChoiceKey", "Empty", description: "Leave this place empty");
            empty.Background = current == null ? OnBrush : OffBrush;
            empty.Click += (_, _) => SetShortcut(slot, "");
            choices.Add(empty);

            ShowKeyChooser($"Choose a key for the {ShortcutRowNames[row]} row", choices, search: false);
        }

        private void SetShortcut(int slot, string id)
        {
            var ids = ShortcutCatalogue.ToIds(ShortcutCatalogue.Resolve(_userSettings.ShortcutKeys));

            int elsewhere = id == "" ? -1 : ids.IndexOf(id);
            if (elsewhere >= 0 && elsewhere != slot)
            {
                ids[elsewhere] = ids[slot];
            }
            ids[slot] = id;

            HideKeyChooser();
            ChangeSettings(s => s.ShortcutKeys = ids);
        }

        private void ShortcutsStandard_Click(object sender, RoutedEventArgs e)
        {
            HideKeyChooser();
            ChangeSettings(s => s.ShortcutKeys = null);
        }

        private void BuildAppEditor()
        {
            AppEditor.Children.Clear();
            var apps = AppLauncher.Chosen(_userSettings.AppKeys, AppSlots);

            for (int slot = 0; slot < AppSlots; slot++)
            {
                int place = slot;
                var app = apps[slot];
                var button = app != null ? EditorKey("EditorKey", app.Name, logo: app.Icon) : EditorKey("EditorKey", "Empty");
                button.Background = slot == _editingAppSlot ? OnBrush : OffBrush;
                button.Click += (_, _) => ChooseAppSlot(place);
                AppEditor.Children.Add(button);
            }
        }

        private void ChooseAppSlot(int slot)
        {
            _editingAppSlot = slot;
            _editingShortcutSlot = -1;
            BuildShortcutEditor();
            BuildAppEditor();

            // Reading the Start menu takes a moment the first time; say so rather than
            // looking stuck
            Cursor = System.Windows.Input.Cursors.Wait;
            var programs = AppLauncher.Choices;
            Cursor = null;

            var current = AppLauncher.Chosen(_userSettings.AppKeys, AppSlots)[slot];

            var choices = new List<Button>();
            var empty = EditorKey("ChoiceKey", "Empty", description: "Leave this place empty");
            empty.Background = current == null ? OnBrush : OffBrush;
            empty.Click += (_, _) => SetApp(slot, "");
            choices.Add(empty);

            foreach (var app in programs)
            {
                var choice = EditorKey("ChoiceKey", app.Name, logo: app.Icon);
                choice.Background = app.Id == current?.Id ? OnBrush : OffBrush;
                choice.Tag = app.Name;
                choice.Click += (_, _) => SetApp(slot, app.Id);
                choices.Add(choice);
            }

            ShowKeyChooser("Choose a program for this place", choices, search: true);
        }

        private void SetApp(int slot, string id)
        {
            var ids = new List<string>();
            foreach (var app in AppLauncher.Chosen(_userSettings.AppKeys, AppSlots))
            {
                ids.Add(app?.Id ?? "");
            }

            int elsewhere = id == "" ? -1 : ids.IndexOf(id);
            if (elsewhere >= 0 && elsewhere != slot)
            {
                ids[elsewhere] = ids[slot];
            }
            ids[slot] = id;

            HideKeyChooser();
            ChangeSettings(s => s.AppKeys = ids);
        }

        private void AppsStandard_Click(object sender, RoutedEventArgs e)
        {
            HideKeyChooser();
            ChangeSettings(s => s.AppKeys = null);
        }

        // The choices open over the Keyboard tab, where there is room for all of them -
        // a program list can be long - without the page ever needing to scroll

        private void ShowKeyChooser(string title, List<Button> choices, bool search)
        {
            KeyChooserTitle.Text = title;
            KeyChoices.Children.Clear();
            foreach (var choice in choices)
            {
                KeyChoices.Children.Add(choice);
            }

            AppSearchRow.Visibility = search ? Visibility.Visible : Visibility.Collapsed;
            AppSearchBox.Text = "";
            KeyChooserOverlay.Visibility = Visibility.Visible;
        }

        private void HideKeyChooser()
        {
            KeyChooserOverlay.Visibility = Visibility.Collapsed;
            _editingShortcutSlot = _editingAppSlot = -1;
            BuildShortcutEditor();
            BuildAppEditor();
        }

        private void KeyChooserClose_Click(object sender, RoutedEventArgs e) => HideKeyChooser();

        /// <summary>A click on the dark edge, not on the choices, puts them away</summary>
        private void KeyChooserOverlay_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.OriginalSource == KeyChooserOverlay)
            {
                HideKeyChooser();
            }
        }

        /// <summary>Show only the programs whose name has the words typed</summary>
        private void AppSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string find = AppSearchBox.Text.Trim();
            foreach (var child in KeyChoices.Children)
            {
                if (child is Button { Tag: string name } choice)
                {
                    choice.Visibility = find.Length == 0 || name.Contains(find, StringComparison.CurrentCultureIgnoreCase)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            }
        }

        // ---------------------------------------------------------------------------
        // Settings on the main screen
        //
        // Every change takes effect at once, is saved for this student straight away,
        // and is shown back on the screen, so there is no Save or Apply to forget.
        // ---------------------------------------------------------------------------

        // Orange, the same as Shift, Caps and Select on the keyboard: one colour for "on"
        private static readonly Brush OnBrush = (Brush)Application.Current.FindResource("AccentOnBrush");
        private static readonly Brush OffBrush = new SolidColorBrush(Color.FromRgb(0x3C, 0x3C, 0x3C));

        /// <summary>
        /// Cursor speed is shown as a percentage and moves in whole-number steps: 1% below
        /// 20%, 5% up to 50%, 10% above. Finest at the slow end, where a small change is a
        /// large share of the speed. A value between steps (from an older settings file)
        /// snaps to the next step in the direction pressed.
        /// </summary>
        private static float StepCursorSpeed(float current, bool faster)
        {
            int percent = (int)MathF.Round(current * 100);
            int next;

            if (faster)
            {
                int step = percent < 20 ? 1 : percent < 50 ? 5 : 10;
                next = (percent / step + 1) * step;
            }
            else
            {
                int step = percent <= 20 ? 1 : percent <= 50 ? 5 : 10;
                next = ((percent + step - 1) / step - 1) * step;
            }

            return Math.Clamp(next, 5, 100) / 100f;
        }

        private static float Step(float current, float step, float min, float max, bool up) =>
            Math.Clamp(MathF.Round(current + (up ? step : -step), 3), min, max);

        private void CursorSlower_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.CursorSensitivity = StepCursorSpeed(s.CursorSensitivity, faster: false));

        private void CursorFaster_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.CursorSensitivity = StepCursorSpeed(s.CursorSensitivity, faster: true));

        private void ScrollSlower_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.ScrollSensitivity = Step(s.ScrollSensitivity, 0.1f, 0.1f, 1.0f, up: false));

        private void ScrollFaster_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.ScrollSensitivity = Step(s.ScrollSensitivity, 0.1f, 0.1f, 1.0f, up: true));

        private void DwellClickToggle_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.DwellClickEnabled = !s.DwellClickEnabled);

        private void DwellClickShorter_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.DwellClickSeconds = Step(s.DwellClickSeconds, 0.25f, 0.5f, 3.0f, up: false));

        private void DwellClickLonger_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.DwellClickSeconds = Step(s.DwellClickSeconds, 0.25f, 0.5f, 3.0f, up: true));

        private void KeyboardDwellToggle_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.KeyboardDwellEnabled = !s.KeyboardDwellEnabled);

        private void KeyboardDwellShorter_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.KeyboardDwellSeconds = Step(s.KeyboardDwellSeconds, 0.25f, 0.5f, 3.0f, up: false));

        private void KeyboardDwellLonger_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.KeyboardDwellSeconds = Step(s.KeyboardDwellSeconds, 0.25f, 0.5f, 3.0f, up: true));

        private void KeyboardTop_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.KeyboardAtTop = true);

        private void KeyboardBottom_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.KeyboardAtTop = false);

        private void KeyboardSmaller_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.KeyboardScale = Step(s.KeyboardScale, 0.1f,
                UserSettings.MinKeyboardScale, UserSettings.MaxKeyboardScale, up: false));

        private void KeyboardBigger_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.KeyboardScale = Step(s.KeyboardScale, 0.1f,
                UserSettings.MinKeyboardScale, UserSettings.MaxKeyboardScale, up: true));

        private void KeyboardFadeToggle_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.KeyboardFadeEnabled = !s.KeyboardFadeEnabled);

        private void KeyboardFadeShorter_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.KeyboardFadeSeconds = Step(s.KeyboardFadeSeconds, 0.5f, 1.0f, 10.0f, up: false));

        private void KeyboardFadeLonger_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.KeyboardFadeSeconds = Step(s.KeyboardFadeSeconds, 0.5f, 1.0f, 10.0f, up: true));

        private void KeyboardFadeFainter_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.KeyboardFadeOpacity = Step(s.KeyboardFadeOpacity, 0.1f, 0.2f, 0.6f, up: false));

        private void KeyboardFadeStronger_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.KeyboardFadeOpacity = Step(s.KeyboardFadeOpacity, 0.1f, 0.2f, 0.6f, up: true));

        private void ShortcutsToggle_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.ShowShortcuts = !s.ShowShortcuts);

        private void AutoCapitalToggle_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.AutoCapitalise = !s.AutoCapitalise);

        /// <summary>
        /// Phrases are typed here by staff with a real keyboard. The keyboard sees each
        /// change at once; the file is written when the box is left, not per keystroke,
        /// because it may be on a network drive.
        /// </summary>
        private void Phrase_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_refreshingSettings || sender is not System.Windows.Controls.TextBox { Tag: string tag } box
                || !int.TryParse(tag, out int index))
            {
                return;
            }

            _userSettings.Phrases[index] = box.Text;
        }

        private void Phrase_LostFocus(object sender, RoutedEventArgs e) =>
            UserSettingsStore.Save(_userSettings);

        private void StopWindowsKeyboardToggle_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.StopWindowsKeyboard = !s.StopWindowsKeyboard);

        private void Grid3Toggle_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.EnableGrid3AutoSuspend = !s.EnableGrid3AutoSuspend);

        private void SlowPointerSlower_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.SlowPointerPercent = Math.Max(10, s.SlowPointerPercent - 10));

        private void SlowPointerFaster_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.SlowPointerPercent = Math.Min(80, s.SlowPointerPercent + 10));

        /// <summary>
        /// Copy this student's settings to a file, to import as another student
        /// </summary>
        private void ExportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export this student's HIDra settings",
                FileName = "HIDra settings.json",
                Filter = "HIDra settings (*.json)|*.json",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            ExportImportStatus.Text = UserSettingsStore.Export(_userSettings, dialog.FileName)
                ? $"Exported to {dialog.FileName}"
                : "Could not write that file. Try another folder.";
        }

        /// <summary>
        /// Replace this student's settings with ones exported from another student
        /// </summary>
        private void ImportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import HIDra settings for this student",
                Filter = "HIDra settings (*.json)|*.json|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            var imported = UserSettingsStore.Import(dialog.FileName, _userSettings);
            if (imported == null)
            {
                ExportImportStatus.Text = "That file is not HIDra settings.";
                return;
            }

            _userSettings = imported;
            ChangeSettings(_ => { });
            UpdateGuideText();
            ExportImportStatus.Text = $"Imported from {System.IO.Path.GetFileName(dialog.FileName)}, and saved for this student.";
        }

        private void CurveSteady_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.GentleCurve = false);

        private void CurveGentle_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.GentleCurve = true);

        private void SmoothingLess_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.StickSmoothing = Math.Max(0, s.StickSmoothing - 1));

        private void SmoothingMore_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.StickSmoothing = Math.Min(UserSettings.MaxStickSmoothing, s.StickSmoothing + 1));

        private void RepeatShorter_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.IgnoreRepeatSeconds = StepRepeat(s.IgnoreRepeatSeconds, longer: false));

        private void RepeatLonger_Click(object sender, RoutedEventArgs e) =>
            ChangeSettings(s => s.IgnoreRepeatSeconds = StepRepeat(s.IgnoreRepeatSeconds, longer: true));

        /// <summary>
        /// How long each smoothing step averages the sticks over, in seconds. A tenth of
        /// a second is already a noticeable steadying; much past a fifth, the pointer
        /// feels as if it is being towed.
        /// </summary>
        private static readonly float[] SmoothingSeconds = { 0f, 0.06f, 0.12f, 0.2f };
        private static readonly string[] SmoothingNames = { "Off", "A little", "More", "Most" };

        /// <summary>
        /// The windows a repeat press can be ignored for. A tremor or bounce comes within
        /// a few tenths of a second; much longer would swallow presses meant twice.
        /// </summary>
        private static readonly float[] RepeatSteps = { 0f, 0.2f, 0.3f, 0.5f, 0.75f };

        private static float StepRepeat(float current, bool longer)
        {
            int index = Array.FindIndex(RepeatSteps, v => v >= current - 0.001f);
            if (index < 0) index = RepeatSteps.Length - 1;
            index = Math.Clamp(index + (longer ? 1 : -1), 0, RepeatSteps.Length - 1);
            return RepeatSteps[index];
        }

        private void ChangeSettings(Action<UserSettings> change)
        {
            change(_userSettings);
            ApplySettings();
            UserSettingsStore.Save(_userSettings);
            RefreshSettingsDisplay();
        }

        /// <summary>
        /// Push the saved settings to everything that uses them, while it is running.
        /// </summary>
        private void ApplySettings()
        {
            if (_engine != null)
            {
                var input = _engine.Settings;
                input.CursorSensitivity = _userSettings.CursorSensitivity;
                input.ScrollSensitivity = _userSettings.ScrollSensitivity;
                input.EnableGrid3AutoSuspend = _userSettings.EnableGrid3AutoSuspend;
                input.EnableDwellClick = _userSettings.DwellClickEnabled;
                input.DwellClickSeconds = _userSettings.DwellClickSeconds;
                input.GentleCurve = _userSettings.GentleCurve;
                input.StickSmoothingSeconds = SmoothingSeconds[Math.Clamp(_userSettings.StickSmoothing, 0, UserSettings.MaxStickSmoothing)];
                input.IgnoreRepeatSeconds = _userSettings.IgnoreRepeatSeconds;
                input.PrecisionModeSensitivity = _userSettings.SlowPointerPercent / 100f;
                _engine.SetButtonMappings(ButtonJobCatalogue.ToMappings(_userSettings.ButtonJobs));

                // Slow pointer only stays on while LT is what switches it
                if (Jobs[ButtonJobCatalogue.LeftTrigger].Id != "slow-pointer")
                {
                    _engine.SlowPointer = false;
                }
            }

            if (_virtualKeyboard != null)
            {
                _virtualKeyboard.DwellEnabled = _userSettings.KeyboardDwellEnabled;
                _virtualKeyboard.DwellSeconds = _userSettings.KeyboardDwellSeconds;
                _virtualKeyboard.AutoCapitalise = _userSettings.AutoCapitalise;
                _virtualKeyboard.Phrases = _userSettings.Phrases;
                _virtualKeyboard.SetScale(_userSettings.KeyboardScale);
                _virtualKeyboard.FadeEnabled = _userSettings.KeyboardFadeEnabled;
                _virtualKeyboard.FadeSeconds = _userSettings.KeyboardFadeSeconds;
                _virtualKeyboard.FadeOpacity = _userSettings.KeyboardFadeOpacity;
                _virtualKeyboard.RefreshFade();
                _virtualKeyboard.ShowShortcuts = _userSettings.ShowShortcuts;
                _virtualKeyboard.SetShortcutKeys(_userSettings.ShortcutKeys);
                _virtualKeyboard.AppKeys = _userSettings.AppKeys;

                if (_virtualKeyboard.IsVisible)
                {
                    _virtualKeyboard.MoveToEdge(_userSettings.KeyboardAtTop);
                }
            }

            if (!_userSettings.DwellClickEnabled)
            {
                _dwellRing?.StopCountdown();
            }

            // Only this student's own Windows setting changes; the option is remembered
            WindowsTouchKeyboard.Apply(_userSettings);
        }

        // Set while the screen is being filled from the settings, so filling the phrase
        // boxes is not mistaken for someone typing in them
        private bool _refreshingSettings;

        private void RefreshSettingsDisplay()
        {
            _refreshingSettings = true;
            KeyboardSizeValue.Text = $"{_userSettings.KeyboardScale * 100:0}%";
            ShowToggle(AutoCapitalToggle, _userSettings.AutoCapitalise);
            ShowToggle(ShortcutsToggle, _userSettings.ShowShortcuts);
            ShowToggle(KeyboardFadeToggle, _userSettings.KeyboardFadeEnabled);
            KeyboardFadeValue.Text = $"{_userSettings.KeyboardFadeSeconds:0.0} s";
            KeyboardFadeOpacityValue.Text = $"{_userSettings.KeyboardFadeOpacity * 100:0}%";
            for (int i = 0; i < UserSettings.PhraseCount; i++)
            {
                if (FindName($"Phrase{i}") is System.Windows.Controls.TextBox box && box.Text != _userSettings.Phrases[i])
                {
                    box.Text = _userSettings.Phrases[i];
                }
            }
            SettingsLocationText.Text = $"Saved in {UserSettingsStore.Location}";
            _refreshingSettings = false;

            // Percentages rather than the underlying fractions: "15%" means something to
            // staff at a glance, "0.15" does not
            CursorSpeedValue.Text = $"{_userSettings.CursorSensitivity * 100:0}%";
            ScrollSpeedValue.Text = $"{_userSettings.ScrollSensitivity * 100:0}%";
            DwellClickValue.Text = $"{_userSettings.DwellClickSeconds:0.0#} s";
            KeyboardDwellValue.Text = $"{_userSettings.KeyboardDwellSeconds:0.0#} s";

            SlowPointerValue.Text = $"{_userSettings.SlowPointerPercent}%";
            BuildShortcutEditor();
            BuildAppEditor();
            BuildJobChooser();
            UpdateGuideText();

            CurveSteadyButton.Background = _userSettings.GentleCurve ? OffBrush : OnBrush;
            CurveGentleButton.Background = _userSettings.GentleCurve ? OnBrush : OffBrush;
            SmoothingValue.Text = SmoothingNames[Math.Clamp(_userSettings.StickSmoothing, 0, UserSettings.MaxStickSmoothing)];
            IgnoreRepeatValue.Text = _userSettings.IgnoreRepeatSeconds <= 0 ? "Off" : $"{_userSettings.IgnoreRepeatSeconds:0.0#} s";

            ShowToggle(DwellClickToggle, _userSettings.DwellClickEnabled);
            ShowToggle(KeyboardDwellToggle, _userSettings.KeyboardDwellEnabled);
            ShowToggle(StopWindowsKeyboardToggle, _userSettings.StopWindowsKeyboard);
            ShowToggle(Grid3Toggle, _userSettings.EnableGrid3AutoSuspend);

            KeyboardTopButton.Background = _userSettings.KeyboardAtTop ? OnBrush : OffBrush;
            KeyboardBottomButton.Background = _userSettings.KeyboardAtTop ? OffBrush : OnBrush;
        }

        private static void ShowToggle(System.Windows.Controls.Button button, bool on)
        {
            button.Content = on ? "On" : "Off";
            button.Background = on ? OnBrush : OffBrush;
        }

        private void OnVirtualKeyboardKeyPressed(object? sender, VirtualKey key)
        {
            _engine?.SendKeyPress(key);
        }
        
        private void OnVirtualKeyboardTextEntered(object? sender, string text)
        {
            _engine?.SendText(text);
        }

        private void OnKeyboardNavigate(object? sender, KeyboardNavigationDirection direction)
        {
            Dispatcher.BeginInvoke(() => _virtualKeyboard?.MoveHighlight(direction));
        }

        private void OnKeyboardSelect(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(() => _virtualKeyboard?.ActivateHighlight());
        }

        private void OnKeyboardSelectShifted(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(() => _virtualKeyboard?.ActivateHighlightShifted());
        }

        private void OnKeyboardQuickKey(object? sender, KeyboardQuickKey key)
        {
            Dispatcher.BeginInvoke(() => _virtualKeyboard?.PressQuickKey(key));
        }

        private void OnVirtualKeyboardKeyCombo(object? sender, VirtualKey[] keys)
        {
            _engine?.SendKeyCombo(keys);
        }

        private ReadAloud? _readAloud;

        private async void OnReadAloudRequested(object? sender, EventArgs e)
        {
            _readAloud ??= new ReadAloud();
            await _readAloud.ReadSelectionAsync(keys => _engine?.SendKeyCombo(keys));
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Closing the window must not end the session. Pressing the X used to shut
            // HIDra down completely, leaving the student with no pointer and no way to
            // restart it; now it hides to the tray, recoverable with the Back+Start
            // chord on the controller or from the notification area.
            if (!_exitConfirmed)
            {
                e.Cancel = true;
                Hide();
                _trayIcon?.ShowMessage("HIDra is still running",
                    "Your controller still works. Hold Back and Start together to bring this window back.");
                return;
            }

            _startRetryTimer?.Stop();
            UserSettingsStore.Save(_userSettings);
            PracticeProgressStore.Save(_progress);
            _virtualKeyboard?.Close();
            _readAloud?.Dispose();
            _modeToast?.Close();
            _dwellRing?.Close();
            _engine?.Stop();
            _engine?.Dispose();
            _trayIcon?.Dispose();

            Application.Current.Shutdown();
        }
    }
}

