using HIDra.Core.Simulation;
using HIDra.Models;
using System;
using System.Collections.Generic;

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
                    var key = ParseVirtualKey(actionMapping.Keys[0]);
                    if (key.HasValue)
                        _keyboardSimulator.KeyPress(key.Value);
                }
                break;

            case "keycombo":
                if (actionMapping.Keys.Count > 0)
                {
                    var keys = new List<VirtualKey>();
                    foreach (var keyStr in actionMapping.Keys)
                    {
                        var key = ParseVirtualKey(keyStr);
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
    /// Parse a string to a VirtualKey
    /// </summary>
    private VirtualKey? ParseVirtualKey(string keyString)
    {
        if (string.IsNullOrWhiteSpace(keyString))
            return null;

        // Handle common key names
        switch (keyString.ToLowerInvariant())
        {
            case "ctrl":
            case "control":
                return VirtualKey.Control;
            case "shift":
                return VirtualKey.Shift;
            case "alt":
                return VirtualKey.Menu;
            case "win":
            case "windows":
                return VirtualKey.LeftWindows;
            case "enter":
            case "return":
                return VirtualKey.Return;
            case "space":
            case "spacebar":
                return VirtualKey.Space;
            case "tab":
                return VirtualKey.Tab;
            case "backspace":
                return VirtualKey.Back;
            case "delete":
            case "del":
                return VirtualKey.Delete;
            case "escape":
            case "esc":
                return VirtualKey.Escape;
            case "home":
                return VirtualKey.Home;
            case "end":
                return VirtualKey.End;
            case "pageup":
            case "pgup":
                return VirtualKey.PageUp;
            case "pagedown":
            case "pgdn":
                return VirtualKey.PageDown;
            case "up":
            case "arrowup":
                return VirtualKey.Up;
            case "down":
            case "arrowdown":
                return VirtualKey.Down;
            case "left":
            case "arrowleft":
                return VirtualKey.Left;
            case "right":
            case "arrowright":
                return VirtualKey.Right;
        }

        // Try to parse as VirtualKey enum
        if (Enum.TryParse<VirtualKey>(keyString, true, out var virtualKeyCode))
            return virtualKeyCode;

        // Handle single character keys
        if (keyString.Length == 1)
        {
            char c = char.ToUpper(keyString[0]);
            if (c >= 'A' && c <= 'Z')
                return (VirtualKey)c;
            if (c >= '0' && c <= '9')
                return (VirtualKey)c;
        }

        // Handle function keys
        if (keyString.ToLowerInvariant().StartsWith("f") && 
            int.TryParse(keyString.Substring(1), out int fNum) && 
            fNum >= 1 && fNum <= 24)
        {
            // VK_F1 through VK_F24 are contiguous, so the codes stay correct beyond
            // F12 even though the enum only names that far.
            return (VirtualKey)((int)VirtualKey.F1 + (fNum - 1));
        }

        return null;
    }
}
