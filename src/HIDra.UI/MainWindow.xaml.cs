using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using HIDra.Core;
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
        private System.Windows.Threading.DispatcherTimer? _startRetryTimer;

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
                CursorSensitivity = 0.5f,           // Moderate speed for control
                ScrollSensitivity = 0.5f,           // Optimized for smooth scrolling
                PrecisionModeSensitivity = 0.3f,    // Very slow for precise work
                Deadzone = 0.05f,                   // Low deadzone (5%) for maximum control
                PollRateMs = 10,                    // 100Hz polling rate
                StickCalibrationMax = 0.90f,        // Compensate for worn controllers
                TriggerThreshold = 0.3f,            // 30% trigger press to activate
                EnableGrid3AutoSuspend = true       // Grid 3 auto-suspend enabled by default
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

            return (settings, buttonMappings);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeTrayIcon();
            StartEngine();
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
                _engine.BatteryChanged += OnBatteryChanged;
                _engine.ShowWindowRequested += OnShowWindowRequested;
                _engine.StickModeChanged += OnStickModeChanged;
                _engine.UnusableControllerDetected += OnUnusableControllerDetected;
                _engine.PausedChanged += OnPausedChanged;
                _engine.KeyboardNavigateRequested += OnKeyboardNavigate;
                _engine.KeyboardSelectRequested += OnKeyboardSelect;
                _engine.KeyboardSelectShiftedRequested += OnKeyboardSelectShifted;

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

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var helpWindow = new HelpWindow
            {
                Owner = this
            };
            helpWindow.ShowDialog();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_engine == null)
            {
                MessageBox.Show("Please connect a controller first before adjusting settings.", 
                    "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Get current settings from the engine
            var settings = GetEngineSettings();
            if (settings != null)
            {
                var settingsWindow = new SettingsWindow(settings)
                {
                    Owner = this
                };
                settingsWindow.ShowDialog();
            }
        }

        private InputSettings? GetEngineSettings()
        {
            // Access the private _settings field via reflection
            var engineType = typeof(HIDraEngine);
            var settingsField = engineType.GetField("_settings", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return settingsField?.GetValue(_engine) as InputSettings;
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
                    ? new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00))
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
            WindowState = WindowState.Normal;
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
                ConnectionStatus.Connecting => Color.FromRgb(0xFF, 0x98, 0x00), // Orange
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
                _virtualKeyboard = new VirtualKeyboardWindow();
                _virtualKeyboard.KeyPressed += OnVirtualKeyboardKeyPressed;
                _virtualKeyboard.TextEntered += OnVirtualKeyboardTextEntered;
                _virtualKeyboard.KeyComboPressed += OnVirtualKeyboardKeyCombo;

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
                };
            }
        }
        
        private void OnVirtualKeyboardToggleRequested(object? sender, EventArgs e)
        {
            // Use Dispatcher to ensure we're on the UI thread
            Dispatcher.BeginInvoke(() =>
            {
                if (_virtualKeyboard == null)
                {
                    InitializeVirtualKeyboard();
                }
                
                if (_virtualKeyboard != null)
                {
                    if (_virtualKeyboard.IsVisible)
                    {
                        _virtualKeyboard.Hide();
                    }
                    else
                    {
                        _virtualKeyboard.Show();

                        // Start with a key highlighted, so the first press types
                        // something rather than only revealing where the highlight is.
                        if (!_virtualKeyboard.HasHighlight)
                        {
                            _virtualKeyboard.MoveHighlight(KeyboardNavigationDirection.Right);
                        }
                    }

                }
            });
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

        private void OnVirtualKeyboardKeyCombo(object? sender, VirtualKey[] keys)
        {
            _engine?.SendKeyCombo(keys);
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
            _virtualKeyboard?.Close();
            _modeToast?.Close();
            _engine?.Stop();
            _engine?.Dispose();
            _trayIcon?.Dispose();

            Application.Current.Shutdown();
        }
    }
}

