using System;
using System.Runtime.InteropServices;

namespace HIDra.Core.Controllers;

/// <summary>
/// Direct P/Invoke bindings for XInput 1.4 (Windows 8 and later).
///
/// This replaces the SharpDX.XInput wrapper, which has been unmaintained since 2019.
/// XInput is a small, stable C API, so binding it directly removes an abandoned
/// dependency, removes a native DLL from the single-file bundle (which helps in
/// locked-down environments that block execution from the extraction directory),
/// and gives us access to battery information that the tool needs in order to warn
/// before a controller dies mid-session.
/// </summary>
internal static class XInputNative
{
    private const string XInputDll = "xinput1_4.dll";

    /// <summary>XInput supports at most four controllers, indexed 0-3.</summary>
    public const int MaxControllerCount = 4;

    public const int ErrorSuccess = 0;
    public const int ErrorDeviceNotConnected = 1167;

    /// <summary>Battery information is being requested for the gamepad itself, not a headset.</summary>
    public const byte BatteryDeviceTypeGamepad = 0x00;

    [DllImport(XInputDll, EntryPoint = "XInputGetState")]
    public static extern int XInputGetState(int dwUserIndex, out XInputState pState);

    [DllImport(XInputDll, EntryPoint = "XInputGetBatteryInformation")]
    public static extern int XInputGetBatteryInformation(
        int dwUserIndex,
        byte devType,
        out XInputBatteryInformation pBatteryInformation);

    [DllImport(XInputDll, EntryPoint = "XInputSetState")]
    public static extern int XInputSetState(int dwUserIndex, ref XInputVibration pVibration);

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputState
    {
        /// <summary>
        /// Increments only when the state actually changes, so it can be used to skip
        /// redundant work when the controller is sitting still.
        /// </summary>
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputBatteryInformation
    {
        public byte BatteryType;
        public byte BatteryLevel;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputVibration
    {
        public ushort LeftMotorSpeed;
        public ushort RightMotorSpeed;
    }

    /// <summary>XINPUT_GAMEPAD_* button flags.</summary>
    [Flags]
    public enum GamepadButtons : ushort
    {
        DPadUp = 0x0001,
        DPadDown = 0x0002,
        DPadLeft = 0x0004,
        DPadRight = 0x0008,
        Start = 0x0010,
        Back = 0x0020,
        LeftThumb = 0x0040,
        RightThumb = 0x0080,
        LeftShoulder = 0x0100,
        RightShoulder = 0x0200,
        A = 0x1000,
        B = 0x2000,
        X = 0x4000,
        Y = 0x8000
    }

    /// <summary>XINPUT_BATTERY_TYPE_* values.</summary>
    public static class BatteryTypes
    {
        public const byte Disconnected = 0x00;
        public const byte Wired = 0x01;
        public const byte Alkaline = 0x02;
        public const byte NiMh = 0x03;
        public const byte Unknown = 0xFF;
    }

    /// <summary>XINPUT_BATTERY_LEVEL_* values.</summary>
    public static class BatteryLevels
    {
        public const byte Empty = 0x00;
        public const byte Low = 0x01;
        public const byte Medium = 0x02;
        public const byte Full = 0x03;
    }
}
