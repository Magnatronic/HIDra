namespace HIDra.Models;

/// <summary>
/// Windows virtual-key codes.
///
/// Replaces WindowsInput.Native.VirtualKeyCode from InputSimulatorPlus, which targets
/// .NET Framework only and has not been maintained. Values are the VK_* constants from
/// the Win32 headers and must not be renumbered.
///
/// Only the keys HIDra can actually produce are listed. Letters and digits are omitted
/// deliberately: typed characters go through Unicode text entry rather than virtual-key
/// codes, so they work regardless of the user's keyboard layout.
/// </summary>
public enum VirtualKey : ushort
{
    None = 0x00,

    Back = 0x08,
    Tab = 0x09,
    Return = 0x0D,

    Shift = 0x10,
    Control = 0x11,

    /// <summary>The Alt key. Named VK_MENU in Win32.</summary>
    Menu = 0x12,

    Pause = 0x13,
    CapsLock = 0x14,
    Escape = 0x1B,
    Space = 0x20,

    /// <summary>Page Up. Named VK_PRIOR in Win32.</summary>
    PageUp = 0x21,

    /// <summary>Page Down. Named VK_NEXT in Win32.</summary>
    PageDown = 0x22,

    End = 0x23,
    Home = 0x24,
    Left = 0x25,
    Up = 0x26,
    Right = 0x27,
    Down = 0x28,

    Insert = 0x2D,
    Delete = 0x2E,

    LeftWindows = 0x5B,
    RightWindows = 0x5C,
    Applications = 0x5D,

    F1 = 0x70,
    F2 = 0x71,
    F3 = 0x72,
    F4 = 0x73,
    F5 = 0x74,
    F6 = 0x75,
    F7 = 0x76,
    F8 = 0x77,
    F9 = 0x78,
    F10 = 0x79,
    F11 = 0x7A,
    F12 = 0x7B,

    NumLock = 0x90,
    ScrollLock = 0x91,

    LeftShift = 0xA0,
    RightShift = 0xA1,
    LeftControl = 0xA2,
    RightControl = 0xA3,
    LeftAlt = 0xA4,
    RightAlt = 0xA5,

    // Letter keys, needed for shortcuts such as Ctrl+C where the virtual key rather
    // than the character is what matters.
    A = 0x41,
    C = 0x43,
    V = 0x56,
    X = 0x58,
    Y = 0x59,
    Z = 0x5A
}
