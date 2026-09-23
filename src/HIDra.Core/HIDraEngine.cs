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
    private readonly InputFilter _inputFilter;
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

    // Dwell click: armed by cursor movement, fired once the cursor has been still long
    // enough, then disarmed until it moves again - so resting never clicks repeatedly.
    private readonly System.Diagnostics.Stopwatch _dwellStillTimer = new();
    private bool _dwellArmed;
    private bool _dwellCountingDown;

    // How far the cursor has drifted since it last counted as moving
    private double _dwellDriftX;
    private double _dwellDriftY;

    /// <summary>
    /// Movement within this many pixels still counts as resting. Without it, the small
    /// wobble of an unsteady hand - or a stick not quite centred - restarted the count
    /// on every frame, so a dwell click might never arrive for the people who need it.
    /// </summary>
    private const double DwellMoveTolerancePixels = 6;

    /// <summary>
    /// How long the cursor must have settled before the countdown ring appears. Showing
    /// it on the first still frame made it flash on and off beside the cursor during
    /// any slow or hesitant movement, which looked like the cursor flickering.
    /// </summary>
    private const double DwellRingDelaySeconds = 0.3;

    // Measures how long each frame actually took, so cursor speed can be expressed in
    // pixels per second rather than per frame. Poll timing is not reliable enough to
    // treat every frame as equal.
    private readonly System.Diagnostics.Stopwatch _frameTimer = System.Diagnostics.Stopwatch.StartNew();
    private double _lastFrameSeconds;

    /// <summary>
    /// Longest frame duration we will act on. A stalled thread or a machine waking from
    /// sleep would otherwise produce one enormous delta and fling the cursor across the
    /// screen.
    /// </summary>
    private const double MaxFrameSeconds = 0.05;

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
    /// Event raised when the keyboard should flip between the top and bottom of the
    /// screen (Left Trigger), so it stops covering what the student is typing into
    /// </summary>
    public event EventHandler? KeyboardPositionToggleRequested;

    /// <summary>
    /// A dwell click has started counting down, with the seconds left until it clicks.
    /// Lets the UI show a countdown by the cursor, so a click is never a surprise.
    /// </summary>
    public event EventHandler<double>? DwellCountdownStarted;

    /// <summary>
    /// A dwell countdown ended - either it clicked, or the cursor moved or a button was
    /// pressed first.
    /// </summary>
    public event EventHandler? DwellCountdownEnded;

    /// <summary>
    /// The controller is being used - a button, a trigger or a stick. Raised at most a
    /// few times a second, so the UI can wake a faded keyboard without being flooded.
    /// </summary>
    public event EventHandler? InputActivity;

    private readonly System.Diagnostics.Stopwatch _activityThrottle = System.Diagnostics.Stopwatch.StartNew();
    private const int ActivityThrottleMs = 150;

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

    /// <summary>
    /// The live settings. Changing them takes effect on the next frame, which is how the
    /// main screen adjusts speed and dwell while the controller is in use.
    /// </summary>
    public InputSettings Settings => _settings;

    /// <summary>
    /// True while controller input is deliberately suppressed.
    ///
    /// Pausing is not the same as stopping. The controller is still polled and the
    /// recovery chord is still watched, so the user can resume without help; tearing
    /// the engine down instead would leave the one person who depends on it with no
    /// pointer and no way to bring it back.
    /// </summary>
    public bool IsPaused { get; private set; }

    /// <summary>
    /// Raised when input is paused or resumed, so the UI can say which.
    /// </summary>
    public event EventHandler<bool>? PausedChanged;

    /// <summary>
    /// True while the on-screen keyboard is open and the D-pad should move its
    /// highlight rather than manage windows.
    /// </summary>
    public bool KeyboardNavigationActive { get; set; }

    /// <summary>Move the on-screen keyboard highlight one key.</summary>
    public event EventHandler<KeyboardNavigationDirection>? KeyboardNavigateRequested;

    /// <summary>Press the highlighted key on the on-screen keyboard.</summary>
    public event EventHandler? KeyboardSelectRequested;

    /// <summary>
    /// Press the highlighted key shifted, giving the symbol printed above it or the
    /// capital letter. Saves travelling to the Shift key and back for every one.
    /// </summary>
    public event EventHandler? KeyboardSelectShiftedRequested;

    /// <summary>
    /// Type Backspace, Space or Enter on the on-screen keyboard, from LB, RB or Y.
    /// </summary>
    public event EventHandler<KeyboardQuickKey>? KeyboardQuickKeyRequested;

    /// <summary>
    /// The set of controls in use has changed - raised only on a change, not every
    /// poll, so the main screen can light up what is pressed without being flooded.
    /// </summary>
    public event EventHandler<ControllerControls>? ActiveControlsChanged;

    private ControllerControls _activeControls;

    // Held-direction repeat, so crossing the keyboard does not mean one press per key.
    private KeyboardNavigationDirection? _heldDirection;
    private readonly System.Diagnostics.Stopwatch _keyRepeatTimer = new();
    private int _keyRepeatCount;

    /// <summary>
    /// Pause before a held direction starts repeating. Long enough that a deliberate
    /// single step never runs on by itself.
    /// </summary>
    private const int KeyRepeatDelayMs = 450;

    /// <summary>Interval between repeats once they start.</summary>
    private const int KeyRepeatIntervalMs = 130;

    /// <summary>
    /// How far the stick must be pushed before it steps to the next key. Deliberately
    /// well above the deadzone used for cursor movement: a stray step lands the
    /// highlight on the wrong letter, which costs more to undo than a little cursor
    /// drift does.
    /// </summary>
    private const float KeyboardStickEngageThreshold = 0.55f;

    /// <summary>
    /// How far the stick must fall back before that direction is released. Lower than
    /// the engage threshold, so a stick resting near the boundary cannot flicker.
    /// </summary>
    private const float KeyboardStickReleaseThreshold = 0.35f;

    /// <summary>
    /// Suspend controller input without stopping the engine.
    /// </summary>
    public void Pause()
    {
        if (IsPaused)
        {
            return;
        }

        IsPaused = true;

        // Let go of anything currently held, or a button down at the moment of pausing
        // would stay down for as long as the pause lasts.
        _mouseSimulator.ReleaseAll();
        _keyboardSimulator.ReleaseAll();
        _isTaskSwitcherOpen = false;

        PausedChanged?.Invoke(this, true);
    }

    /// <summary>
    /// Resume controller input after a pause.
    /// </summary>
    public void Resume()
    {
        if (!IsPaused)
        {
            return;
        }

        IsPaused = false;

        // Start from a clean slate so buttons held during the pause do not register as
        // fresh presses the instant input comes back.
        _previousState = null;
        _inputFilter.Reset();

        PausedChanged?.Invoke(this, false);
    }

    public HIDraEngine(InputSettings? settings = null, Dictionary<string, ButtonMapping>? buttonMappings = null)
    {
        _settings = settings ?? new InputSettings();
        _buttonMappings = buttonMappings ?? new Dictionary<string, ButtonMapping>();
        _controllerService = new XboxControllerService();
        _inputProcessor = new InputProcessor(_settings);
        _inputFilter = new InputFilter(_settings);
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
            _inputFilter.Reset();
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
            // Smoothing and ignored repeat presses happen first, so every use of the
            // controller - pointer, scrolling, keyboard, buttons - gets them alike
            var filtered = _inputFilter.Apply(state);
            ProcessControllerState(filtered);
            _previousState = filtered.Clone();
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
        if (IsPaused)
        {
            // Nothing is emitted while paused, but the chord is still watched: it is
            // the only way back for someone whose sole input device this is.
            UpdateRecoveryChord(state);
            return;
        }

        double now = _frameTimer.Elapsed.TotalSeconds;
        double elapsed = now - _lastFrameSeconds;
        _lastFrameSeconds = now;

        // The first frame after starting or resuming has no meaningful predecessor.
        if (elapsed <= 0 || elapsed > MaxFrameSeconds)
        {
            elapsed = Math.Min(MaxFrameSeconds, _settings.PollRateMs / 1000.0);
        }

        float deltaSeconds = (float)elapsed;

        // The Left Trigger used to be precision mode, but it cannot be held while
        // steering the left stick, which is exactly when precision is wanted. It moves
        // the keyboard instead, and the default speed is slow enough not to need it.
        const bool precisionMode = false;

        // While the keyboard is open the left stick is steering the highlight, so it
        // must not also drag the cursor or scroll the page underneath. The right stick
        // keeps whatever job it currently has, so the user is not left with nothing.
        // Kept separate from `state` on purpose: navigation below still needs the real
        // stick position, and zeroing it here would leave the highlight unable to move.
        var pointerState = state;

        if (KeyboardNavigationActive)
        {
            pointerState = state.Clone();
            pointerState.LeftStickX = 0f;
            pointerState.LeftStickY = 0f;
        }

        float cursorDeltaX = 0f, cursorDeltaY = 0f;

        // Process mouse and scroll - swap sticks based on mode
        if (_useRightStickForCursor)
        {
            // Right stick for cursor, left stick for scroll
            var (mouseX, mouseY) = _inputProcessor.ProcessMouseMovementFromRightStick(pointerState, precisionMode, deltaSeconds);
            _mouseSimulator.MoveMouse(mouseX, mouseY);
            (cursorDeltaX, cursorDeltaY) = (mouseX, mouseY);
            
            var (scrollX, scrollY) = _inputProcessor.ProcessScrollFromLeftStick(pointerState);
            AccumulateAndApplyScroll(scrollX, scrollY);
        }
        else
        {
            // Default: Left stick for cursor, right stick for scroll
            var (mouseX, mouseY) = _inputProcessor.ProcessMouseMovement(pointerState, precisionMode, deltaSeconds);
            _mouseSimulator.MoveMouse(mouseX, mouseY);
            (cursorDeltaX, cursorDeltaY) = (mouseX, mouseY);
            
            var (scrollX, scrollY) = _inputProcessor.ProcessScroll(pointerState);
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

        UpdateDwellClick(state, cursorDeltaX, cursorDeltaY, holdMode);
        ReportActivity(state);

        // Recovery chord is checked before button dispatch so it can suppress the
        // second button's own action while the chord is being formed.
        UpdateRecoveryChord(state);

        // While the on-screen keyboard is open the D-pad drives it. Window snapping is
        // unavailable for that time, which is a fair trade: typing is far more frequent
        // than window management, and this is what removes aiming from every keystroke.
        if (KeyboardNavigationActive)
        {
            UpdateKeyboardNavigation(state);
        }
        else
        {
            _heldDirection = null;
            _keyRepeatTimer.Reset();
        }

        // Process buttons
        if (_previousState != null)
        {
            if (_inputProcessor.IsTriggerPressed(state.LeftTrigger) &&
                !_inputProcessor.IsTriggerPressed(_previousState.LeftTrigger))
            {
                KeyboardPositionToggleRequested?.Invoke(this, EventArgs.Empty);
            }

            ProcessButtons(state, _previousState);
        }
    }

    /// <summary>
    /// Click once when the cursor has moved and then stayed still for the dwell time.
    ///
    /// Only movement arms it, so leaving the controller alone never clicks, and a click
    /// disarms it until the cursor moves again. Any button or trigger also disarms it:
    /// someone who clicks with A should not get a second click from the dwell a moment
    /// later. It stays out of the way while the keyboard is open, where the stick moves
    /// the highlight rather than the cursor.
    /// </summary>
    private void UpdateDwellClick(ControllerState state, float deltaX, float deltaY, bool holdMode)
    {
        if (!_settings.EnableDwellClick || KeyboardNavigationActive)
        {
            DisarmDwell();
            return;
        }

        bool anyButton = state.ButtonA || state.ButtonB || state.ButtonX || state.ButtonY
            || state.LeftBumper || state.RightBumper || state.Back || state.Start
            || state.LeftStickClick || state.RightStickClick
            || state.DpadUp || state.DpadDown || state.DpadLeft || state.DpadRight
            || holdMode || _inputProcessor.IsTriggerPressed(state.LeftTrigger);

        if (anyButton)
        {
            DisarmDwell();
            return;
        }

        _dwellDriftX += deltaX;
        _dwellDriftY += deltaY;

        if (Math.Sqrt(_dwellDriftX * _dwellDriftX + _dwellDriftY * _dwellDriftY) > DwellMoveTolerancePixels)
        {
            // A real move: cancel any countdown, and start timing again from here
            EndCountdown();
            _dwellArmed = true;
            _dwellDriftX = _dwellDriftY = 0;
            _dwellStillTimer.Restart();
            return;
        }

        if (!_dwellArmed)
        {
            return;
        }

        double elapsed = _dwellStillTimer.Elapsed.TotalSeconds;

        if (!_dwellCountingDown && elapsed >= Math.Min(DwellRingDelaySeconds, _settings.DwellClickSeconds))
        {
            _dwellCountingDown = true;
            DwellCountdownStarted?.Invoke(this, Math.Max(0, _settings.DwellClickSeconds - elapsed));
        }

        if (elapsed >= _settings.DwellClickSeconds)
        {
            DisarmDwell();
            _mouseSimulator.LeftClick();
        }
    }

    private void ReportActivity(ControllerState state)
    {
        const float stickThreshold = 0.25f;

        bool active = state.ButtonA || state.ButtonB || state.ButtonX || state.ButtonY
            || state.LeftBumper || state.RightBumper || state.Back || state.Start
            || state.LeftStickClick || state.RightStickClick
            || state.DpadUp || state.DpadDown || state.DpadLeft || state.DpadRight
            || _inputProcessor.IsTriggerPressed(state.LeftTrigger)
            || _inputProcessor.IsTriggerPressed(state.RightTrigger)
            || Math.Abs(state.LeftStickX) > stickThreshold || Math.Abs(state.LeftStickY) > stickThreshold
            || Math.Abs(state.RightStickX) > stickThreshold || Math.Abs(state.RightStickY) > stickThreshold;

        if (active && _activityThrottle.ElapsedMilliseconds >= ActivityThrottleMs)
        {
            _activityThrottle.Restart();
            InputActivity?.Invoke(this, EventArgs.Empty);
        }

        var controls = ControllerControls.None;
        if (state.ButtonA) controls |= ControllerControls.A;
        if (state.ButtonB) controls |= ControllerControls.B;
        if (state.ButtonX) controls |= ControllerControls.X;
        if (state.ButtonY) controls |= ControllerControls.Y;
        if (state.LeftBumper) controls |= ControllerControls.LeftBumper;
        if (state.RightBumper) controls |= ControllerControls.RightBumper;
        if (_inputProcessor.IsTriggerPressed(state.LeftTrigger)) controls |= ControllerControls.LeftTrigger;
        if (_inputProcessor.IsTriggerPressed(state.RightTrigger)) controls |= ControllerControls.RightTrigger;
        if (state.Back) controls |= ControllerControls.Back;
        if (state.Start) controls |= ControllerControls.Start;
        if (Math.Abs(state.LeftStickX) > stickThreshold || Math.Abs(state.LeftStickY) > stickThreshold)
            controls |= ControllerControls.LeftStick;
        if (Math.Abs(state.RightStickX) > stickThreshold || Math.Abs(state.RightStickY) > stickThreshold)
            controls |= ControllerControls.RightStick;
        if (state.LeftStickClick) controls |= ControllerControls.LeftStickPress;
        if (state.RightStickClick) controls |= ControllerControls.RightStickPress;
        if (state.DpadUp || state.DpadDown || state.DpadLeft || state.DpadRight)
            controls |= ControllerControls.DPad;

        if (controls != _activeControls)
        {
            _activeControls = controls;
            ActiveControlsChanged?.Invoke(this, controls);
        }
    }

    /// <summary>
    /// Nothing more until the cursor moves again
    /// </summary>
    private void DisarmDwell()
    {
        _dwellArmed = false;
        _dwellDriftX = _dwellDriftY = 0;
        EndCountdown();
    }

    private void EndCountdown()
    {
        if (_dwellCountingDown)
        {
            _dwellCountingDown = false;
            DwellCountdownEnded?.Invoke(this, EventArgs.Empty);
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

            // The chord doubles as the way out of a pause, so a paused HIDra is never
            // a dead end for the person holding the controller.
            if (IsPaused)
            {
                Resume();
            }

            ShowWindowRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Move the keyboard highlight from the D-pad, repeating while a direction is held.
    ///
    /// Without repeat, crossing the keyboard would mean a separate press for every key
    /// in between, which would trade one kind of effort for another.
    /// </summary>
    /// <summary>
    /// Turn a continuous stick position into a discrete step, or null when the stick is
    /// not pushed far enough to count.
    ///
    /// Two thresholds rather than one: the stick must be pushed past the larger value to
    /// start moving, and must fall back below the smaller one before that direction is
    /// released. Without that gap, resting a little off centre - or a worn stick that no
    /// longer returns cleanly - would sit right on the boundary and flicker between
    /// stepping and not stepping.
    ///
    /// Only the larger axis is considered, so a diagonal push moves one way rather than
    /// alternating unpredictably between two.
    /// </summary>
    private KeyboardNavigationDirection? DirectionFromStick(float x, float y)
    {
        // Held directions release later than they engage, so keep using the axis we are
        // already travelling along until it genuinely falls away.
        float threshold = _heldDirection == null
            ? KeyboardStickEngageThreshold
            : KeyboardStickReleaseThreshold;

        float absX = Math.Abs(x);
        float absY = Math.Abs(y);

        if (absX < threshold && absY < threshold)
        {
            return null;
        }

        if (absX >= absY)
        {
            return x > 0 ? KeyboardNavigationDirection.Right : KeyboardNavigationDirection.Left;
        }

        // Pushing the stick up gives a positive Y, and up the keyboard is what is meant.
        return y > 0 ? KeyboardNavigationDirection.Up : KeyboardNavigationDirection.Down;
    }

    private void UpdateKeyboardNavigation(ControllerState state)
    {
        // The left stick is the primary control: the D-pad asks for more precise finger
        // placement than it is reasonable to require. The D-pad still works, because
        // supporting both costs nothing and leaves the choice open.
        KeyboardNavigationDirection? direction =
            DirectionFromStick(state.LeftStickX, state.LeftStickY)
            ?? (state.DpadLeft ? KeyboardNavigationDirection.Left
              : state.DpadRight ? KeyboardNavigationDirection.Right
              : state.DpadUp ? KeyboardNavigationDirection.Up
              : state.DpadDown ? KeyboardNavigationDirection.Down
              : null);

        if (direction == null)
        {
            _heldDirection = null;
            _keyRepeatTimer.Reset();
            return;
        }

        if (direction != _heldDirection)
        {
            // A new direction always steps once immediately, so a single press feels
            // instant rather than waiting on the repeat delay.
            _heldDirection = direction;
            _keyRepeatCount = 0;
            _keyRepeatTimer.Restart();
            KeyboardNavigateRequested?.Invoke(this, direction.Value);
            return;
        }

        int due = _keyRepeatCount == 0
            ? KeyRepeatDelayMs
            : KeyRepeatIntervalMs;

        if (_keyRepeatTimer.ElapsedMilliseconds >= due)
        {
            _keyRepeatCount++;
            _keyRepeatTimer.Restart();
            KeyboardNavigateRequested?.Invoke(this, direction.Value);
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
        // A presses the highlighted key while the keyboard is open. A left click is still
        // available on the right trigger, so nothing becomes unreachable.
        if (!KeyboardNavigationActive)
        {
            ProcessButton("ButtonA", current.ButtonA, previous.ButtonA, activeModifier);
        }
        else if (_inputProcessor.IsButtonPressed(current.ButtonA, previous.ButtonA))
        {
            KeyboardSelectRequested?.Invoke(this, EventArgs.Empty);
        }
        // B gives the shifted character of the highlighted key while the keyboard is
        // open - the symbol printed above it, or the capital. Right-click is unavailable
        // for that time, which is rarely wanted mid-typing and is the same trade already
        // made for A.
        if (!KeyboardNavigationActive)
        {
            ProcessButton("ButtonB", current.ButtonB, previous.ButtonB, activeModifier);
        }
        else if (_inputProcessor.IsButtonPressed(current.ButtonB, previous.ButtonB))
        {
            KeyboardSelectShiftedRequested?.Invoke(this, EventArgs.Empty);
        }
        ProcessButton("ButtonX", current.ButtonX, previous.ButtonX, activeModifier);
        // While the keyboard is open, LB, RB and Y type Backspace, Space and Enter. After
        // letters these are the keys pressed most, and each sits at the edge of the board,
        // so every one was a long trip with the highlight and back. Their usual jobs -
        // window switcher, double click, swapping the sticks - are rarely wanted in the
        // middle of a sentence, and all come back the moment the keyboard is closed.
        // Left and right match the direction the text moves: LB takes away, RB adds.
        if (!KeyboardNavigationActive)
        {
            ProcessButton("ButtonY", current.ButtonY, previous.ButtonY, activeModifier);
            ProcessButton("LeftBumper", current.LeftBumper, previous.LeftBumper, activeModifier);
            ProcessButton("RightBumper", current.RightBumper, previous.RightBumper, activeModifier);
        }
        else
        {
            if (_inputProcessor.IsButtonPressed(current.LeftBumper, previous.LeftBumper))
                KeyboardQuickKeyRequested?.Invoke(this, KeyboardQuickKey.Backspace);
            if (_inputProcessor.IsButtonPressed(current.RightBumper, previous.RightBumper))
                KeyboardQuickKeyRequested?.Invoke(this, KeyboardQuickKey.Space);
            if (_inputProcessor.IsButtonPressed(current.ButtonY, previous.ButtonY))
                KeyboardQuickKeyRequested?.Invoke(this, KeyboardQuickKey.Enter);
        }
        // While the recovery chord is being formed, only the button pressed first runs
        // its normal action. Suppressing the second one stops the chord from also
        // firing Task View or the Start menu on top of restoring the window.
        if (!(current.Back && current.Start))
        {
            ProcessButton("Back", current.Back, previous.Back, activeModifier);
            ProcessButton("Start", current.Start, previous.Start, activeModifier);
        }

        if (!KeyboardNavigationActive)
        {
            ProcessButton("DpadUp", current.DpadUp, previous.DpadUp, activeModifier);
            ProcessButton("DpadDown", current.DpadDown, previous.DpadDown, activeModifier);
            ProcessButton("DpadLeft", current.DpadLeft, previous.DpadLeft, activeModifier);
            ProcessButton("DpadRight", current.DpadRight, previous.DpadRight, activeModifier);
        }
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
