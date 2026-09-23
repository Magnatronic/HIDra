namespace HIDra.Models;

/// <summary>
/// A step from one key to the next on the on-screen keyboard.
///
/// Typing previously meant steering the mouse cursor onto each key in turn, so every
/// character demanded a separate act of precise aiming - the very movement that is
/// hardest for someone with limited motor control, repeated once per letter. Moving a
/// highlight between keys replaces aiming with discrete steps that cannot be overshot.
/// </summary>
public enum KeyboardNavigationDirection
{
    Up,
    Down,
    Left,
    Right
}

/// <summary>
/// The keys typed most often after letters, given their own controller buttons while the
/// on-screen keyboard is open (LB, RB and Y), so none of them costs a trip across it.
/// </summary>
public enum KeyboardQuickKey
{
    Backspace,
    Space,
    Enter
}
