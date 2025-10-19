using HIDra.Core.Actions;
using HIDra.Core.Controllers;
using HIDra.Core.Input;
using HIDra.Core.Simulation;
using HIDra.Models;
using System;
using System.Collections.Generic;
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
        _buttonActionHandler.TaskSwitcherRequested += OnTaskSwitcherRequested;
        _buttonActionHandler.StickModeSwapRequested += OnStickModeSwapRequested;
        _buttonActionHandler.ToggleOnScreenKeyboardRequested += OnToggleOnScreenKeyboardRequested;
    }

    /// <summary>
    /// Initialize and detect controller
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        try
        {
            var controller = await _controllerService.DetectControllerAsync();
            
            if (controller == null)
            {
                ErrorOccurred?.Invoke(this, "No Xbox controller detected. Please connect a controller and try again.");
                return false;
            }

            var connected = await _controllerService.ConnectAsync(controller);
            
            if (!connected)
            {
                ErrorOccurred?.Invoke(this, "Failed to connect to controller.");
                return false;
            }

            return true;
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
        
        // Release all inputs
        _mouseSimulator.ReleaseAll();
        _keyboardSimulator.ReleaseAll();
    }
    
    /// <summary>
    /// Send a key press (for virtual keyboard)
    /// </summary>
    public void SendKeyPress(WindowsInput.Native.VirtualKeyCode key)
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
    /// Handle controller connection changes
    /// </summary>
    private void OnConnectionChanged(object? sender, ControllerInfo info)
    {
        ConnectionChanged?.Invoke(this, info);

        if (!info.IsConnected)
        {
            Stop();
        }
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

        // Process buttons
        if (_previousState != null)
        {
            ProcessButtons(state, _previousState);
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
                _keyboardSimulator.KeyPress(WindowsInput.Native.VirtualKeyCode.ESCAPE); // Close switcher
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
        ProcessButton("Back", current.Back, previous.Back, activeModifier);
        ProcessButton("Start", current.Start, previous.Start, activeModifier);
        ProcessButton("DpadUp", current.DpadUp, previous.DpadUp, activeModifier);
        ProcessButton("DpadDown", current.DpadDown, previous.DpadDown, activeModifier);
        ProcessButton("DpadLeft", current.DpadLeft, previous.DpadLeft, activeModifier);
        ProcessButton("DpadRight", current.DpadRight, previous.DpadRight, activeModifier);
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

    public void Dispose()
    {
        Stop();
        _controllerService?.Dispose();
        _mouseSimulator?.Dispose();
        _keyboardSimulator?.Dispose();
    }
}
