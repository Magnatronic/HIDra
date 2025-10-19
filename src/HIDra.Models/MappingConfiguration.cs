using System.Collections.Generic;

namespace HIDra.Models;

/// <summary>
/// Root configuration object for controller mappings
/// </summary>
public class MappingConfiguration
{
    public ProfileInfo Profile { get; set; } = new();
    public MappingSettings Settings { get; set; } = new();
    public Dictionary<string, AxisMapping> Axes { get; set; } = new();
    public Dictionary<string, ButtonMapping> Buttons { get; set; } = new();
    public Dictionary<string, ModifierMode> ModifierModes { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}

/// <summary>
/// Profile metadata
/// </summary>
public class ProfileInfo
{
    public string Name { get; set; } = "Default Profile";
    public string Version { get; set; } = "1.0";
    public string ControllerType { get; set; } = "Xbox";
    public string Description { get; set; } = "";
}

/// <summary>
/// Global mapping settings
/// </summary>
public class MappingSettings
{
    public float CursorSensitivity { get; set; } = 1.0f;
    public float ScrollSensitivity { get; set; } = 1.0f;
    public float PrecisionModeSensitivity { get; set; } = 0.3f;
    public float Deadzone { get; set; } = 0.15f;
    public int PollRateMs { get; set; } = 10;
    public float StickCalibrationMax { get; set; } = 0.90f;
    public float TriggerThreshold { get; set; } = 0.3f;
}

/// <summary>
/// Axis (stick/trigger) mapping
/// </summary>
public class AxisMapping
{
    public string Action { get; set; } = "";
    public float Sensitivity { get; set; } = 1.0f;
    public float Deadzone { get; set; } = 0.15f;
    public float Threshold { get; set; } = 0.3f;
    public bool InvertAxis { get; set; } = false;
    public string Description { get; set; } = "";
}

/// <summary>
/// Button mapping (can have default and modifier-specific mappings)
/// </summary>
public class ButtonMapping
{
    public ActionMapping? Default { get; set; }
    public Dictionary<string, ActionMapping> Modifiers { get; set; } = new();
}

/// <summary>
/// Specific action for a button press
/// </summary>
public class ActionMapping
{
    public string Action { get; set; } = "";
    public List<string> Keys { get; set; } = new();
    public string Mode { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>
/// Modifier mode configuration (Ctrl, Shift, etc.)
/// </summary>
public class ModifierMode
{
    public string Trigger { get; set; } = "";
    public List<string> ModifierKeys { get; set; } = new();
    public Dictionary<string, ActionMapping> Buttons { get; set; } = new();
}
