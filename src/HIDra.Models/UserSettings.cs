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
}
