using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using WindowsInput;
using WindowsInput.Native;

namespace HIDra.Core.Simulation;

/// <summary>
/// Simulates keyboard input
/// </summary>
public class KeyboardSimulator : IDisposable
{
    private readonly InputSimulator _simulator;
    private readonly HashSet<VirtualKeyCode> _heldKeys;

    // Windows API declarations for window management
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_MAXIMIZE = 3;
    private const int SW_MINIMIZE = 6;

    public KeyboardSimulator()
    {
        _simulator = new InputSimulator();
        _heldKeys = new HashSet<VirtualKeyCode>();
    }

    /// <summary>
    /// Press a key
    /// </summary>
    public void KeyPress(VirtualKeyCode key)
    {
        _simulator.Keyboard.KeyPress(key);
    }

    /// <summary>
    /// Press multiple keys (e.g., Ctrl+C)
    /// </summary>
    public void KeyPress(params VirtualKeyCode[] keys)
    {
        if (keys.Length == 1)
        {
            KeyPress(keys[0]);
        }
        else if (keys.Length > 1)
        {
            // Hold all modifier keys
            for (int i = 0; i < keys.Length - 1; i++)
            {
                _simulator.Keyboard.KeyDown(keys[i]);
            }

            // Press the final key
            _simulator.Keyboard.KeyPress(keys[^1]);

            // Release modifiers in reverse order
            for (int i = keys.Length - 2; i >= 0; i--)
            {
                _simulator.Keyboard.KeyUp(keys[i]);
            }
        }
    }

    /// <summary>
    /// Hold a key down
    /// </summary>
    public void KeyDown(VirtualKeyCode key)
    {
        if (!_heldKeys.Contains(key))
        {
            _simulator.Keyboard.KeyDown(key);
            _heldKeys.Add(key);
        }
    }

    /// <summary>
    /// Release a key
    /// </summary>
    public void KeyUp(VirtualKeyCode key)
    {
        if (_heldKeys.Contains(key))
        {
            _simulator.Keyboard.KeyUp(key);
            _heldKeys.Remove(key);
        }
    }

    /// <summary>
    /// Type text
    /// </summary>
    public void TypeText(string text)
    {
        _simulator.Keyboard.TextEntry(text);
    }

    /// <summary>
    /// Simulate Alt+Tab (quick switch - not recommended for task switcher mode)
    /// </summary>
    public void AltTab()
    {
        KeyPress(VirtualKeyCode.MENU, VirtualKeyCode.TAB);
    }

    /// <summary>
    /// Simulate Alt+Shift+Tab (quick switch - not recommended for task switcher mode)
    /// </summary>
    public void AltShiftTab()
    {
        KeyPress(VirtualKeyCode.MENU, VirtualKeyCode.SHIFT, VirtualKeyCode.TAB);
    }

    /// <summary>
    /// Enter task switcher mode by holding Alt and pressing Tab
    /// </summary>
    public void EnterTaskSwitcher()
    {
        // Ensure Alt is not stuck from previous operation
        _simulator.Keyboard.KeyUp(VirtualKeyCode.MENU);
        _heldKeys.Remove(VirtualKeyCode.MENU);
        
        System.Threading.Thread.Sleep(10); // Brief pause
        
        KeyDown(VirtualKeyCode.MENU); // Hold Alt
        System.Threading.Thread.Sleep(30); // Small delay to ensure Alt is registered
        KeyPress(VirtualKeyCode.TAB);  // Tap Tab
    }

    /// <summary>
    /// Navigate forward in task switcher (while Alt is held)
    /// </summary>
    public void TaskSwitcherNext()
    {
        KeyPress(VirtualKeyCode.TAB);
    }

    /// <summary>
    /// Navigate backward in task switcher (while Alt is held)
    /// </summary>
    public void TaskSwitcherPrevious()
    {
        KeyPress(VirtualKeyCode.SHIFT, VirtualKeyCode.TAB);
    }

    /// <summary>
    /// Exit task switcher by releasing Alt
    /// </summary>
    public void ExitTaskSwitcher()
    {
        // Small delay to ensure Windows processes the selection
        System.Threading.Thread.Sleep(50);
        
        // Force release Alt even if not tracked in _heldKeys (for safety)
        _simulator.Keyboard.KeyUp(VirtualKeyCode.MENU);
        _heldKeys.Remove(VirtualKeyCode.MENU);
    }

    /// <summary>
    /// Simulate Windows key
    /// </summary>
    public void WindowsKey()
    {
        KeyPress(VirtualKeyCode.LWIN);
    }

    /// <summary>
    /// Simulate Windows+Tab (Task View)
    /// </summary>
    public void WindowsTab()
    {
        KeyPress(VirtualKeyCode.LWIN, VirtualKeyCode.TAB);
    }

    /// <summary>
    /// Simulate Ctrl+C (Copy)
    /// </summary>
    public void Copy()
    {
        KeyPress(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_C);
    }

    /// <summary>
    /// Simulate Ctrl+V (Paste)
    /// </summary>
    public void Paste()
    {
        KeyPress(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_V);
    }

    /// <summary>
    /// Simulate Ctrl+Z (Undo)
    /// </summary>
    public void Undo()
    {
        KeyPress(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_Z);
    }

    /// <summary>
    /// Simulate Ctrl+Y (Redo)
    /// </summary>
    public void Redo()
    {
        KeyPress(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_Y);
    }

    /// <summary>
    /// Simulate Ctrl+Tab (Next Tab)
    /// </summary>
    public void NextTab()
    {
        KeyPress(VirtualKeyCode.CONTROL, VirtualKeyCode.TAB);
    }

    /// <summary>
    /// Simulate Ctrl+Shift+Tab (Previous Tab)
    /// </summary>
    public void PreviousTab()
    {
        KeyPress(VirtualKeyCode.CONTROL, VirtualKeyCode.SHIFT, VirtualKeyCode.TAB);
    }

    /// <summary>
    /// Simulate Alt+F4 (Close Window)
    /// </summary>
    public void CloseWindow()
    {
        KeyPress(VirtualKeyCode.MENU, VirtualKeyCode.F4);
    }

    /// <summary>
    /// Open on-screen keyboard
    /// </summary>
    public void OpenOnScreenKeyboard()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "osk.exe",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to open on-screen keyboard: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Toggle on-screen keyboard (will be handled by UI layer with virtual keyboard window)
    /// This method now serves as a placeholder that the UI will override
    /// </summary>
    public void ToggleOnScreenKeyboard()
    {
        // This will be handled by the UI layer's VirtualKeyboardWindow
        // The ButtonActionHandler will raise an event that MainWindow captures
        Console.WriteLine("Toggle virtual keyboard requested");
    }

    /// <summary>
    /// Maximize the active window using Windows API
    /// </summary>
    public void MaximizeWindow()
    {
        IntPtr hWnd = GetForegroundWindow();
        if (hWnd != IntPtr.Zero)
        {
            ShowWindow(hWnd, SW_MAXIMIZE);
        }
    }

    /// <summary>
    /// Minimize the active window using Windows API
    /// </summary>
    public void MinimizeWindow()
    {
        IntPtr hWnd = GetForegroundWindow();
        if (hWnd != IntPtr.Zero)
        {
            ShowWindow(hWnd, SW_MINIMIZE);
        }
    }

    /// <summary>
    /// Release all held keys
    /// </summary>
    public void ReleaseAll()
    {
        foreach (var key in _heldKeys.ToArray())
        {
            _simulator.Keyboard.KeyUp(key);
        }
        _heldKeys.Clear();
    }

    public void Dispose()
    {
        ReleaseAll();
    }
}
