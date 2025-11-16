using System;
using System.Runtime.InteropServices;
using System.Threading;
using SharpDX.XInput;

namespace ControllerTest
{
    class Program
    {
        // XInput test
        private static Controller? xinputController;
        
        // Raw Input structures
        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTDEVICE
        {
            public ushort UsagePage;
            public ushort Usage;
            public int Flags;
            public IntPtr Target;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, int uiNumDevices, int cbSize);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        // HID API structures
        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_GetProductString(IntPtr HidDeviceObject, byte[] Buffer, int BufferLength);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, int Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiEnumDeviceInterfaces(IntPtr DeviceInfoSet, IntPtr DeviceInfoData, ref Guid InterfaceClassGuid, int MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=== Xbox Controller Input Level Test ===");
            Console.WriteLine("This tool tests at which level Grid 3 is intercepting the controller.\n");
            
            Console.WriteLine("Test 1: XInput API (High-level)");
            TestXInput();
            
            Console.WriteLine("\nTest 2: Raw Input API (Mid-level)");
            TestRawInput();
            
            Console.WriteLine("\nTest 3: HID Device Enumeration (Low-level)");
            TestHIDDevices();
            
            Console.WriteLine("\n=== Test Complete ===");
            Console.WriteLine("\nInterpretation:");
            Console.WriteLine("- If XInput sees controller but no input: Grid 3 intercepts at XInput layer");
            Console.WriteLine("- If Raw Input registers but no data: Grid 3 intercepts at driver layer");
            Console.WriteLine("- If HID shows device but can't open: Grid 3 has exclusive device access");
            Console.WriteLine("- If nothing works: Grid 3 uses a filter driver or similar low-level hook");
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        static void TestXInput()
        {
            Console.WriteLine("Checking for Xbox controller via XInput...");
            
            // Try all 4 possible controller slots
            for (int i = 0; i < 4; i++)
            {
                var controller = new Controller((UserIndex)i);
                if (controller.IsConnected)
                {
                    Console.WriteLine($"  ✓ Controller found in slot {i}");
                    xinputController = controller;
                    
                    // Try to read state
                    Console.WriteLine("  Reading controller state for 3 seconds...");
                    Console.WriteLine("  Move sticks or press buttons now!");
                    
                    var startTime = DateTime.Now;
                    bool anyInput = false;
                    
                    while ((DateTime.Now - startTime).TotalSeconds < 3)
                    {
                        var state = controller.GetState();
                        var gamepad = state.Gamepad;
                        
                        if (gamepad.LeftThumbX != 0 || gamepad.LeftThumbY != 0 ||
                            gamepad.RightThumbX != 0 || gamepad.RightThumbY != 0 ||
                            gamepad.Buttons != 0)
                        {
                            Console.WriteLine($"    Input detected! Buttons: {gamepad.Buttons}, " +
                                            $"LStick: ({gamepad.LeftThumbX}, {gamepad.LeftThumbY}), " +
                                            $"RStick: ({gamepad.RightThumbX}, {gamepad.RightThumbY})");
                            anyInput = true;
                        }
                        
                        Thread.Sleep(100);
                    }
                    
                    if (!anyInput)
                    {
                        Console.WriteLine("    ✗ NO INPUT DETECTED - Grid 3 may be intercepting at XInput level");
                    }
                    else
                    {
                        Console.WriteLine("    ✓ Input working - Grid 3 not blocking XInput");
                    }
                    
                    return;
                }
            }
            
            Console.WriteLine("  ✗ No Xbox controller detected via XInput");
        }

        static void TestRawInput()
        {
            Console.WriteLine("Attempting to register for Raw Input...");
            
            try
            {
                var hwnd = GetConsoleWindow();
                if (hwnd == IntPtr.Zero)
                {
                    Console.WriteLine("  ✗ Cannot get console window handle");
                    return;
                }
                
                var devices = new RAWINPUTDEVICE[1];
                devices[0].UsagePage = 0x01; // Generic Desktop
                devices[0].Usage = 0x05;     // Game Pad
                devices[0].Flags = 0x00000100; // RIDEV_INPUTSINK
                devices[0].Target = hwnd;
                
                bool registered = RegisterRawInputDevices(devices, 1, Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
                
                if (registered)
                {
                    Console.WriteLine("  ✓ Successfully registered for Raw Input gamepad messages");
                    Console.WriteLine("  Note: Console apps can't easily receive WM_INPUT messages");
                    Console.WriteLine("        If XInput works but HIDra Raw Input doesn't, Grid 3 blocks WM_INPUT");
                }
                else
                {
                    Console.WriteLine($"  ✗ Failed to register Raw Input. Error: {Marshal.GetLastWin32Error()}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Exception during Raw Input test: {ex.Message}");
            }
        }

        static void TestHIDDevices()
        {
            Console.WriteLine("Enumerating HID devices...");
            
            // HID GUID: {4d1e55b2-f16f-11cf-88cb-001111000030}
            var hidGuid = new Guid(0x4d1e55b2, 0xf16f, 0x11cf, 0x88, 0xcb, 0x00, 0x11, 0x11, 0x00, 0x00, 0x30);
            
            IntPtr deviceInfoSet = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, 0x00000010 | 0x00000002);
            
            if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == new IntPtr(-1))
            {
                Console.WriteLine("  ✗ Failed to enumerate HID devices");
                return;
            }
            
            int deviceCount = 0;
            int memberIndex = 0;
            
            while (true)
            {
                var deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA();
                deviceInterfaceData.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));
                
                bool success = SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, memberIndex, ref deviceInterfaceData);
                
                if (!success)
                    break;
                
                deviceCount++;
                memberIndex++;
            }
            
            Console.WriteLine($"  Found {deviceCount} HID devices");
            
            if (deviceCount > 0)
            {
                Console.WriteLine("  ✓ HID device enumeration works");
                Console.WriteLine("  If Grid 3 blocks here, it's using a very low-level filter driver");
            }
            else
            {
                Console.WriteLine("  ! No HID devices found (unusual - may indicate driver-level blocking)");
            }
        }
    }
}
