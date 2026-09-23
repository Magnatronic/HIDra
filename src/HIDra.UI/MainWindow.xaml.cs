using System;
using System.Windows;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;
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
                _engine.KeyboardSectionJumpRequested += (_, direction) =>
                    Dispatcher.BeginInvoke(() => _virtualKeyboard?.JumpSection(direction));
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
                // The Practice typing box is the exception: it is there to be typed into.
                if (System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.TextBox box
                    && box != TypingBox)
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
                PracticeKeyboardMoved();
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
            // While typing: the button that opens the keyboard closes it, the typing
            // buttons type, and the rest keep their jobs
            string Job(string name) =>
                !typing ? jobs[name].Label
                : jobs[name].Id == ButtonJobCatalogue.Keyboard ? "Close the keyboard"
                : ButtonJobCatalogue.Button(name)?.TypingJob ?? jobs[name].Label;

            // The D-pad and the stick presses are more than one button under one label:
            // one job for them all is said once, different jobs each get a line
            string Several((string Name, string Label)[] parts, string? standard)
            {
                if (standard != null && Array.TrueForAll(parts, p => jobs[p.Name].Id == ButtonJobCatalogue.Button(p.Name)!.StandardJob))
                {
                    return standard;
                }

                if (Array.TrueForAll(parts, p => Job(p.Name) == Job(parts[0].Name)))
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

            drawing.ActLT.Text = Job(ButtonJobCatalogue.LeftTrigger);
            drawing.ActRT.Text = "Hold to click and drag";
            drawing.ActLB.Text = Job("LeftBumper");
            drawing.ActRB.Text = Job("RightBumper");
            drawing.ActY.Text = Job("ButtonY");
            drawing.ActB.Text = Job("ButtonB");
            drawing.ActX.Text = Job("ButtonX");
            drawing.ActA.Text = Job("ButtonA");
            drawing.ActBack.Text = Job("Back");
            drawing.ActStart.Text = Job("Start");
            drawing.ActDPad.Text = typing ? "Up, down: the orange box\nLeft, right: the text cursor" : Several(DPadParts, "Maximise, minimise, snap");
            drawing.ActStickPress.Text = Several(StickPressParts, null);

            // Four jobs in one label need a smaller size to fit
            drawing.ActDPad.FontSize = drawing.ActDPad.Text.Contains('\n') ? 14 : 18;
            drawing.ActStickPress.FontSize = drawing.ActStickPress.Text.Contains('\n') ? 14 : 18;

            // While typing, the left stick always steers the keyboard. Otherwise the
            // sticks follow the swap.
            drawing.ActLeftStick.Text = typing ? "Move the orange box" : _sticksSwapped ? "Scroll" : "Move the pointer";
            drawing.ActRightStick.Text = _sticksSwapped ? "Move the pointer" : typing ? "Jump to words or shortcuts" : "Scroll";
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

            var practiceHint = new List<object> { "Click in the box, then " };
            practiceHint.AddRange(Keyboard());
            practiceHint.Add("for the keyboard");
            WriteText(TypingHint, practiceHint);
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
            JobChooserHint.Text = current.Description + ". " + (button.TypingJob == null
                    ? "It does this while the keyboard is open, too."
                    : current.Id == ButtonJobCatalogue.Keyboard
                        ? "While the keyboard is open it closes it."
                        : $"While the keyboard is open: {button.TypingJob}.")
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
        // Two tests of everyday skills - pointing (with scrolling) and typing - each the
        // same every time at a given size or level, so one run compares fairly with the
        // last. Every run is kept in the student's practice record, shown beside the
        // tests and saved as a spreadsheet for reviews. Feedback is all on screen: some
        // students cannot hear it.
        // ---------------------------------------------------------------------------

        private PracticeProgress _progress = PracticeProgressStore.Load();
        private string _activity = "Pointer";

        private void InitializeTryItOut()
        {
            _progress.CircleLevel = Math.Clamp(_progress.CircleLevel, 0, CircleSizes.Length - 1);
            _progress.TypingLevel = Math.Clamp(_progress.TypingLevel, 0, TypingItems.Length - 1);

            _target = new Border
            {
                Background = OffBrush,
                BorderBrush = (Brush)FindResource("AccentBrush"),
                BorderThickness = new Thickness(4),
                Cursor = System.Windows.Input.Cursors.Hand,
                Visibility = Visibility.Collapsed
            };
            _target.MouseEnter += (_, _) => _target.Background = OnBrush;
            _target.MouseLeave += (_, _) => _target.Background = OffBrush;
            _target.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                TargetHit(e.GetPosition(PointerCanvas));
            };
            PointerCanvas.Children.Add(_target);

            ShowPointerStart();
            ShowTypingStart();
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
            PointerActivity.Visibility = activity == "Pointer" ? Visibility.Visible : Visibility.Collapsed;
            TypingActivity.Visibility = activity == "Typing" ? Visibility.Visible : Visibility.Collapsed;
            ActivityPointerButton.Background = activity == "Pointer" ? OnBrush : OffBrush;
            ActivityTypingButton.Background = activity == "Typing" ? OnBrush : OffBrush;
            ActivityScore.Text = "";
        }

        private void SaveProgress()
        {
            PracticeProgressStore.Save(_progress);
            ShowProgress();
        }

        // --- Pointer test ---

        /// <summary>
        /// The circle's width at each size. The first is easy to hit with a very slow
        /// pointer; the last is the size of a small button in an ordinary program.
        /// </summary>
        private static readonly double[] CircleSizes = { 150, 124, 102, 84, 70, 58, 48, 40 };

        /// <summary>
        /// Where the circles appear, in order, the same every run: across as a share of
        /// the width, down in screens of the page, which is three screens tall. Four need
        /// a scroll to reach - down the page, and back up again - and the distances vary,
        /// so near and far pointing are both measured.
        /// </summary>
        private static readonly (double X, double Y)[] CirclePlaces =
        {
            (0.15, 0.25), (0.85, 0.70), (0.30, 0.80), (0.70, 0.20),
            (0.50, 1.60), (0.20, 2.40), (0.80, 2.70), (0.40, 0.30),
            (0.90, 0.50), (0.10, 0.60), (0.60, 1.90), (0.35, 0.45),
        };

        private const double PageScreens = 3;

        private Border _target = new();
        private bool _pointerRunning;
        private int _circle;
        private int _runMisses;
        private int _runScrolled;
        private int _runOvershoots;
        private readonly System.Diagnostics.Stopwatch _runTimer = new();
        private readonly System.Diagnostics.Stopwatch _circleTimer = new();
        private readonly List<double> _throughputs = new();

        // The circle showing: whether it was in sight when it appeared (so pointing at it
        // measures pointing alone), whether it has been in sight since, and where the
        // last click was, which is where the pointer set off from
        private bool _circleStartedInView;
        private bool _circleSeen;
        private Point? _lastClick;

        private void ShowPointerStart(string? title = null, string? text = null)
        {
            PointerStartTitle.Text = title ?? "Pointer test";
            PointerStartText.Text = text ?? $"Click each circle as it appears - {CirclePlaces.Length} in all. "
                + "Some are further down or up the page: scroll to find them with the right stick.\n"
                + $"Circle size {_progress.CircleLevel + 1} of {CircleSizes.Length}.";
            PointerStartButton.Content = title == null ? "Start" : "Go again";
            PointerStartPanel.Visibility = Visibility.Visible;
            _target.Visibility = Visibility.Collapsed;
            ScrollHintTop.Visibility = ScrollHintBottom.Visibility = Visibility.Collapsed;
        }

        private void PointerStart_Click(object sender, RoutedEventArgs e)
        {
            PointerStartPanel.Visibility = Visibility.Collapsed;
            SizePointerPage();
            PointerScroll.ScrollToTop();

            _pointerRunning = true;
            _circle = 0;
            _runMisses = _runScrolled = _runOvershoots = 0;
            _throughputs.Clear();
            _lastClick = null;

            double size = CircleSizes[_progress.CircleLevel];
            _target.Width = _target.Height = size;
            _target.CornerRadius = new CornerRadius(size / 2);
            _target.Visibility = Visibility.Visible;

            _runTimer.Restart();
            ShowCircle();
        }

        /// <summary>The page is three screens of the box tall, whatever size the box is</summary>
        private void SizePointerPage()
        {
            PointerCanvas.Width = Math.Max(0, PointerScroll.ViewportWidth);
            PointerCanvas.Height = Math.Max(0, PointerScroll.ViewportHeight * PageScreens);
        }

        private void PointerScroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SizePointerPage();
            if (_pointerRunning)
            {
                PlaceCircle();
            }
        }

        private void ShowCircle()
        {
            PlaceCircle();
            _circleStartedInView = CircleInView();
            _circleSeen = _circleStartedInView;
            if (!_circleStartedInView)
            {
                _runScrolled++;
            }
            _circleTimer.Restart();
            ShowScrollHint();
            ActivityScore.Text = $"Circle {_circle + 1} of {CirclePlaces.Length}    Misses {_runMisses}";
        }

        private void PlaceCircle()
        {
            var (x, y) = CirclePlaces[_circle];
            double size = _target.Width, viewport = PointerScroll.ViewportHeight;
            Canvas.SetLeft(_target, Math.Clamp(x * PointerCanvas.Width - size / 2, 0, Math.Max(0, PointerCanvas.Width - size)));
            Canvas.SetTop(_target, Math.Clamp(y * viewport - size / 2, 0, Math.Max(0, PointerCanvas.Height - size)));
        }

        /// <summary>Whether the whole circle is on screen</summary>
        private bool CircleInView()
        {
            double top = Canvas.GetTop(_target), offset = PointerScroll.VerticalOffset;
            return top >= offset && top + _target.Height <= offset + PointerScroll.ViewportHeight;
        }

        private void ShowScrollHint()
        {
            bool running = _pointerRunning && PracticePage.Visibility == Visibility.Visible;
            double top = Canvas.GetTop(_target), offset = PointerScroll.VerticalOffset;
            ScrollHintTop.Visibility = running && top + _target.Height <= offset ? Visibility.Visible : Visibility.Collapsed;
            ScrollHintBottom.Visibility = running && top >= offset + PointerScroll.ViewportHeight ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PointerScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!_pointerRunning)
            {
                return;
            }

            // Scrolled into view and then out of it again, the other way: past it
            bool inView = CircleInView();
            if (_circleSeen && !inView && e.VerticalChange != 0)
            {
                bool gone = e.VerticalChange > 0
                    ? Canvas.GetTop(_target) + _target.Height <= PointerScroll.VerticalOffset
                    : Canvas.GetTop(_target) >= PointerScroll.VerticalOffset + PointerScroll.ViewportHeight;
                if (gone)
                {
                    _runOvershoots++;
                    _circleSeen = false;
                }
            }
            _circleSeen |= inView;

            ShowScrollHint();
        }

        private void PointerCanvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_pointerRunning)
            {
                _runMisses++;
                ActivityScore.Text = $"Circle {_circle + 1} of {CirclePlaces.Length}    Misses {_runMisses}";
            }
        }

        private void TargetHit(Point click)
        {
            if (!_pointerRunning)
            {
                return;
            }

            // Fitts's law: how hard the move was (distance against size, in bits) over
            // how long it took. Only for circles in sight from the start, and not the
            // first, whose starting point is the Start button.
            double seconds = _circleTimer.Elapsed.TotalSeconds;
            if (_circleStartedInView && _lastClick is Point from && seconds > 0)
            {
                var centre = new Point(Canvas.GetLeft(_target) + _target.Width / 2, Canvas.GetTop(_target) + _target.Height / 2);
                double distance = (centre - from).Length;
                double difficulty = Math.Log2(distance / _target.Width + 1);
                _throughputs.Add(difficulty / seconds);
            }
            _lastClick = click;

            _circle++;
            if (_circle < CirclePlaces.Length)
            {
                ShowCircle();
                return;
            }

            FinishPointerRun();
        }

        private void FinishPointerRun()
        {
            _pointerRunning = false;
            _runTimer.Stop();

            var run = new PointerRun
            {
                When = DateTime.Now,
                Size = _progress.CircleLevel,
                Targets = CirclePlaces.Length,
                Seconds = Math.Round(_runTimer.Elapsed.TotalSeconds, 1),
                Misses = _runMisses,
                Scrolled = _runScrolled,
                Overshoots = _runOvershoots,
                Throughput = _throughputs.Count > 0 ? Math.Round(_throughputs.Average(), 2) : 0
            };

            // The best before this run, at this size - the fair comparison
            double? best = null;
            foreach (var earlier in _progress.PointerRuns)
            {
                if (earlier.Size == run.Size && (best == null || earlier.Seconds < best))
                {
                    best = earlier.Seconds;
                }
            }
            _progress.PointerRuns.Add(run);

            string verdict = best == null ? "Your first run at this size."
                : run.Seconds < best ? $"Your fastest yet at this size! (was {best:0.0} s)"
                : $"Your best at this size: {best:0.0} s";

            // At most one miss: smaller circles next time. Half or more missed: bigger.
            string next = "";
            if (run.Misses <= 1 && _progress.CircleLevel < CircleSizes.Length - 1)
            {
                _progress.CircleLevel++;
                next = "\nSmaller circles next time!";
            }
            else if (run.Misses >= CirclePlaces.Length / 2 && _progress.CircleLevel > 0)
            {
                _progress.CircleLevel--;
                next = "\nBigger circles next time.";
            }

            SaveProgress();
            ActivityScore.Text = "";
            ShowPointerStart("Done!",
                $"{run.Targets} circles in {run.Seconds:0.0} seconds, {run.Misses} {(run.Misses == 1 ? "miss" : "misses")}.\n{verdict}{next}");
        }

        // --- Typing test ---

        private static readonly string[] TypingLevelNames = { "Words", "Phrases", "Sentences" };

        /// <summary>
        /// The same items every time at each level, so runs compare. Everyday words the
        /// students use, UK spelling, and only the punctuation a sentence needs.
        /// </summary>
        private static readonly string[][] TypingItems =
        {
            new[] { "cat", "sun", "home", "happy", "music", "water", "friend", "orange" },
            new[] { "good morning", "thank you", "see you soon", "I like music", "a cup of tea" },
            new[] { "I am going to the shop.", "The bus is late today.", "Can I have a drink please?", "My favourite colour is orange." },
        };

        private bool _typingRunning;
        private int _item;
        private string _itemText = "";
        private int _lastTypedLength;
        private readonly System.Diagnostics.Stopwatch _itemTimer = new();
        private TypingRun _typingRun = new();
        private System.Windows.Threading.DispatcherTimer? _nextItemTimer;

        private void TypingLevel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string tag } && int.TryParse(tag, out int level))
            {
                _progress.TypingLevel = level;
                PracticeProgressStore.Save(_progress);
                _typingRunning = false;
                ShowTypingStart();
            }
        }

        private void ShowTypingStart(string? title = null, string? text = null)
        {
            int level = _progress.TypingLevel;
            for (int i = 0; i < TypingItems.Length; i++)
            {
                ((Button)FindName($"TypingLevel{i}")).Background = i == level ? OnBrush : OffBrush;
            }

            TypingStartTitle.Text = title ?? $"Typing test: {TypingLevelNames[level].ToLowerInvariant()}";
            TypingStartText.Text = text ?? $"Type each of the {TypingItems[level].Length} {TypingLevelNames[level].ToLowerInvariant()} as it appears. "
                + "The box moves between the top and bottom of the page: if the keyboard covers it, move the keyboard with LT.";
            TypingStartPanel.Visibility = Visibility.Visible;
            TypingItem.Visibility = Visibility.Collapsed;
            TypingMessage.Text = "";
        }

        private void TypingStart_Click(object sender, RoutedEventArgs e)
        {
            TypingStartPanel.Visibility = Visibility.Collapsed;
            _typingRunning = true;
            _item = 0;
            _typingRun = new TypingRun { Level = _progress.TypingLevel };
            ShowItem();
        }

        private void ShowItem()
        {
            _itemText = TypingItems[_typingRun.Level][_item];
            _itemTimer.Reset();
            _lastTypedLength = 0;
            TypingBox.Text = "";
            TypingMessage.Text = "";

            // Top for one, bottom for the next, so wherever the keyboard opens it is in
            // the way some of the time
            TypingItem.VerticalAlignment = _item % 2 == 0 ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            TypingItem.Visibility = Visibility.Visible;
            ShowItemProgress();
            TypingBox.Focus();

            ActivityScore.Text = $"{_item + 1} of {TypingItems[_typingRun.Level].Length}";
        }

        /// <summary>The item, with what has been typed right so far in orange</summary>
        private void ShowItemProgress()
        {
            string typed = TypingBox.Text;
            int right = 0;
            while (right < typed.Length && right < _itemText.Length
                && char.ToLowerInvariant(typed[right]) == char.ToLowerInvariant(_itemText[right]))
            {
                right++;
            }

            TypingTarget.Inlines.Clear();
            TypingTarget.Inlines.Add(new Run(_itemText[..right]) { Foreground = (Brush)FindResource("AccentBrush") });
            TypingTarget.Inlines.Add(new Run(_itemText[right..]));
        }

        private void TypingBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_typingRunning || _nextItemTimer?.IsEnabled == true)
            {
                return;
            }

            string text = TypingBox.Text;
            if (text.Length > 0 && !_itemTimer.IsRunning)
            {
                _itemTimer.Start();
            }

            // A letter added that does not fit is a wrong letter; anything taken away is
            // a correction. A space after the last word, which a word suggestion adds, is
            // neither.
            if (text.Length > _lastTypedLength)
            {
                string fits = text.TrimEnd();
                if (fits.Length == text.Length && !_itemText.StartsWith(fits, StringComparison.OrdinalIgnoreCase))
                {
                    _typingRun.WrongLetters++;
                }
            }
            else if (text.Length < _lastTypedLength)
            {
                _typingRun.Corrections++;
            }
            _lastTypedLength = text.Length;

            ShowItemProgress();

            if (Matches(text, _itemText))
            {
                _itemTimer.Stop();
                _typingRun.Seconds += _itemTimer.Elapsed.TotalSeconds;
                _typingRun.Characters += _itemText.Length;
                NextItemSoon("Well done!");
            }
        }

        /// <summary>
        /// Done, whatever the capitals, extra spaces, or a missing full stop at the end
        /// </summary>
        private static bool Matches(string typed, string item)
        {
            static string Plain(string s) => string.Join(' ', s.Trim().TrimEnd('.').Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return Plain(typed).Equals(Plain(item), StringComparison.OrdinalIgnoreCase);
        }

        private void TypingSkip_Click(object sender, RoutedEventArgs e)
        {
            if (_typingRunning && _nextItemTimer?.IsEnabled != true)
            {
                _typingRun.Skipped++;
                NextItemSoon("Skipped");
            }
        }

        /// <summary>A moment to see it finished, then the next one</summary>
        private void NextItemSoon(string message)
        {
            TypingMessage.Text = message;
            TypingItem.Visibility = Visibility.Collapsed;
            _nextItemTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _nextItemTimer.Tick -= NextItemTick;
            _nextItemTimer.Tick += NextItemTick;
            _nextItemTimer.Start();
        }

        private void NextItemTick(object? sender, EventArgs e)
        {
            _nextItemTimer?.Stop();
            _item++;
            if (_item < TypingItems[_typingRun.Level].Length)
            {
                ShowItem();
            }
            else
            {
                FinishTypingRun();
            }
        }

        /// <summary>LT moved the keyboard: counted while a typing run is on</summary>
        private void PracticeKeyboardMoved()
        {
            if (_typingRunning)
            {
                _typingRun.KeyboardMoves++;
            }
        }

        private void FinishTypingRun()
        {
            _typingRunning = false;
            var run = _typingRun;
            run.When = DateTime.Now;
            run.Seconds = Math.Round(run.Seconds, 1);

            double? best = null;
            foreach (var earlier in _progress.TypingRuns)
            {
                if (earlier.Level == run.Level && earlier.Characters > 0 && (best == null || earlier.LettersPerMinute > best))
                {
                    best = earlier.LettersPerMinute;
                }
            }
            _progress.TypingRuns.Add(run);
            SaveProgress();

            string verdict = run.Characters == 0 ? "Nothing typed this time."
                : best == null ? "Your first run at this level."
                : run.LettersPerMinute > best ? $"Your fastest yet! (was {best:0})"
                : $"Your best at this level: {best:0} letters a minute";

            // Few slips: time for the next level
            string next = run.Skipped == 0 && run.WrongLetters <= 2 && run.Level < TypingItems.Length - 1
                ? $"\nReady for {TypingLevelNames[run.Level + 1].ToLowerInvariant()}?"
                : "";

            ActivityScore.Text = "";
            ShowTypingStart("Done!",
                $"{run.LettersPerMinute:0} letters a minute. {run.WrongLetters} wrong {(run.WrongLetters == 1 ? "letter" : "letters")}, "
                + $"{run.Corrections} {(run.Corrections == 1 ? "correction" : "corrections")}.\n{verdict}{next}");
        }

        // --- The record ---

        /// <summary>
        /// The latest runs of each test, newest first, with the best so far
        /// </summary>
        private void ShowProgress()
        {
            var pointer = new List<string[]>();
            for (int i = _progress.PointerRuns.Count - 1; i >= 0 && pointer.Count < 6; i--)
            {
                var run = _progress.PointerRuns[i];
                pointer.Add(new[] { RunDay(run.When), $"{run.Size + 1}", $"{run.Seconds:0.0} s", $"{run.Misses}", $"{run.Throughput:0.0}" });
            }
            FillTable(PointerTable, new[] { "When", "Size", "Time", "Misses", "Speed" }, pointer);

            var typing = new List<string[]>();
            for (int i = _progress.TypingRuns.Count - 1; i >= 0 && typing.Count < 6; i--)
            {
                var run = _progress.TypingRuns[i];
                typing.Add(new[] { RunDay(run.When), TypingLevelNames[Math.Clamp(run.Level, 0, 2)], $"{run.LettersPerMinute:0}", $"{run.WrongLetters}", $"{run.Corrections}" });
            }
            FillTable(TypingTable, new[] { "When", "Level", "A minute", "Wrong", "Fixed" }, typing);

            PointerBest.Text = _progress.PointerRuns.Count == 0 ? "No runs yet"
                : $"Circle size now {_progress.CircleLevel + 1} of {CircleSizes.Length}. Speed is in bits a second - higher is better.";
            TypingBest.Text = _progress.TypingRuns.Count == 0 ? "No runs yet"
                : $"Level now: {TypingLevelNames[_progress.TypingLevel].ToLowerInvariant()}. Letters typed a minute.";
        }

        private static void FillTable(Grid table, string[] headings, List<string[]> rows)
        {
            table.Children.Clear();
            table.RowDefinitions.Clear();
            table.ColumnDefinitions.Clear();
            for (int column = 0; column < headings.Length; column++)
            {
                table.ColumnDefinitions.Add(new ColumnDefinition { Width = column == 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(62) });
            }

            void Cell(int row, int column, string text, bool heading)
            {
                var cell = new TextBlock
                {
                    Text = text,
                    FontSize = heading ? 12.5 : 14.5,
                    FontWeight = heading ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = heading ? new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)) : Brushes.White,
                    TextAlignment = column == 0 ? TextAlignment.Left : TextAlignment.Right,
                    Margin = new Thickness(0, 2, 0, 2)
                };
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, column);
                table.Children.Add(cell);
            }

            if (rows.Count == 0)
            {
                return;
            }

            table.RowDefinitions.Add(new RowDefinition());
            for (int column = 0; column < headings.Length; column++)
            {
                Cell(0, column, headings[column], heading: true);
            }
            for (int row = 0; row < rows.Count; row++)
            {
                table.RowDefinitions.Add(new RowDefinition());
                for (int column = 0; column < headings.Length; column++)
                {
                    Cell(row + 1, column, rows[row][column], heading: false);
                }
            }
        }

        private static string RunDay(DateTime when) =>
            when.Date == DateTime.Today ? $"Today {when:HH:mm}"
            : when.Date == DateTime.Today.AddDays(-1) ? $"Yesterday {when:HH:mm}"
            : when.ToString("ddd d MMM");

        /// <summary>
        /// Every run as a spreadsheet, for a review
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

            ProgressStatus.Text = PracticeProgressStore.ExportCsv(_progress, dialog.FileName,
                    level => $"Circle {level + 1} of {CircleSizes.Length}", level => TypingLevelNames[Math.Clamp(level, 0, 2)])
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

