using HIDra.Core.Simulation;
using HIDra.Models;
using System;
using System.Collections.Generic;
using WindowsInput.Native;

namespace HIDra.Core.Actions;

/// <summary>
/// Handles execution of button actions based on configuration
/// </summary>
public class ButtonActionHandler
{
    private readonly KeyboardSimulator _keyboardSimulator;
    private readonly MouseSimulator _mouseSimulator;

    /// <summary>
    /// Event for task switcher actions (forward/backward)
    /// </summary>
    public event EventHandler<bool>? TaskSwitcherRequested;
    
    /// <summary>
    /// Event for stick mode swap request
    /// </summary>
    public event EventHandler? StickModeSwapRequested;
    
    /// <summary>
    /// Event for toggling on-screen keyboard
    /// </summary>
    public event EventHandler? ToggleOnScreenKeyboardRequested;

    public ButtonActionHandler(KeyboardSimulator keyboardSimulator, MouseSimulator mouseSimulator)
    {
        _keyboardSimulator = keyboardSimulator ?? throw new ArgumentNullException(nameof(keyboardSimulator));
        _mouseSimulator = mouseSimulator ?? throw new ArgumentNullException(nameof(mouseSimulator));
    }

    /// <summary>
    /// Execute an action mapping
    /// </summary>
    public void ExecuteAction(ActionMapping? actionMapping)
    {
        if (actionMapping == null)
            return;

        switch (actionMapping.Action.ToLowerInvariant())
        {
            case "mouseleftclick":
                _mouseSimulator.LeftClick();
                break;

            case "mouserightclick":
                _mouseSimulator.RightClick();
                break;

            case "mousemiddleclick":
                _mouseSimulator.MiddleClick();
                break;

            case "mousedoubleclick":
                _mouseSimulator.DoubleClick();
                break;

            case "key":
                if (actionMapping.Keys.Count > 0)
                {
                    var key = ParseVirtualKeyCode(actionMapping.Keys[0]);
                    if (key.HasValue)
                        _keyboardSimulator.KeyPress(key.Value);
                }
                break;

            case "keycombo":
                if (actionMapping.Keys.Count > 0)
                {
                    var keys = new List<VirtualKeyCode>();
                    foreach (var keyStr in actionMapping.Keys)
                    {
                        var key = ParseVirtualKeyCode(keyStr);
                        if (key.HasValue)
                            keys.Add(key.Value);
                    }
                    
                    if (keys.Count > 0)
                        _keyboardSimulator.KeyPress(keys.ToArray());
                }
                break;

            case "copy":
                _keyboardSimulator.Copy();
                break;

            case "paste":
                _keyboardSimulator.Paste();
                break;

            case "undo":
                _keyboardSimulator.Undo();
                break;

            case "redo":
                _keyboardSimulator.Redo();
                break;

            case "nexttab":
                _keyboardSimulator.NextTab();
                break;

            case "previoustab":
                _keyboardSimulator.PreviousTab();
                break;

            case "closewindow":
                _keyboardSimulator.CloseWindow();
                break;

            case "openonscreenkeyboard":
                _keyboardSimulator.OpenOnScreenKeyboard();
                break;

            case "taskswitcherforward":
                TaskSwitcherRequested?.Invoke(this, true);
                break;

            case "taskswitcherbackward":
                TaskSwitcherRequested?.Invoke(this, false);
                break;

            case "windowskey":
                _keyboardSimulator.WindowsKey();
                break;

            case "windowstab":
                _keyboardSimulator.WindowsTab();
                break;

            case "swapstickmodes":
                StickModeSwapRequested?.Invoke(this, EventArgs.Empty);
                break;
            
            case "toggleonscreenkeyboard":
                ToggleOnScreenKeyboardRequested?.Invoke(this, EventArgs.Empty);
                break;

            case "maximizewindow":
                _keyboardSimulator.MaximizeWindow();
                break;

            case "minimizewindow":
                _keyboardSimulator.MinimizeWindow();
                break;

            default:
                // Unknown action - do nothing
                break;
        }
    }

    /// <summary>
    /// Parse a string to a VirtualKeyCode
    /// </summary>
    private VirtualKeyCode? ParseVirtualKeyCode(string keyString)
    {
        if (string.IsNullOrWhiteSpace(keyString))
            return null;

        // Handle common key names
        switch (keyString.ToLowerInvariant())
        {
            case "ctrl":
            case "control":
                return VirtualKeyCode.CONTROL;
            case "shift":
                return VirtualKeyCode.SHIFT;
            case "alt":
                return VirtualKeyCode.MENU;
            case "win":
            case "windows":
                return VirtualKeyCode.LWIN;
            case "enter":
            case "return":
                return VirtualKeyCode.RETURN;
            case "space":
            case "spacebar":
                return VirtualKeyCode.SPACE;
            case "tab":
                return VirtualKeyCode.TAB;
            case "backspace":
                return VirtualKeyCode.BACK;
            case "delete":
            case "del":
                return VirtualKeyCode.DELETE;
            case "escape":
            case "esc":
                return VirtualKeyCode.ESCAPE;
            case "home":
                return VirtualKeyCode.HOME;
            case "end":
                return VirtualKeyCode.END;
            case "pageup":
            case "pgup":
                return VirtualKeyCode.PRIOR;
            case "pagedown":
            case "pgdn":
                return VirtualKeyCode.NEXT;
            case "up":
            case "arrowup":
                return VirtualKeyCode.UP;
            case "down":
            case "arrowdown":
                return VirtualKeyCode.DOWN;
            case "left":
            case "arrowleft":
                return VirtualKeyCode.LEFT;
            case "right":
            case "arrowright":
                return VirtualKeyCode.RIGHT;
        }

        // Try to parse as VirtualKeyCode enum
        if (Enum.TryParse<VirtualKeyCode>(keyString, true, out var virtualKeyCode))
            return virtualKeyCode;

        // Handle single character keys
        if (keyString.Length == 1)
        {
            char c = char.ToUpper(keyString[0]);
            if (c >= 'A' && c <= 'Z')
                return (VirtualKeyCode)c;
            if (c >= '0' && c <= '9')
                return (VirtualKeyCode)c;
        }

        // Handle function keys
        if (keyString.ToLowerInvariant().StartsWith("f") && 
            int.TryParse(keyString.Substring(1), out int fNum) && 
            fNum >= 1 && fNum <= 24)
        {
            return VirtualKeyCode.F1 + (fNum - 1);
        }

        return null;
    }
}
