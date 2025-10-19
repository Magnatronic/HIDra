namespace HIDra.Models;

/// <summary>
/// Represents the current state of a game controller
/// </summary>
public class ControllerState
{
    /// <summary>
    /// Left analog stick X-axis (-1.0 to 1.0)
    /// </summary>
    public float LeftStickX { get; set; }

    /// <summary>
    /// Left analog stick Y-axis (-1.0 to 1.0)
    /// </summary>
    public float LeftStickY { get; set; }

    /// <summary>
    /// Right analog stick X-axis (-1.0 to 1.0)
    /// </summary>
    public float RightStickX { get; set; }

    /// <summary>
    /// Right analog stick Y-axis (-1.0 to 1.0)
    /// </summary>
    public float RightStickY { get; set; }

    /// <summary>
    /// Left trigger (0.0 to 1.0)
    /// </summary>
    public float LeftTrigger { get; set; }

    /// <summary>
    /// Right trigger (0.0 to 1.0)
    /// </summary>
    public float RightTrigger { get; set; }

    /// <summary>
    /// A button state
    /// </summary>
    public bool ButtonA { get; set; }

    /// <summary>
    /// B button state
    /// </summary>
    public bool ButtonB { get; set; }

    /// <summary>
    /// X button state
    /// </summary>
    public bool ButtonX { get; set; }

    /// <summary>
    /// Y button state
    /// </summary>
    public bool ButtonY { get; set; }

    /// <summary>
    /// Left bumper (LB) state
    /// </summary>
    public bool LeftBumper { get; set; }

    /// <summary>
    /// Right bumper (RB) state
    /// </summary>
    public bool RightBumper { get; set; }

    /// <summary>
    /// Back/View button state
    /// </summary>
    public bool Back { get; set; }

    /// <summary>
    /// Start/Menu button state
    /// </summary>
    public bool Start { get; set; }

    /// <summary>
    /// Left stick click (L3) state
    /// </summary>
    public bool LeftStickClick { get; set; }

    /// <summary>
    /// Right stick click (R3) state
    /// </summary>
    public bool RightStickClick { get; set; }

    /// <summary>
    /// D-Pad Up state
    /// </summary>
    public bool DpadUp { get; set; }

    /// <summary>
    /// D-Pad Down state
    /// </summary>
    public bool DpadDown { get; set; }

    /// <summary>
    /// D-Pad Left state
    /// </summary>
    public bool DpadLeft { get; set; }

    /// <summary>
    /// D-Pad Right state
    /// </summary>
    public bool DpadRight { get; set; }

    /// <summary>
    /// Xbox/Guide button state (if supported)
    /// </summary>
    public bool Guide { get; set; }

    /// <summary>
    /// Creates a copy of the current state
    /// </summary>
    public ControllerState Clone()
    {
        return new ControllerState
        {
            LeftStickX = LeftStickX,
            LeftStickY = LeftStickY,
            RightStickX = RightStickX,
            RightStickY = RightStickY,
            LeftTrigger = LeftTrigger,
            RightTrigger = RightTrigger,
            ButtonA = ButtonA,
            ButtonB = ButtonB,
            ButtonX = ButtonX,
            ButtonY = ButtonY,
            LeftBumper = LeftBumper,
            RightBumper = RightBumper,
            Back = Back,
            Start = Start,
            LeftStickClick = LeftStickClick,
            RightStickClick = RightStickClick,
            DpadUp = DpadUp,
            DpadDown = DpadDown,
            DpadLeft = DpadLeft,
            DpadRight = DpadRight,
            Guide = Guide
        };
    }
}
