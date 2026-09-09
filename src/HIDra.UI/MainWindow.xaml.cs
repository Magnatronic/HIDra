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

                InitializeVirtualKeyboard();

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
                ConnectButton.IsEnabled = false;
                _trayIcon?.UpdateStatus(_engine.Controller?.IsConnected == true, _engine.Battery);
            }
            catch (Exception ex)
            {
                UpdateStatus(ConnectionStatus.Error, $"Could not start: {ex.Message}");
                ConnectButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Manual restart of the engine. Rarely needed now that reconnection is
        /// automatic, but kept so staff have a way to reset things.
        /// </summary>
        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            _engine?.Stop();
            _engine?.Dispose();
            _engine = null;

            StartEngine();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _engine?.Stop();
                _engine?.Dispose();
                _engine = null;

                UpdateStatus(ConnectionStatus.Disconnected, "Stopped - press Reconnect to resume controller input");
                ConnectButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                _trayIcon?.UpdateStatus(false, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping engine:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

        private void OnShowWindowRequested(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(RestoreWindow);
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
                    }
                }
            });
        }
        
        private void OnVirtualKeyboardKeyPressed(object? sender, WindowsInput.Native.VirtualKeyCode key)
        {
            _engine?.SendKeyPress(key);
        }
        
        private void OnVirtualKeyboardTextEntered(object? sender, string text)
        {
            _engine?.SendText(text);
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

            _virtualKeyboard?.Close();
            _engine?.Stop();
            _engine?.Dispose();
            _trayIcon?.Dispose();

            Application.Current.Shutdown();
        }
    }
}

