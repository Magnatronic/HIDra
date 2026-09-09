using HIDra.Core.Actions;
using HIDra.Core.Controllers;
using HIDra.Core.Input;
using HIDra.Core.Simulation;
using HIDra.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace HIDra.Core;

/// <summary>
/// Main engine that coordinates controller input and output simulation
/// </summary>
public class HIDraEngine : IDisposable
{
    private readonly XboxControllerService _controllerService;
    private readonly InputProcessor _inputProcessor;
    private readonly MouseSimulator _mouseSimulator;
    private readonly KeyboardSimulator _keyboardSimulator;
    private readonly ButtonActionHandler _buttonActionHandler;
    private readonly InputSettings _settings;
    private readonly Dictionary<string, ButtonMapping> _buttonMappings;

    private ControllerState? _previousState;
    private bool _isRunning;
    
    // Scroll accumulation for smooth, gradual scrolling
    private float _scrollAccumulatorX = 0f;
    private float _scrollAccumulatorY = 0f;
    
    // Task switcher state tracking
    private bool _isTaskSwitcherOpen = false;
    
    // Stick mode swap state
    private bool _useRightStickForCursor = false;

    // Recovery chord: holding Back and Start together brings the HIDra window back.
    private System.Diagnostics.Stopwatch? _recoveryChordTimer;
    private bool _recoveryChordFired;
    private const int RecoveryChordHoldMs = 1000;
    
    // Grid 3 detection
    private System.Timers.Timer? _grid3CheckTimer;
    private volatile bool _grid3Detected = false;
    private IntPtr _winEventHookHandle = IntPtr.Zero;
    private WinEventDelegate? _winEventDelegate; // Keep reference to prevent GC
    private POINT _cursorPositionBeforeHide;
    
    // Win32 API declarations for window event hooks
    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
    
    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
    
    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
    
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    
    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);
    
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);
    
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);
    
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    /// <summary>
    /// Event raised when controller connection changes
    /// </summary>
    public event EventHandler<ControllerInfo>? ConnectionChanged;

    /// <summary>
    /// Event raised when an error occurs
    /// </summary>
    public event EventHandler<string>? ErrorOccurred;
    
    /// <summary>
    /// Event raised when stick mode changes (left/right swap)
    /// </summary>
    public event EventHandler<bool>? StickModeChanged;
    
    /// <summary>
    /// Event raised when virtual keyboard toggle is requested
    /// </summary>
    public event EventHandler? VirtualKeyboardToggleRequested;

    /// <summary>
    /// Event raised when the controller battery power source or charge level changes
    /// </summary>
    public event EventHandler<ControllerBattery>? BatteryChanged;

    /// <summary>
    /// Event raised when the user asks for the HIDra window back using the recovery
    /// chord. Without this there is no way to reach the window again once it is hidden,
    /// because reaching it would require the mouse the user does not have.
    /// </summary>
    public event EventHandler? ShowWindowRequested;

    /// <summary>
    /// Event raised when a controller is attached that XInput cannot use, typically
    /// because it is in DirectInput mode rather than XInput mode.
    /// </summary>
    public event EventHandler<Controllers.NonXInputController>? UnusableControllerDetected;

    /// <summary>
    /// Current battery state, if a controller is attached
    /// </summary>
    public ControllerBattery? Battery => _controllerService.Battery;

    /// <summary>
    /// Current controller information
    /// </summary>
    public ControllerInfo? Controller => _controllerService.Controller;

    /// <summary>
    /// Is the engine currently running
    /// </summary>
    public bool IsRunning => _isRunning;

    public HIDraEngine(InputSettings? settings = null, Dictionary<string, ButtonMapping>? buttonMappings = null)
    {
        _settings = settings ?? new InputSettings();
        _buttonMappings = buttonMappings ?? new Dictionary<string, ButtonMapping>();
        _controllerService = new XboxControllerService();
        _inputProcessor = new InputProcessor(_settings);
        _mouseSimulator = new MouseSimulator();
        _keyboardSimulator = new KeyboardSimulator();
        _buttonActionHandler = new ButtonActionHandler(_keyboardSimulator, _mouseSimulator);

        _controllerService.ConnectionChanged += OnConnectionChanged;
        _controllerService.StateUpdated += OnStateUpdated;
        _controllerService.BatteryChanged += (s, battery) => BatteryChanged?.Invoke(this, battery);
        _controllerService.UnusableControllerDetected += (s, c) => UnusableControllerDetected?.Invoke(this, c);
        _buttonActionHandler.TaskSwitcherRequested += OnTaskSwitcherRequested;
        _buttonActionHandler.StickModeSwapRequested += OnStickModeSwapRequested;
        _buttonActionHandler.ToggleOnScreenKeyboardRequested += OnToggleOnScreenKeyboardRequested;
    }

    /// <summary>
    /// Check whether a controller is present right now, so the UI can say so at startup.
    ///
    /// A false result is not a failure and must not stop anything: Start() supervises
    /// continuously and will pick up a controller whenever one appears. This matters
    /// because HIDra launches at logon, which can easily happen before a member of
    /// staff has finished plugging the controller in.
    /// </summary>
    public bool DetectController()
    {
        try
        {
            return _controllerService.DetectController() != null;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Initialization error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Start processing controller input
    /// </summary>
    public void Start()
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        _controllerService.StartPolling(_settings.PollRateMs);
        
        // Only enable Grid 3 detection if the setting is enabled
        if (_settings.EnableGrid3AutoSuspend)
        {
            // Install window event hook for instant foreground window change detection
            _winEventDelegate = new WinEventDelegate(WinEventCallback);
            _winEventHookHandle = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _winEventDelegate,
                0, 0, WINEVENT_OUTOFCONTEXT);
            
            // Backstop for the rare case where the foreground hook misses a change.
            // The hook above is what makes detection feel instant, so this only needs
            // to be occasional - at 100ms it was enumerating every process on the
            // machine ten times a second for the entire session.
            _grid3CheckTimer = new System.Timers.Timer(2000);
            _grid3CheckTimer.Elapsed += (s, e) => CheckForGrid3();
            _grid3CheckTimer.Start();
            
            // Initial check
            CheckForGrid3();
        }
    }

    /// <summary>
    /// Stop processing controller input
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;
        _controllerService.StopPolling();
        
        // Stop Grid 3 detection if it was enabled
        // Unhook window event
        if (_winEventHookHandle != IntPtr.Zero)
        {
            UnhookWinEvent(_winEventHookHandle);
            _winEventHookHandle = IntPtr.Zero;
        }
        
        // Stop Grid 3 detection timer
        if (_grid3CheckTimer != null)
        {
            _grid3CheckTimer.Stop();
            _grid3CheckTimer.Dispose();
            _grid3CheckTimer = null;
        }
        
        // If we are shutting down while suspended for Grid 3, the cursor is still parked
        // off-screen. Put it back, or it stays invisible after HIDra exits.
        if (_grid3Detected)
        {
            SetCursorPos(_cursorPositionBeforeHide.X, _cursorPositionBeforeHide.Y);
            _grid3Detected = false;
        }

        // Release all inputs
        _mouseSimulator.ReleaseAll();
        _keyboardSimulator.ReleaseAll();
    }
    
    /// <summary>
    /// Send a key press (for virtual keyboard)
    /// </summary>
    public void SendKeyPress(VirtualKey key)
    {
        _keyboardSimulator.KeyPress(key);
    }
    
    /// <summary>
    /// Send text (for virtual keyboard)
    /// </summary>
    public void SendText(string text)
    {
        _keyboardSimulator.TypeText(text);
    }

    /// <summary>
    /// Send a modifier shortcut such as Ctrl+C from the virtual keyboard.
    /// </summary>
    public void SendKeyCombo(params VirtualKey[] keys)
    {
        _keyboardSimulator.KeyPress(keys);
    }

    /// <summary>
    /// Handle controller connection changes
    /// </summary>
    private void OnConnectionChanged(object? sender, ControllerInfo info)
    {
        if (info.IsConnected)
        {
            // Discard the state captured before the controller vanished. Comparing a
            // fresh press against a stale snapshot would fire phantom button actions
            // the moment the controller comes back.
            _previousState = null;
        }
        else
        {
            // Release anything the controller was holding. This matters most for the
            // task switcher, which holds Alt down: if the controller dies mid-switch,
            // a stuck Alt key makes the whole machine unusable for everyone.
            _mouseSimulator.ReleaseAll();
            _keyboardSimulator.ReleaseAll();
            _isTaskSwitcherOpen = false;
        }

        // Deliberately does not stop the engine. The service keeps scanning and will
        // reattach on its own - requiring a click on "Reconnect" left the user stranded,
        // because clicking it needs the mouse that HIDra is there to provide.
        ConnectionChanged?.Invoke(this, info);
    }

    /// <summary>
    /// Handle controller state updates
    /// </summary>
    private void OnStateUpdated(object? sender, ControllerState state)
    {
        if (!_isRunning)
        {
            return;
        }
        
        // Skip processing if Grid 3 is active
        // (Grid 3 detection happens via window event hook + backup timer)
        if (_grid3Detected)
        {
            return;
        }

        try
        {
            ProcessControllerState(state);
            _previousState = state.Clone();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error processing input: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Called instantly when foreground window changes
    /// </summary>
    private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (eventType == EVENT_SYSTEM_FOREGROUND)
        {
            CheckForGrid3();
        }
    }

    /// <summary>
    /// Process controller state and generate output
    /// </summary>
    private void ProcessControllerState(ControllerState state)
    {
        // Check for precision mode (Left Trigger)
        bool precisionMode = _inputProcessor.IsTriggerPressed(state.LeftTrigger);

        // Process mouse and scroll - swap sticks based on mode
        if (_useRightStickForCursor)
        {
            // Right stick for cursor, left stick for scroll
            var (mouseX, mouseY) = _inputProcessor.ProcessMouseMovementFromRightStick(state, precisionMode);
            _mouseSimulator.MoveMouse(mouseX, mouseY);
            
            var (scrollX, scrollY) = _inputProcessor.ProcessScrollFromLeftStick(state);
            AccumulateAndApplyScroll(scrollX, scrollY);
        }
        else
        {
            // Default: Left stick for cursor, right stick for scroll
            var (mouseX, mouseY) = _inputProcessor.ProcessMouseMovement(state, precisionMode);
            _mouseSimulator.MoveMouse(mouseX, mouseY);
            
            var (scrollX, scrollY) = _inputProcessor.ProcessScroll(state);
            AccumulateAndApplyScroll(scrollX, scrollY);
        }

        // Process right trigger for click and hold
        bool holdMode = _inputProcessor.IsTriggerPressed(state.RightTrigger);
        if (holdMode)
        {
            _mouseSimulator.LeftButtonDown();
        }
        else
        {
            _mouseSimulator.LeftButtonUp();
        }

        // Recovery chord is checked before button dispatch so it can suppress the
        // second button's own action while the chord is being formed.
        UpdateRecoveryChord(state);

        // Process buttons
        if (_previousState != null)
        {
            ProcessButtons(state, _previousState);
        }
    }

    /// <summary>
    /// Watches for Back and Start being held together, and asks the UI to show the
    /// window once they have been held long enough.
    ///
    /// A deliberate hold is required so that pressing both in quick succession during
    /// ordinary use does not summon the window unexpectedly.
    /// </summary>
    private void UpdateRecoveryChord(ControllerState state)
    {
        bool chordHeld = state.Back && state.Start;

        if (!chordHeld)
        {
            _recoveryChordTimer = null;
            _recoveryChordFired = false;
            return;
        }

        _recoveryChordTimer ??= System.Diagnostics.Stopwatch.StartNew();

        if (!_recoveryChordFired && _recoveryChordTimer.ElapsedMilliseconds >= RecoveryChordHoldMs)
        {
            _recoveryChordFired = true;
            ShowWindowRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Process button presses
    /// </summary>
    private void ProcessButtons(ControllerState current, ControllerState previous)
    {
        // No modifier keys - simplified for accessibility
        string? activeModifier = null;

        // Handle task switcher special cases for A and B buttons
        if (_isTaskSwitcherOpen)
        {
            if (_inputProcessor.IsButtonPressed(current.ButtonA, previous.ButtonA))
            {
                CloseTaskSwitcher(); // This releases Alt, which activates the selected window
                return; // Don't process as a click
            }

            if (_inputProcessor.IsButtonPressed(current.ButtonB, previous.ButtonB))
            {
                _keyboardSimulator.KeyPress(VirtualKey.Escape); // Close switcher
                _isTaskSwitcherOpen = false;
                return; // Don't process as right-click
            }
        }

        // Process all button mappings
        ProcessButton("ButtonA", current.ButtonA, previous.ButtonA, activeModifier);
        ProcessButton("ButtonB", current.ButtonB, previous.ButtonB, activeModifier);
        ProcessButton("ButtonX", current.ButtonX, previous.ButtonX, activeModifier);
        ProcessButton("ButtonY", current.ButtonY, previous.ButtonY, activeModifier);
        ProcessButton("LeftBumper", current.LeftBumper, previous.LeftBumper, activeModifier);
        ProcessButton("RightBumper", current.RightBumper, previous.RightBumper, activeModifier);
        // While the recovery chord is being formed, only the button pressed first runs
        // its normal action. Suppressing the second one stops the chord from also
        // firing Task View or the Start menu on top of restoring the window.
        if (!(current.Back && current.Start))
        {
            ProcessButton("Back", current.Back, previous.Back, activeModifier);
            ProcessButton("Start", current.Start, previous.Start, activeModifier);
        }

        ProcessButton("DpadUp", current.DpadUp, previous.DpadUp, activeModifier);
        ProcessButton("DpadDown", current.DpadDown, previous.DpadDown, activeModifier);
        ProcessButton("DpadLeft", current.DpadLeft, previous.DpadLeft, activeModifier);
        ProcessButton("DpadRight", current.DpadRight, previous.DpadRight, activeModifier);
        ProcessButton("LeftStickClick", current.LeftStickClick, previous.LeftStickClick, activeModifier);
        ProcessButton("RightStickClick", current.RightStickClick, previous.RightStickClick, activeModifier);
    }

    /// <summary>
    /// Process a single button press using configured mappings
    /// </summary>
    private void ProcessButton(string buttonName, bool currentState, bool previousState, string? modifier)
    {
        if (!_inputProcessor.IsButtonPressed(currentState, previousState))
            return;

        // Check if we have a mapping for this button
        if (!_buttonMappings.TryGetValue(buttonName, out var buttonMapping))
            return;

        ActionMapping? actionToExecute = null;

        // Check modifier-specific mapping first
        if (modifier != null && buttonMapping.Modifiers.TryGetValue(modifier, out var modifierAction))
        {
            actionToExecute = modifierAction;
        }
        // Fall back to default mapping if no modifier action exists
        else if (buttonMapping.Default != null)
        {
            actionToExecute = buttonMapping.Default;
        }

        // Execute the action if found
        if (actionToExecute != null)
        {
            _buttonActionHandler.ExecuteAction(actionToExecute);
        }
    }

    private void HandleTaskSwitcher(bool forward)
    {
        // If task switcher is not open, open it (this moves 1 position)
        if (!_isTaskSwitcherOpen)
        {
            _keyboardSimulator.EnterTaskSwitcher(); // Holds Alt + presses Tab once
            _isTaskSwitcherOpen = true;
            // Don't navigate again - EnterTaskSwitcher already moved us once
        }
        else
        {
            // Task switcher is already open, navigate to next/previous
            if (forward)
                _keyboardSimulator.TaskSwitcherNext();
            else
                _keyboardSimulator.TaskSwitcherPrevious();
        }
    }

    private void CloseTaskSwitcher()
    {
        if (_isTaskSwitcherOpen)
        {
            _keyboardSimulator.ExitTaskSwitcher();
            _isTaskSwitcherOpen = false;
        }
    }

    private void OnTaskSwitcherRequested(object? sender, bool forward)
    {
        HandleTaskSwitcher(forward);
    }
    
    private void OnStickModeSwapRequested(object? sender, EventArgs e)
    {
        _useRightStickForCursor = !_useRightStickForCursor;
        StickModeChanged?.Invoke(this, _useRightStickForCursor);
    }
    
    private void OnToggleOnScreenKeyboardRequested(object? sender, EventArgs e)
    {
        // Raise event for UI layer to handle virtual keyboard
        VirtualKeyboardToggleRequested?.Invoke(this, EventArgs.Empty);
    }
    
    private void AccumulateAndApplyScroll(float scrollX, float scrollY)
    {
        // Accumulate scroll values over time
        _scrollAccumulatorX += scrollX;
        _scrollAccumulatorY += scrollY;
        
        // When accumulated value reaches threshold, perform scroll and reset
        if (Math.Abs(_scrollAccumulatorY) >= 0.5f)
        {
            int scrollAmount = (int)Math.Round(_scrollAccumulatorY);
            _mouseSimulator.ScrollVertical(scrollAmount);
            _scrollAccumulatorY -= scrollAmount; // Keep remainder for next frame
        }
        
        if (Math.Abs(_scrollAccumulatorX) >= 0.5f)
        {
            int scrollAmount = (int)Math.Round(_scrollAccumulatorX);
            _mouseSimulator.ScrollHorizontal(scrollAmount);
            _scrollAccumulatorX -= scrollAmount; // Keep remainder for next frame
        }
    }
    
    /// <summary>
    /// Process names of AAC / switch-access applications that HIDra should stand aside for.
    ///
    /// Matched whole rather than by substring: the previous check also treated any
    /// process merely starting with "Grid " or containing "Communicator" as a match,
    /// which would suspend HIDra for unrelated software that happened to be named
    /// similarly. This wants to become a user-editable list rather than a constant.
    /// </summary>
    private static readonly string[] SuspendingApplicationNames =
    {
        "Grid 3",
        "Grid3",
        "Communicator",
        "Communicator 5"
    };

    private static bool IsSuspendingApplication(string processName)
    {
        foreach (var name in SuspendingApplicationNames)
        {
            if (processName.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a suspending application (Grid 3 and similar) is running
    /// </summary>
    private void CheckForGrid3()
    {
        try
        {
            // If Grid 3 auto-suspend is disabled, make sure we're not in suspended state
            if (!_settings.EnableGrid3AutoSuspend)
            {
                // If we were previously suspended, restore cursor and clear state
                if (_grid3Detected)
                {
                    SetCursorPos(_cursorPositionBeforeHide.X, _cursorPositionBeforeHide.Y);
                    _grid3Detected = false;
                }
                return;
            }
            
            // Check if Grid 3 process exists
            // Once detected, HIDra stays suspended until Grid 3 is closed
            // Each Process returned here owns an OS handle. They must be disposed or
            // the handle count climbs for as long as the app runs - which, started at
            // logon, is all day.
            bool isGrid3Running = false;

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (IsSuspendingApplication(process.ProcessName))
                    {
                        isGrid3Running = true;
                    }
                }
                catch
                {
                    // Skip processes we can't query.
                }
                finally
                {
                    process.Dispose();
                }
            }
            
            // Update detection state
            if (isGrid3Running != _grid3Detected)
            {
                _grid3Detected = isGrid3Running;
                
                if (_grid3Detected)
                {
                    // Release all inputs when Grid 3 is detected
                    _mouseSimulator.ReleaseAll();
                    _keyboardSimulator.ReleaseAll();
                    
                    // Save current cursor position before hiding it
                    // This allows us to restore the exact position when Grid 3 closes
                    GetCursorPos(out _cursorPositionBeforeHide);
                    
                    // Move cursor off-screen to top-left corner (-10, -10)
                    // This prevents the cursor from highlighting cells in Grid 3, which can be
                    // confusing when using the controller as a switch for navigation
                    SetCursorPos(-10, -10);
                }
                else
                {
                    // Grid 3 has closed - restore cursor to its previous position
                    // This returns control to the user exactly where they left off
                    SetCursorPos(_cursorPositionBeforeHide.X, _cursorPositionBeforeHide.Y);
                }
            }
        }
        catch
        {
            // Ignore errors
        }
    }

    public void Dispose()
    {
        Stop();
        _controllerService?.Dispose();
        _mouseSimulator?.Dispose();
        _keyboardSimulator?.Dispose();
    }
}
