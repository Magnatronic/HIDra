namespace HIDra.Models;

/// <summary>
/// Settings for input processing
/// </summary>
public class InputSettings
{
    /// <summary>
    /// Global cursor sensitivity multiplier (0.1 to 5.0)
    /// </summary>
    public float CursorSensitivity { get; set; } = 1.0f;

    /// <summary>
    /// Global scroll sensitivity multiplier (0.1 to 5.0)
    /// </summary>
    public float ScrollSensitivity { get; set; } = 0.5f; // Much lower - Windows scroll units are aggressive

    /// <summary>
    /// Precision mode sensitivity multiplier (0.1 to 1.0)
    /// </summary>
    public float PrecisionModeSensitivity { get; set; } = 0.3f;

    /// <summary>
    /// Deadzone for analog sticks (0.0 to 0.5)
    /// </summary>
    public float Deadzone { get; set; } = 0.05f;
    
    /// <summary>
    /// Controller stick calibration maximum (for worn controllers that can't reach full range)
    /// Set to the maximum value your stick can physically reach (e.g., 0.90 if it maxes out at 90%)
    /// </summary>
    public float StickCalibrationMax { get; set; } = 0.90f;

    /// <summary>
    /// Trigger activation threshold (0.0 to 1.0)
    /// </summary>
    public float TriggerThreshold { get; set; } = 0.3f;

    /// <summary>
    /// Controller polling rate in milliseconds
    /// </summary>
    public int PollRateMs { get; set; } = 10;

    /// <summary>
    /// Double-click time window in milliseconds
    /// </summary>
    public int DoubleClickTimeMs { get; set; } = 300;

    /// <summary>
    /// Enable haptic feedback (if supported)
    /// </summary>
    public bool EnableHaptics { get; set; } = false;

    /// <summary>
    /// Enable debug logging
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;

    /// <summary>
    /// Automatically suspend HIDra when Grid 3 is detected (enabled by default)
    /// </summary>
    public bool EnableGrid3AutoSuspend { get; set; } = true;
}
