using System;
using System.Runtime.InteropServices;
using HIDra.Models;

namespace HIDra.Core.Simulation;

/// <summary>
/// Synthetic input via the Win32 SendInput function.
///
/// Replaces InputSimulatorPlus, which targets .NET Framework only and is unmaintained -
/// the same problem as the SharpDX dependency it sat alongside. Every keystroke and
/// mouse movement HIDra produces goes through here, so it is the last place that
/// should depend on an abandoned package.
///
/// Note that SendInput cannot drive windows running at a higher integrity level, so
/// UAC prompts and elevated applications will not respond to it. That is a deliberate
/// limitation rather than a defect: lifting it needs a signed binary carrying a
/// uiAccess manifest, installed to a protected location.
/// </summary>
internal static class NativeInput
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetMessageExtraInfo();

    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;

    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventMiddleDown = 0x0020;
    private const uint MouseEventMiddleUp = 0x0040;
    private const uint MouseEventWheel = 0x0800;
    private const uint MouseEventHWheel = 0x1000;
    private const uint MouseEventAbsolute = 0x8000;
    private const uint MouseEventVirtualDesk = 0x4000;

    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;

    /// <summary>One notch of the mouse wheel, as defined by WHEEL_DELTA.</summary>
    private const int WheelDelta = 120;

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    /// <summary>
    /// Keys that must carry the extended-key flag. Arrow and navigation keys share
    /// virtual-key codes with the numeric keypad, and without this flag some
    /// applications - and Windows' own Win+Arrow window snapping - read them as the
    /// keypad equivalents instead.
    /// </summary>
    private static bool IsExtendedKey(VirtualKey key) => key switch
    {
        VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down => true,
        VirtualKey.Home or VirtualKey.End => true,
        VirtualKey.PageUp or VirtualKey.PageDown => true,
        VirtualKey.Insert or VirtualKey.Delete => true,
        VirtualKey.LeftWindows or VirtualKey.RightWindows or VirtualKey.Applications => true,
        VirtualKey.RightControl or VirtualKey.RightAlt => true,
        VirtualKey.NumLock or VirtualKey.Pause => true,
        _ => false
    };

    private static void Send(params Input[] inputs)
    {
        if (inputs.Length == 0)
        {
            return;
        }

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static Input KeyInput(VirtualKey key, bool keyUp)
    {
        uint flags = keyUp ? KeyEventKeyUp : 0;

        if (IsExtendedKey(key))
        {
            flags |= KeyEventExtendedKey;
        }

        return new Input
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = (ushort)key,
                    ScanCode = 0,
                    Flags = flags,
                    Time = 0,
                    ExtraInfo = GetMessageExtraInfo()
                }
            }
        };
    }

    private static Input MouseEvent(uint flags, int dx = 0, int dy = 0, int mouseData = 0) => new()
    {
        Type = InputMouse,
        Data = new InputUnion
        {
            Mouse = new MouseInput
            {
                Dx = dx,
                Dy = dy,
                MouseData = unchecked((uint)mouseData),
                Flags = flags,
                Time = 0,
                ExtraInfo = GetMessageExtraInfo()
            }
        }
    };

    public static void KeyDown(VirtualKey key) => Send(KeyInput(key, keyUp: false));

    public static void KeyUp(VirtualKey key) => Send(KeyInput(key, keyUp: true));

    /// <summary>Press and release a key as a single batch.</summary>
    public static void KeyPress(VirtualKey key) =>
        Send(KeyInput(key, keyUp: false), KeyInput(key, keyUp: true));

    /// <summary>
    /// Type a string as Unicode characters rather than as virtual keys.
    ///
    /// This is what makes the on-screen keyboard layout-independent: a pound sign is
    /// delivered as a pound sign regardless of how the machine's keyboard is configured.
    /// </summary>
    public static void TypeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // Two events per UTF-16 code unit: press and release. Characters outside the
        // basic plane arrive as surrogate pairs and are sent as two units, which is
        // what Windows expects.
        var inputs = new Input[text.Length * 2];

        for (int i = 0; i < text.Length; i++)
        {
            inputs[i * 2] = UnicodeInput(text[i], keyUp: false);
            inputs[i * 2 + 1] = UnicodeInput(text[i], keyUp: true);
        }

        Send(inputs);
    }

    private static Input UnicodeInput(char character, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = 0,
                ScanCode = character,
                Flags = KeyEventUnicode | (keyUp ? KeyEventKeyUp : 0),
                Time = 0,
                ExtraInfo = GetMessageExtraInfo()
            }
        }
    };

    public static void MoveMouseBy(int dx, int dy) =>
        Send(MouseEvent(MouseEventMove, dx, dy));

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out NativePoint lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct NativePoint
    {
        public int X;
        public int Y;
    }

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    /// <summary>
    /// Bounds of the whole desktop across every monitor, in pixels.
    /// </summary>
    public static (int Left, int Top, int Width, int Height) GetVirtualScreen() =>
    (
        GetSystemMetrics(SM_XVIRTUALSCREEN),
        GetSystemMetrics(SM_YVIRTUALSCREEN),
        Math.Max(1, GetSystemMetrics(SM_CXVIRTUALSCREEN)),
        Math.Max(1, GetSystemMetrics(SM_CYVIRTUALSCREEN))
    );

    /// <summary>
    /// Move the cursor to an exact screen position.
    ///
    /// Absolute movement is used rather than relative because Windows applies its own
    /// acceleration curve ("enhanced pointer precision") to relative input. That curve
    /// fights the deliberately linear stick response this tool is built around: a
    /// measured request to move 40 pixels actually moved 57, and moving back by the same
    /// amount did not return to the starting point. Absolute positioning is applied
    /// exactly as asked, which is what makes slow, predictable aiming possible.
    /// </summary>
    public static void MoveMouseTo(int x, int y)
    {
        var (left, top, width, height) = GetVirtualScreen();

        // Absolute coordinates are normalised across the virtual desktop to a
        // 0-65535 grid, so they must be converted from pixels first.
        int nx = (int)Math.Round((x - left) * 65535.0 / Math.Max(1, width - 1));
        int ny = (int)Math.Round((y - top) * 65535.0 / Math.Max(1, height - 1));

        nx = Math.Clamp(nx, 0, 65535);
        ny = Math.Clamp(ny, 0, 65535);

        Send(MouseEvent(MouseEventMove | MouseEventAbsolute | MouseEventVirtualDesk, nx, ny));
    }

    public static void LeftButtonDown() => Send(MouseEvent(MouseEventLeftDown));

    public static void LeftButtonUp() => Send(MouseEvent(MouseEventLeftUp));

    public static void LeftButtonClick() =>
        Send(MouseEvent(MouseEventLeftDown), MouseEvent(MouseEventLeftUp));

    public static void RightButtonDown() => Send(MouseEvent(MouseEventRightDown));

    public static void RightButtonUp() => Send(MouseEvent(MouseEventRightUp));

    public static void RightButtonClick() =>
        Send(MouseEvent(MouseEventRightDown), MouseEvent(MouseEventRightUp));

    public static void MiddleButtonUp() => Send(MouseEvent(MouseEventMiddleUp));

    public static void MiddleButtonClick() =>
        Send(MouseEvent(MouseEventMiddleDown), MouseEvent(MouseEventMiddleUp));

    /// <summary>
    /// Double-click. Sent as one batch so no cursor movement can slip between the two
    /// clicks and break the double-click gesture.
    /// </summary>
    public static void LeftButtonDoubleClick() =>
        Send(MouseEvent(MouseEventLeftDown), MouseEvent(MouseEventLeftUp),
             MouseEvent(MouseEventLeftDown), MouseEvent(MouseEventLeftUp));

    /// <summary>Scroll vertically, in wheel notches. Positive scrolls up.</summary>
    public static void VerticalScroll(int notches) =>
        Send(MouseEvent(MouseEventWheel, mouseData: notches * WheelDelta));

    /// <summary>Scroll horizontally, in wheel notches. Positive scrolls right.</summary>
    public static void HorizontalScroll(int notches) =>
        Send(MouseEvent(MouseEventHWheel, mouseData: notches * WheelDelta));
}
