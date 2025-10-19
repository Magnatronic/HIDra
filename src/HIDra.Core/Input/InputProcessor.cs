using HIDra.Models;
using System;

namespace HIDra.Core.Input;

/// <summary>
/// Processes raw controller input and applies deadzone, sensitivity, and smoothing
/// </summary>
public class InputProcessor
{
    private readonly InputSettings _settings;

    public InputProcessor(InputSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Apply deadzone to an analog axis value
    /// </summary>
    public float ApplyDeadzone(float value, float deadzone)
    {
        float absValue = Math.Abs(value);
        
        if (absValue < deadzone)
        {
            return 0f;
        }

        // Rescale the value so the output goes from 0 to 1 smoothly
        float rescaled = (absValue - deadzone) / (1f - deadzone);
        float output = Math.Min(rescaled, 1f);
        
        return Math.Sign(value) * output;
    }

    /// <summary>
    /// Apply sensitivity curve to a value
    /// </summary>
    public float ApplySensitivity(float value, float sensitivity)
    {
        // Hybrid curve: MASSIVE linear zone for users with limited motor control
        // 95% of stick range is slow and predictable, only the very edge accelerates
        float absValue = Math.Abs(value);
        float curved;
        
        // First 95% of stick range uses linear movement at 0.5x speed
        if (absValue <= 0.95f)
        {
            curved = absValue * 0.5f; // Slow linear zone for maximum control
        }
        else
        {
            // Only the last 5% accelerates to reach full speed
            float normalized = (absValue - 0.95f) / 0.05f; // Normalize to 0-1 range
            curved = 0.475f + (float)Math.Pow(normalized, 2.0) * 0.525f; // Gentler curve
        }
        
        return Math.Sign(value) * curved * sensitivity;
    }
    
    /// <summary>
    /// Apply purely linear sensitivity for scrolling (no acceleration)
    /// </summary>
    private float ApplyLinearSensitivity(float value, float sensitivity)
    {
        // Pure linear response - no curves, no acceleration
        // Just multiply by sensitivity for consistent, predictable scrolling
        return value * sensitivity;
    }

    /// <summary>
    /// Process left stick for mouse movement
    /// </summary>
    public (float X, float Y) ProcessMouseMovement(ControllerState state, bool precisionMode)
    {
        // Apply controller calibration first (for worn sticks that can't reach full range)
        float calibratedX = state.LeftStickX / _settings.StickCalibrationMax;
        float calibratedY = state.LeftStickY / _settings.StickCalibrationMax;
        calibratedX = Math.Clamp(calibratedX, -1f, 1f);
        calibratedY = Math.Clamp(calibratedY, -1f, 1f);
        
        // Apply deadzone
        float x = ApplyDeadzone(calibratedX, _settings.Deadzone);
        float y = ApplyDeadzone(calibratedY, _settings.Deadzone);

        // Apply sensitivity
        float sensitivity = precisionMode 
            ? _settings.CursorSensitivity * _settings.PrecisionModeSensitivity 
            : _settings.CursorSensitivity;

        x = ApplySensitivity(x, sensitivity);
        y = ApplySensitivity(y, sensitivity);

        // Scale to pixel movement - maxSpeed is now configurable via CursorSensitivity
        // Base speed of 20 pixels per frame, multiplied by the configured sensitivity
        const float baseMaxSpeed = 20f;
        x *= baseMaxSpeed;
        y *= -baseMaxSpeed; // Invert Y for natural mouse movement

        return (x, y);
    }

    /// <summary>
    /// Process right stick for mouse cursor movement (alternate mode for one-handed use)
    /// </summary>
    public (float X, float Y) ProcessMouseMovementFromRightStick(ControllerState state, bool precisionMode)
    {
        // Apply controller calibration first (for worn sticks that can't reach full range)
        float calibratedX = state.RightStickX / _settings.StickCalibrationMax;
        float calibratedY = state.RightStickY / _settings.StickCalibrationMax;
        calibratedX = Math.Clamp(calibratedX, -1f, 1f);
        calibratedY = Math.Clamp(calibratedY, -1f, 1f);

        // Apply deadzone
        float x = ApplyDeadzone(calibratedX, _settings.Deadzone);
        float y = ApplyDeadzone(calibratedY, _settings.Deadzone);

        // Apply sensitivity (uses precision mode modifier when RB held)
        float sensitivity = precisionMode ? _settings.PrecisionModeSensitivity : _settings.CursorSensitivity;
        x = ApplySensitivity(x, sensitivity);
        y = ApplySensitivity(y, sensitivity);

        // Scale to pixels per frame (20 pixels base speed)
        const float baseMaxSpeed = 20f;
        x *= baseMaxSpeed;
        y *= -baseMaxSpeed; // Invert Y for natural mouse movement (up = negative screen Y)

        return (x, y);
    }

    /// <summary>
    /// Process right stick for scrolling
    /// </summary>
    public (float Horizontal, float Vertical) ProcessScroll(ControllerState state)
    {
        // Apply controller calibration first (for worn sticks that can't reach full range)
        float calibratedX = state.RightStickX / _settings.StickCalibrationMax;
        float calibratedY = state.RightStickY / _settings.StickCalibrationMax;
        calibratedX = Math.Clamp(calibratedX, -1f, 1f);
        calibratedY = Math.Clamp(calibratedY, -1f, 1f);
        
        // Apply deadzone
        float x = ApplyDeadzone(calibratedX, _settings.Deadzone);
        float y = ApplyDeadzone(calibratedY, _settings.Deadzone);

        // Apply LINEAR sensitivity (no curves - pure 1:1 response for scrolling)
        x = ApplyLinearSensitivity(x, _settings.ScrollSensitivity);
        y = ApplyLinearSensitivity(y, _settings.ScrollSensitivity);

        // Scale to reasonable scroll amount (EXTREMELY low - Windows scroll units are very aggressive)
        // Note: MouseSimulator rounds to int, so 0.5 = 1 scroll notch, 1.0 = 1 notch, 1.5 = 2 notches
        const float scrollScale = 0.15f; // Very low because each integer unit scrolls multiple lines
        x *= scrollScale;
        y *= scrollScale; // Natural scrolling: push up = scroll up, push down = scroll down

        return (x, y);
    }

    /// <summary>
    /// Process left stick for scrolling (alternate mode for one-handed use)
    /// </summary>
    public (float Horizontal, float Vertical) ProcessScrollFromLeftStick(ControllerState state)
    {
        // Apply controller calibration first (for worn sticks that can't reach full range)
        float calibratedX = state.LeftStickX / _settings.StickCalibrationMax;
        float calibratedY = state.LeftStickY / _settings.StickCalibrationMax;
        calibratedX = Math.Clamp(calibratedX, -1f, 1f);
        calibratedY = Math.Clamp(calibratedY, -1f, 1f);
        
        // Apply deadzone
        float x = ApplyDeadzone(calibratedX, _settings.Deadzone);
        float y = ApplyDeadzone(calibratedY, _settings.Deadzone);

        // Apply LINEAR sensitivity (no curves - pure 1:1 response for scrolling)
        x = ApplyLinearSensitivity(x, _settings.ScrollSensitivity);
        y = ApplyLinearSensitivity(y, _settings.ScrollSensitivity);

        // Scale to reasonable scroll amount (EXTREMELY low - Windows scroll units are very aggressive)
        // Note: MouseSimulator rounds to int, so 0.5 = 1 scroll notch, 1.0 = 1 notch, 1.5 = 2 notches
        const float scrollScale = 0.15f; // Very low because each integer unit scrolls multiple lines
        x *= scrollScale;
        y *= scrollScale; // Natural scrolling: push up = scroll up, push down = scroll down

        return (x, y);
    }

    /// <summary>
    /// Check if a trigger is pressed beyond threshold
    /// </summary>
    public bool IsTriggerPressed(float triggerValue)
    {
        return triggerValue >= _settings.TriggerThreshold;
    }

    /// <summary>
    /// Detect button press (transition from not pressed to pressed)
    /// </summary>
    public bool IsButtonPressed(bool currentState, bool previousState)
    {
        return currentState && !previousState;
    }

    /// <summary>
    /// Detect button release (transition from pressed to not pressed)
    /// </summary>
    public bool IsButtonReleased(bool currentState, bool previousState)
    {
        return !currentState && previousState;
    }

    /// <summary>
    /// Clamp a value between min and max
    /// </summary>
    public float Clamp(float value, float min, float max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
