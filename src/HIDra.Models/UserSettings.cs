namespace HIDra.Models;

/// <summary>
/// Per-student settings that are saved between sessions
/// </summary>
public class UserSettings
{
    /// <summary>
    /// Cursor speed (0.05 to 1.0). Default is deliberately slow - too slow is safer than too fast.
    /// </summary>
    public float CursorSensitivity { get; set; } = 0.15f;

    /// <summary>
    /// Whether on-screen keyboards are shown at the top of the screen (false = bottom)
    /// </summary>
    public bool KeyboardAtTop { get; set; } = false;

    /// <summary>
    /// Pause controller input while Grid 3 is running
    /// </summary>
    public bool EnableGrid3AutoSuspend { get; set; } = true;

    /// <summary>
    /// Scroll speed (0.1 to 1.0)
    /// </summary>
    public float ScrollSensitivity { get; set; } = 0.5f;

    /// <summary>
    /// Click automatically when the cursor has been still for <see cref="DwellClickSeconds"/>
    /// </summary>
    public bool DwellClickEnabled { get; set; } = false;

    public float DwellClickSeconds { get; set; } = 1.0f;

    /// <summary>
    /// Type the highlighted key automatically when the highlight has rested on it for
    /// <see cref="KeyboardDwellSeconds"/>
    /// </summary>
    public bool KeyboardDwellEnabled { get; set; } = false;

    public float KeyboardDwellSeconds { get; set; } = 1.5f;

    /// <summary>
    /// Stop Windows opening its own gamepad keyboard whenever a text box gets focus. It
    /// takes over the controller, so HIDra and Windows both react to the same sticks.
    /// </summary>
    public bool StopWindowsKeyboard { get; set; } = true;

    /// <summary>
    /// What the Windows setting was before HIDra switched it off, so switching the option
    /// back off restores the student's own choice. Null when HIDra has not changed it.
    /// </summary>
    public int? OriginalWindowsKeyboardAutoInvoke { get; set; }

    public const float MinKeyboardScale = 0.8f;
    public const float MaxKeyboardScale = 1.5f;

    /// <summary>
    /// Size of the on-screen keyboard, 1.0 being its normal size
    /// </summary>
    public float KeyboardScale { get; set; } = 1.0f;

    /// <summary>
    /// Capitalise the first letter of a sentence, and "i" on its own, without Shift or B
    /// </summary>
    public bool AutoCapitalise { get; set; } = true;

    public const int PhraseCount = 6;

    /// <summary>
    /// The student's own phrases - name, email, sentences they use often - typed whole
    /// from the keyboard's Phrases key. Blank entries are simply not shown.
    /// </summary>
    public List<string> Phrases { get; set; } = new();

    /// <summary>
    /// Let the keyboard fade, so the text behind it shows through, once the controller
    /// has been left alone for <see cref="KeyboardFadeSeconds"/>
    /// </summary>
    public bool KeyboardFadeEnabled { get; set; } = false;

    public float KeyboardFadeSeconds { get; set; } = 2.0f;

    /// <summary>
    /// How visible the faded keyboard stays, 0.2 to 0.6. Never fully invisible, or it
    /// would be unclear whether the keyboard is open at all.
    /// </summary>
    public float KeyboardFadeOpacity { get; set; } = 0.3f;

    /// <summary>
    /// Show the shortcut panel beside the keyboard. Off leaves just the keyboard, for a
    /// student who finds the extra keys too much.
    /// </summary>
    public bool ShowShortcuts { get; set; } = true;

    /// <summary>
    /// How much the sticks are smoothed to even out a wobble: 0 off, 1 a little, 2 more,
    /// 3 most. More smoothing is steadier but lags a little behind the hand.
    /// </summary>
    public int StickSmoothing { get; set; } = 0;

    public const int MaxStickSmoothing = 3;

    /// <summary>
    /// Seconds after letting go of a button during which pressing it again is ignored.
    /// 0 is off.
    /// </summary>
    public float IgnoreRepeatSeconds { get; set; } = 0f;

    /// <summary>
    /// The Gentle pointer curve: slower for a small push, faster for a big one
    /// </summary>
    public bool GentleCurve { get; set; } = false;

    /// <summary>
    /// What LT does while the keyboard is closed. While it is open, LT always moves it.
    /// </summary>
    public LeftTriggerAction LeftTrigger { get; set; } = LeftTriggerAction.Magnifier;

    /// <summary>
    /// The pointer's speed while Slow pointer is on, as a percentage of its normal speed
    /// </summary>
    public int SlowPointerPercent { get; set; } = 30;

    /// <summary>
    /// This student's shortcut keys, row by row, as the ids of the keys chosen: "" for a
    /// slot left empty. Null for the standard set.
    /// </summary>
    public List<string>? ShortcutKeys { get; set; }

    /// <summary>
    /// The programs the Apps key offers this student, as program ids: "" for an empty
    /// slot. Null for the standard choice.
    /// </summary>
    public List<string>? AppKeys { get; set; }
}

/// <summary>
/// The jobs LT can be given while the keyboard is closed. Chosen per student, because what
/// helps one - seeing small things, closing a menu, landing on a small target - is not
/// what helps another. Numbered, because the number is what is saved.
/// </summary>
public enum LeftTriggerAction
{
    /// <summary>Windows Magnifier: zoom in around the pointer, and out again</summary>
    Magnifier = 0,

    /// <summary>Escape: close a menu or box, or leave a slideshow</summary>
    Escape = 1,

    /// <summary>Turn a slower pointer on and off, for small targets</summary>
    SlowPointer = 2,

    /// <summary>LT does nothing while the keyboard is closed</summary>
    Nothing = 3
}
