using System;
using System.Windows;
using System.Windows.Media;
using System.Collections.Generic;
using System.Windows.Controls;
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
        private (InputSettings settings, System.Collections.Generic.Dictionary<string, ButtonMapping> buttonMappings) CreateHardcodedConfiguration()
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

            // Hardcoded button mappings - cannot be accidentally changed
            var buttonMappings = new System.Collections.Generic.Dictionary<string, ButtonMapping>
            {
                ["ButtonA"] = new ButtonMapping 
                { 
                    Default = new ActionMapping { Action = "MouseLeftClick", Description = "Left mouse click" }
                },
                ["ButtonB"] = new ButtonMapping 
                { 
                    Default = new ActionMapping { Action = "MouseRightClick", Description = "Right mouse click" }
                },
                ["ButtonX"] = new ButtonMapping 
                { 
                    Default = new ActionMapping { Action = "ToggleOnScreenKeyboard", Description = "Toggle virtual keyboard" }
                },
                ["ButtonY"] = new ButtonMapping 
                { 
                    Default = new ActionMapping { Action = "SwapStickModes", Description = "Swap cursor/scroll sticks" }
                },
                ["LeftBumper"] = new ButtonMapping 
                { 
                    Default = new ActionMapping { Action = "TaskSwitcherBackward", Description = "Previous application" }
                },
                ["RightBumper"] = new ButtonMapping 
                { 
                    Default = new ActionMapping { Action = "MouseDoubleClick", Description = "Double click" }
                },
                ["Back"] = new ButtonMapping 
                { 
                    Default = new ActionMapping { Action = "WindowsKey", Description = "Open Start Menu" }
                },
                ["Start"] = new ButtonMapping 
                { 
                    Default = new ActionMapping { Action = "WindowsTab", Description = "Task View (all windows)" }
                },
                ["DpadUp"] = new ButtonMapping 
                { 
                    Default = new ActionMapping { Action = "MaximizeWindow", Description = "Maximize window" }
                },
                ["DpadDown"] = new ButtonMapping 
                { 
                    Default = new ActionMapping { Action = "MinimizeWindow", Description = "Minimize window" }
                },
                ["DpadLeft"] = new ButtonMapping 
                { 
                    Default = new ActionMapping 
                    { 
                        Action = "KeyCombo", 
                        Keys = new System.Collections.Generic.List<string> { "LWin", "Left" },
                        Description = "Snap window to left half" 
                    }
                },
                ["DpadRight"] = new ButtonMapping 
                { 
                    Default = new ActionMapping 
                    { 
                        Action = "KeyCombo", 
                        Keys = new System.Collections.Generic.List<string> { "LWin", "Right" },
                        Description = "Snap window to right half" 
                    }
                },
                ["LeftStickClick"] = new ButtonMapping
                {
                    Default = new ActionMapping { Action = "Undo", Description = "Undo (Ctrl+Z)" }
                },
                ["RightStickClick"] = new ButtonMapping
                {
                    Default = new ActionMapping { Action = "Undo", Description = "Undo (Ctrl+Z)" }
                }
            };

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
                    ShowShortcuts = _userSettings.ShowShortcuts
                };
                _virtualKeyboard.SetScale(_userSettings.KeyboardScale);
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
                // The Try it out box is the exception: it is there to be typed into.
                if (System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.TextBox box
                    && box != TryTypingBox)
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
        /// Left Trigger: flip the keyboard between the top and bottom of the screen, and
        /// remember the choice for next time. Does nothing when the keyboard is closed,
        /// so a stray press cannot silently change where it next appears.
        /// </summary>
        /// <summary>
        /// LT while the keyboard is closed: whatever job staff have given it for this
        /// student. Each says what it did in the same message as swapping the sticks, so
        /// nothing changes without the student seeing why.
        /// </summary>
        private void UseLeftTrigger()
        {
            if (_engine == null)
            {
                return;
            }

            switch (_userSettings.LeftTrigger)
            {
                case LeftTriggerAction.Magnifier:
                    bool zoomed = System.Diagnostics.Process.GetProcessesByName("Magnify") is { Length: > 0 } running
                        && DisposeAll(running);
                    _engine.SendKeyCombo(zoomed
                        ? new[] { VirtualKey.LeftWindows, VirtualKey.Escape }
                        : new[] { VirtualKey.LeftWindows, VirtualKey.OemPlus });
                    ShowToast(zoomed ? "Zoom off" : "Zoomed in\nLT again to zoom out");
                    break;

                case LeftTriggerAction.Escape:
                    _engine.SendKeyPress(VirtualKey.Escape);
                    break;

                case LeftTriggerAction.SlowPointer:
                    _engine.SlowPointer = !_engine.SlowPointer;
                    ShowToast(_engine.SlowPointer ? "Slow pointer on\nLT again for normal speed" : "Slow pointer off");
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

        private readonly List<(ControllerControls Controls, Shape Shape, Brush Stroke, double Thickness)> _liveShapes = new();
        private readonly List<(ControllerControls Controls, Border Card)> _liveCards = new();

        private void InitializeGuide()
        {
            void Part(ControllerControls controls, Shape shape, Border card)
            {
                _liveShapes.Add((controls, shape, shape.Stroke, shape.StrokeThickness));
                _liveCards.Add((controls, card));
            }

            Part(ControllerControls.LeftTrigger, CtlLT, CardLT);
            Part(ControllerControls.RightTrigger, CtlRT, CardRT);
            Part(ControllerControls.LeftBumper, CtlLB, CardLB);
            Part(ControllerControls.RightBumper, CtlRB, CardRB);
            Part(ControllerControls.LeftStick, CtlLeftStick, CardLeftStick);
            Part(ControllerControls.RightStick, CtlRightStick, CardRightStick);
            Part(ControllerControls.DPad, CtlDPad, CardDPad);
            Part(ControllerControls.A, CtlA, CardA);
            Part(ControllerControls.B, CtlB, CardB);
            Part(ControllerControls.X, CtlX, CardX);
            Part(ControllerControls.Y, CtlY, CardY);
            Part(ControllerControls.Back, CtlBack, CardBack);
            Part(ControllerControls.Start, CtlStart, CardStart);

            // Pressing a stick in lights the stick as well as the Undo label
            _liveShapes.Add((ControllerControls.LeftStickPress, CtlLeftStick, CtlLeftStick.Stroke, CtlLeftStick.StrokeThickness));
            _liveShapes.Add((ControllerControls.RightStickPress, CtlRightStick, CtlRightStick.Stroke, CtlRightStick.StrokeThickness));
            _liveCards.Add((ControllerControls.LeftStickPress | ControllerControls.RightStickPress, CardStickPress));

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
                (ControllerPage, ControllerTabButton),
                (KeyboardPage, KeyboardTabButton),
            };

            foreach (var (element, tab) in pages)
            {
                bool showing = (string)tab.Tag == page;
                element.Visibility = showing ? Visibility.Visible : Visibility.Collapsed;
                tab.Background = showing ? OnBrush : OffBrush;
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

        private void UpdateGuideText()
        {
            HowToPointer.Visibility = _guideTyping ? Visibility.Collapsed : Visibility.Visible;
            HowToTyping.Visibility = _guideTyping ? Visibility.Visible : Visibility.Collapsed;
            HowToModeText.Text = _guideTyping ? "While the keyboard is open" : "While using the pointer";

            ActLT.Text = _guideTyping ? "Keyboard to top or bottom" : _userSettings.LeftTrigger switch
            {
                LeftTriggerAction.Magnifier => "Zoom in and out",
                LeftTriggerAction.Escape => "Close a menu (Escape)",
                LeftTriggerAction.SlowPointer => "Slow pointer on and off",
                _ => "Only used while typing"
            };

            (string title, string text) = _userSettings.LeftTrigger switch
            {
                LeftTriggerAction.Magnifier => ("See small things", "zooms in around the pointer. Press it again to zoom out."),
                LeftTriggerAction.Escape => ("Close a menu", "closes a menu or box opened by mistake, or ends a slideshow."),
                LeftTriggerAction.SlowPointer => ("Hit small things", "makes the pointer slow. Press it again for normal speed."),
                _ => ("", "")
            };
            HowToLeftTriggerTitle.Text = title;
            HowToLeftTriggerText.Text = text;
            HowToLeftTrigger.Visibility = title.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            ActRT.Text = "Hold to click and drag";
            ActLB.Text = _guideTyping ? "Backspace" : "Show open programs";
            ActRB.Text = _guideTyping ? "Space" : "Double click";
            ActY.Text = _guideTyping ? "Enter" : "Swap the two sticks";
            ActB.Text = _guideTyping ? "Capital, or the symbol on top" : "Right click";
            ActX.Text = _guideTyping ? "Close the keyboard" : "Open the keyboard";
            ActA.Text = _guideTyping ? "Type the key" : "Click";
            ActDPad.Text = _guideTyping ? "Move the orange box" : "Maximise, minimise, snap";

            // While typing, the left stick always steers the keyboard. Otherwise the
            // sticks follow Y's swap.
            ActLeftStick.Text = _guideTyping ? "Move the orange box" : _sticksSwapped ? "Scroll" : "Move the pointer";
            ActRightStick.Text = _sticksSwapped ? "Move the pointer" : "Scroll";
        }

        private void OnActiveControlsChanged(object? sender, ControllerControls active)
        {
            Dispatcher.BeginInvoke(() => ShowActiveControls(active));
        }

        private void ShowActiveControls(ControllerControls active)
        {
            var accent = (Brush)FindResource("AccentBrush");
            var cardBorder = (Brush)FindResource("CardBorderBrush");

            // A shape can be lit by more than one control - a stick by moving it or by
            // pressing it in - so work out each one's state before painting
            var litShapes = new HashSet<Shape>();
            foreach (var part in _liveShapes)
            {
                if ((active & part.Controls) != 0)
                {
                    litShapes.Add(part.Shape);
                }
            }

            foreach (var part in _liveShapes)
            {
                bool lit = litShapes.Contains(part.Shape);
                part.Shape.Stroke = lit ? accent : part.Stroke;
                part.Shape.StrokeThickness = lit ? 4 : part.Thickness;
            }

            foreach (var (controls, card) in _liveCards)
            {
                card.BorderBrush = (active & controls) != 0 ? accent : cardBorder;
            }

            // Holding Back and Start together brings this window back, so it lights the
            // label that says so
            var chord = ControllerControls.Back | ControllerControls.Start;
            CardRecovery.BorderBrush = (active & chord) == chord ? accent : cardBorder;
        }

        // ---------------------------------------------------------------------------
        // Try it out
        //
        // Beside the settings, so a change can be tried straight away: circles to click,
        // for pointer speed, feel, smoothing and click by resting; a box to type in, for
        // the keyboard; a list to scroll, for scroll speed. Nothing here is saved.
        // ---------------------------------------------------------------------------

        private readonly Random _random = new();
        private readonly List<Border> _targets = new();
        private int _hits;
        private int _misses;

        private void InitializeTryItOut()
        {
            // Big, middling and small: the small one is the size of an ordinary button
            foreach (double size in new[] { 110.0, 72.0, 44.0 })
            {
                var target = new Border
                {
                    Width = size,
                    Height = size,
                    CornerRadius = new CornerRadius(size / 2),
                    Background = OffBrush,
                    BorderBrush = (Brush)FindResource("AccentBrush"),
                    BorderThickness = new Thickness(3),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                target.MouseEnter += (_, _) => target.Background = OnBrush;
                target.MouseLeave += (_, _) => target.Background = OffBrush;
                target.MouseLeftButtonDown += (_, e) =>
                {
                    e.Handled = true;
                    _hits++;
                    target.Background = OffBrush;
                    Place(target);
                    ShowTargetScore();
                };
                _targets.Add(target);
                TargetCanvas.Children.Add(target);
            }

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
        }

        private void TargetCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            foreach (var target in _targets)
            {
                Place(target);
            }
        }

        private void TargetArea_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _misses++;
            ShowTargetScore();
        }

        private void TargetReset_Click(object sender, RoutedEventArgs e)
        {
            _hits = _misses = 0;
            foreach (var target in _targets)
            {
                Place(target);
            }
            ShowTargetScore();
        }

        private void ShowTargetScore() =>
            TargetScore.Text = $"Hits {_hits}    Misses {_misses}";

        /// <summary>
        /// Somewhere new in the box, clear of the other circles
        /// </summary>
        private void Place(Border target)
        {
            double width = TargetCanvas.ActualWidth, height = TargetCanvas.ActualHeight;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            for (int attempt = 0; attempt < 30; attempt++)
            {
                double x = _random.NextDouble() * Math.Max(0, width - target.Width);
                double y = _random.NextDouble() * Math.Max(0, height - target.Height);
                var spot = new Rect(x, y, target.Width, target.Height);
                spot.Inflate(12, 12);

                bool clear = true;
                foreach (var other in _targets)
                {
                    if (other != target && spot.IntersectsWith(new Rect(Canvas.GetLeft(other), Canvas.GetTop(other), other.Width, other.Height)))
                    {
                        clear = false;
                        break;
                    }
                }

                if (clear || attempt == 29)
                {
                    Canvas.SetLeft(target, x);
                    Canvas.SetTop(target, y);
                    return;
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

        private void LeftTriggerChoice_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string tag } && Enum.TryParse(tag, out LeftTriggerAction action))
            {
                ChangeSettings(s => s.LeftTrigger = action);
            }
        }

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

                // Slow pointer only stays on while LT is what switches it
                if (_userSettings.LeftTrigger != LeftTriggerAction.SlowPointer)
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

            LeftTriggerMagnifierButton.Background = _userSettings.LeftTrigger == LeftTriggerAction.Magnifier ? OnBrush : OffBrush;
            LeftTriggerEscapeButton.Background = _userSettings.LeftTrigger == LeftTriggerAction.Escape ? OnBrush : OffBrush;
            LeftTriggerSlowButton.Background = _userSettings.LeftTrigger == LeftTriggerAction.SlowPointer ? OnBrush : OffBrush;
            LeftTriggerNothingButton.Background = _userSettings.LeftTrigger == LeftTriggerAction.Nothing ? OnBrush : OffBrush;
            LeftTriggerExplanation.Text = _userSettings.LeftTrigger switch
            {
                LeftTriggerAction.Magnifier => "Zoom: Windows Magnifier - bigger text and targets around the pointer. LT again to zoom out.",
                LeftTriggerAction.Escape => "Escape: closes a menu or box opened by mistake, or ends a slideshow.",
                LeftTriggerAction.SlowPointer => "Slow pointer: LT turns a slower pointer on for small targets, and off again.",
                _ => "Nothing: LT is only used while the keyboard is open."
            };
            SlowPointerValue.Text = $"{_userSettings.SlowPointerPercent}%";
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

