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
                TriggerThreshold = 0.3f             // 30% trigger press to activate
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
                    Default = new ActionMapping { Action = "TaskSwitcherForward", Description = "Next application" }
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
                }
            };

            return (settings, buttonMappings);
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateStatus(ConnectionStatus.Disconnected, "Searching for controller...");
            
            // Auto-connect on startup (accessibility feature - user can't click with mouse if they need controller to BE the mouse)
            await AutoConnectAsync();
        }

        private async System.Threading.Tasks.Task AutoConnectAsync()
        {
            try
            {
                ConnectButton.IsEnabled = false;
                UpdateStatus(ConnectionStatus.Connecting, "Auto-detecting controller...");

                // Get hardcoded configuration (no files needed - student-proof!)
                var (settings, buttonMappings) = CreateHardcodedConfiguration();

                // Create engine with hardcoded settings and button mappings
                _engine = new HIDraEngine(settings, buttonMappings);
                _engine.ConnectionChanged += OnConnectionChanged;
                _engine.ErrorOccurred += OnErrorOccurred;
                _engine.VirtualKeyboardToggleRequested += OnVirtualKeyboardToggleRequested;

                // Initialize virtual keyboard window
                InitializeVirtualKeyboard();

                // Initialize and detect controller
                bool initialized = await _engine.InitializeAsync();

                if (initialized)
                {
                    // Start processing input immediately
                    _engine.Start();
                    
                    var controller = _engine.Controller;
                    if (controller != null)
                    {
                        UpdateStatus(ConnectionStatus.Connected, 
                            $"{controller.Name} connected and active - Ready to use!");
                    }

                    StopButton.IsEnabled = true;
                }
                else
                {
                    UpdateStatus(ConnectionStatus.Disconnected, 
                        "No Xbox controller found. Please connect one and click 'Connect'.");
                    ConnectButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                UpdateStatus(ConnectionStatus.Disconnected, 
                    $"Auto-connect failed: {ex.Message}. Click 'Connect' to retry.");
                ConnectButton.IsEnabled = true;
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ConnectButton.IsEnabled = false;
                UpdateStatus(ConnectionStatus.Connecting, "Searching for controller...");

                // Clean up existing engine if any
                if (_engine != null)
                {
                    _engine.Stop();
                    _engine.Dispose();
                    _engine = null;
                }

                // Get hardcoded configuration (no files needed - student-proof!)
                var (settings, buttonMappings) = CreateHardcodedConfiguration();

                // Create engine with hardcoded settings and button mappings
                _engine = new HIDraEngine(settings, buttonMappings);
                _engine.ConnectionChanged += OnConnectionChanged;
                _engine.ErrorOccurred += OnErrorOccurred;
                _engine.VirtualKeyboardToggleRequested += OnVirtualKeyboardToggleRequested;

                // Initialize virtual keyboard window
                InitializeVirtualKeyboard();

                // Initialize and detect controller
                bool initialized = await _engine.InitializeAsync();

                if (initialized)
                {
                    // Start processing input
                    _engine.Start();
                    
                    var controller = _engine.Controller;
                    if (controller != null)
                    {
                        UpdateStatus(ConnectionStatus.Connected, 
                            $"{controller.Name} connected and active");
                    }

                    StopButton.IsEnabled = true;
                }
                else
                {
                    UpdateStatus(ConnectionStatus.Disconnected, 
                        "No Xbox controller found. Please connect one and try again.");
                    ConnectButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error connecting to controller:\n{ex.Message}", 
                    "Connection Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
                
                UpdateStatus(ConnectionStatus.Error, "Connection failed");
                ConnectButton.IsEnabled = true;
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _engine?.Stop();
                _engine?.Dispose();
                _engine = null;

                UpdateStatus(ConnectionStatus.Disconnected, "Controller disconnected");
                ConnectButton.IsEnabled = true;
                StopButton.IsEnabled = false;
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

        private void OnConnectionChanged(object? sender, ControllerInfo info)
        {
            Dispatcher.Invoke(() =>
            {
                if (info.IsConnected)
                {
                    UpdateStatus(ConnectionStatus.Connected, $"{info.Name} connected");
                }
                else
                {
                    UpdateStatus(ConnectionStatus.Disconnected, "Controller disconnected");
                    ConnectButton.IsEnabled = true;
                    StopButton.IsEnabled = false;
                }
            });
        }

        private void OnErrorOccurred(object? sender, string error)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(error, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            Dispatcher.Invoke(() =>
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
            _virtualKeyboard?.Close();
            _engine?.Stop();
            _engine?.Dispose();
        }
    }
}

