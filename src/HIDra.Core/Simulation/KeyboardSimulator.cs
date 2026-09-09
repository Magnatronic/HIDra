using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HIDra.Models;

namespace HIDra.Core.Simulation;

/// <summary>
/// Simulates keyboard input
/// </summary>
public class KeyboardSimulator : IDisposable
{
    private readonly HashSet<VirtualKey> _heldKeys;

    // Windows API declarations for window management
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_MAXIMIZE = 3;
    private const int SW_MINIMIZE = 6;

    public KeyboardSimulator()
    {
        _heldKeys = new HashSet<VirtualKey>();
    }

    /// <summary>
    /// Press a key
    /// </summary>
    public void KeyPress(VirtualKey key)
    {
        NativeInput.KeyPress(key);
    }

    /// <summary>
    /// Press multiple keys (e.g., Ctrl+C)
    /// </summary>
    public void KeyPress(params VirtualKey[] keys)
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
                NativeInput.KeyDown(keys[i]);
            }

            // Press the final key
            NativeInput.KeyPress(keys[^1]);

            // Release modifiers in reverse order
            for (int i = keys.Length - 2; i >= 0; i--)
            {
                NativeInput.KeyUp(keys[i]);
            }
        }
    }

    /// <summary>
    /// Hold a key down
    /// </summary>
    public void KeyDown(VirtualKey key)
    {
        if (!_heldKeys.Contains(key))
        {
            NativeInput.KeyDown(key);
            _heldKeys.Add(key);
        }
    }

    /// <summary>
    /// Release a key
    /// </summary>
    public void KeyUp(VirtualKey key)
    {
        if (_heldKeys.Contains(key))
        {
            NativeInput.KeyUp(key);
            _heldKeys.Remove(key);
        }
    }

    /// <summary>
    /// Type text
    /// </summary>
    public void TypeText(string text)
    {
        NativeInput.TypeText(text);
    }

    /// <summary>
    /// Simulate Alt+Tab (quick switch - not recommended for task switcher mode)
    /// </summary>
    public void AltTab()
    {
        KeyPress(VirtualKey.Menu, VirtualKey.Tab);
    }

    /// <summary>
    /// Simulate Alt+Shift+Tab (quick switch - not recommended for task switcher mode)
    /// </summary>
    public void AltShiftTab()
    {
        KeyPress(VirtualKey.Menu, VirtualKey.Shift, VirtualKey.Tab);
    }

    /// <summary>
    /// Enter task switcher mode by holding Alt and pressing Tab
    /// </summary>
    public void EnterTaskSwitcher()
    {
        // Ensure Alt is not stuck from previous operation
        NativeInput.KeyUp(VirtualKey.Menu);
        _heldKeys.Remove(VirtualKey.Menu);
        
        System.Threading.Thread.Sleep(10); // Brief pause
        
        KeyDown(VirtualKey.Menu); // Hold Alt
        System.Threading.Thread.Sleep(30); // Small delay to ensure Alt is registered
        KeyPress(VirtualKey.Tab);  // Tap Tab
    }

    /// <summary>
    /// Navigate forward in task switcher (while Alt is held)
    /// </summary>
    public void TaskSwitcherNext()
    {
        KeyPress(VirtualKey.Tab);
    }

    /// <summary>
    /// Navigate backward in task switcher (while Alt is held)
    /// </summary>
    public void TaskSwitcherPrevious()
    {
        KeyPress(VirtualKey.Shift, VirtualKey.Tab);
    }

    /// <summary>
    /// Exit task switcher by releasing Alt
    /// </summary>
    public void ExitTaskSwitcher()
    {
        // Small delay to ensure Windows processes the selection
        System.Threading.Thread.Sleep(50);
        
        // Force release Alt even if not tracked in _heldKeys (for safety)
        NativeInput.KeyUp(VirtualKey.Menu);
        _heldKeys.Remove(VirtualKey.Menu);
    }

    /// <summary>
    /// Simulate Windows key
    /// </summary>
    public void WindowsKey()
    {
        KeyPress(VirtualKey.LeftWindows);
    }

    /// <summary>
    /// Simulate Windows+Tab (Task View)
    /// </summary>
    public void WindowsTab()
    {
        KeyPress(VirtualKey.LeftWindows, VirtualKey.Tab);
    }

    /// <summary>
    /// Simulate Ctrl+C (Copy)
    /// </summary>
    public void Copy()
    {
        KeyPress(VirtualKey.Control, VirtualKey.C);
    }

    /// <summary>
    /// Simulate Ctrl+V (Paste)
    /// </summary>
    public void Paste()
    {
        KeyPress(VirtualKey.Control, VirtualKey.V);
    }

    /// <summary>
    /// Simulate Ctrl+Z (Undo)
    /// </summary>
    public void Undo()
    {
        KeyPress(VirtualKey.Control, VirtualKey.Z);
    }

    /// <summary>
    /// Simulate Ctrl+Y (Redo)
    /// </summary>
    public void Redo()
    {
        KeyPress(VirtualKey.Control, VirtualKey.Y);
    }

    /// <summary>
    /// Simulate Ctrl+Tab (Next Tab)
    /// </summary>
    public void NextTab()
    {
        KeyPress(VirtualKey.Control, VirtualKey.Tab);
    }

    /// <summary>
    /// Simulate Ctrl+Shift+Tab (Previous Tab)
    /// </summary>
    public void PreviousTab()
    {
        KeyPress(VirtualKey.Control, VirtualKey.Shift, VirtualKey.Tab);
    }

    /// <summary>
    /// Simulate Alt+F4 (Close Window)
    /// </summary>
    public void CloseWindow()
    {
        KeyPress(VirtualKey.Menu, VirtualKey.F4);
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
            NativeInput.KeyUp(key);
        }
        _heldKeys.Clear();
    }

    public void Dispose()
    {
        ReleaseAll();
    }
}
