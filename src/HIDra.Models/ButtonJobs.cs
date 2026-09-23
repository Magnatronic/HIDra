using System.Collections.Generic;
using System.Linq;

namespace HIDra.Models;

/// <summary>
/// A job a controller button can be given: the word shown for it, a line for staff, and
/// the action it runs.
/// </summary>
public sealed record ButtonJob(string Id, string Label, string Description, string Action, params string[] Keys)
{
    /// <summary>
    /// The one button this job can go on, or null for any. Zoom and Slow pointer are run
    /// by the main window rather than the engine, which it only does for LT.
    /// </summary>
    public string? OnlyOn { get; init; }

    public ActionMapping ToMapping() => new()
    {
        Action = Action,
        Keys = Keys.ToList(),
        Description = Label
    };
}

/// <summary>
/// A button whose job can be chosen per student
/// </summary>
/// <param name="Name">The engine's name for it, which is also what is saved</param>
/// <param name="Label">What it is called on the controller</param>
/// <param name="StandardJob">The job it has for a new student</param>
/// <param name="KeepsJobWhileTyping">
/// Whether it can be what opens and closes the keyboard. A, B, Y, LB, RB, LT and the
/// D-pad always type while the keyboard is open, so the keyboard cannot go on them.
/// </param>
/// <param name="TypingJob">
/// What it does instead while the keyboard is open, or null if it keeps its job. The
/// button that opens the keyboard always keeps that job, so it can close it.
/// </param>
public sealed record RemappableButton(string Name, string Label, string StandardJob, bool KeepsJobWhileTyping, string? TypingJob = null);

/// <summary>
/// Every job a button can be given, and which buttons can be given one.
///
/// Staff choose from this list rather than typing key combinations, the same as the
/// shortcut keys: a mistyped combination could do something nobody meant, and the
/// student would be the one left to cope with it.
///
/// Three things can never be taken away, whatever is chosen:
///   - holding Back and Start together brings HIDra back (the engine checks for it
///     before any button's job, so no choice here can touch it);
///   - some button always clicks;
///   - some button that still works while typing always opens and closes the keyboard,
///     or there would be no way to close it again.
/// </summary>
public static class ButtonJobCatalogue
{
    public const string Click = "click";
    public const string Keyboard = "keyboard";
    public const string Nothing = "nothing";

    /// <summary>
    /// LT is a trigger, not a button, so the main window runs its job rather than the
    /// engine's button mappings
    /// </summary>
    public const string LeftTrigger = "LeftTrigger";

    /// <summary>
    /// Ids are what is saved in a student's settings. Never renamed once released.
    /// </summary>
    public static readonly IReadOnlyList<ButtonJob> All = new[]
    {
        new ButtonJob(Click, "Click", "A left click", "MouseLeftClick"),
        new ButtonJob("right-click", "Right click", "Opens a menu for what is under the pointer", "MouseRightClick"),
        new ButtonJob("double-click", "Double click", "Opens a file or folder", "MouseDoubleClick"),
        new ButtonJob(Keyboard, "Open the keyboard", "Opens and closes HIDra's keyboard", "ToggleOnScreenKeyboard"),
        new ButtonJob("swap-sticks", "Swap the two sticks", "Pointer on the other stick, for the other hand", "SwapStickModes"),
        new ButtonJob("switch-programs", "Show open programs", "Press again to move along; A picks one, B goes back", "TaskSwitcherBackward"),
        new ButtonJob("start-menu", "Start menu", "The Windows Start menu", "WindowsKey"),
        new ButtonJob("all-windows", "All windows", "Every open window at once (Task View)", "WindowsTab"),
        new ButtonJob("maximise", "Maximise window", "Makes the window fill the screen", "MaximizeWindow"),
        new ButtonJob("minimise", "Minimise window", "Hides the window on the taskbar", "MinimizeWindow"),
        new ButtonJob("snap-left", "Snap window left", "Puts the window on the left half", "KeyCombo", "LWin", "Left"),
        new ButtonJob("snap-right", "Snap window right", "Puts the window on the right half", "KeyCombo", "LWin", "Right"),
        new ButtonJob("undo", "Undo", "Takes back the last change (Ctrl+Z)", "Undo"),
        new ButtonJob("redo", "Redo", "Puts back what Undo took away (Ctrl+Y)", "Redo"),
        new ButtonJob("copy", "Copy", "Copies what is selected (Ctrl+C)", "Copy"),
        new ButtonJob("paste", "Paste", "Pastes what was copied (Ctrl+V)", "Paste"),
        new ButtonJob("escape", "Escape", "Closes a menu or box, or ends a slideshow", "Key", "Escape"),
        new ButtonJob("enter", "Enter", "Presses Enter", "Key", "Enter"),
        new ButtonJob("tab", "Tab", "Moves to the next box or button", "Key", "Tab"),
        new ButtonJob("close-window", "Close window", "Closes the window in front (Alt+F4)", "CloseWindow"),
        new ButtonJob("zoom", "Zoom in and out", "Windows Magnifier: bigger around the pointer. Again to zoom out", "") { OnlyOn = "LeftTrigger" },
        new ButtonJob("slow-pointer", "Slow pointer on and off", "A slower pointer for small targets. Its speed is on the Pointer tab", "") { OnlyOn = "LeftTrigger" },
        new ButtonJob(Nothing, "Nothing", "The button does nothing", ""),
    };

    /// <summary>
    /// The buttons whose jobs can be chosen. RT stays click-and-drag and the sticks stay
    /// pointer and scroll, because each is the only way to do those. LT's job is only
    /// while the keyboard is closed - while it is open, LT always moves the keyboard.
    /// </summary>
    public static readonly IReadOnlyList<RemappableButton> Buttons = new[]
    {
        new RemappableButton(LeftTrigger, "LT", "escape", false, "Keyboard to top or bottom"),
        new RemappableButton("ButtonA", "A", Click, false, "Type the key"),
        new RemappableButton("ButtonB", "B", "right-click", false, "Capital, or the symbol on top"),
        new RemappableButton("ButtonX", "X", Keyboard, true),
        new RemappableButton("ButtonY", "Y", "swap-sticks", false, "Enter"),
        new RemappableButton("LeftBumper", "LB", "switch-programs", false, "Backspace"),
        new RemappableButton("RightBumper", "RB", "double-click", false, "Space"),
        new RemappableButton("Back", "Back", "start-menu", true, "Escape"),
        new RemappableButton("Start", "Start", "all-windows", true, "Caps Lock"),
        new RemappableButton("DpadUp", "D-pad up", "maximise", false, "Move the orange box"),
        new RemappableButton("DpadDown", "D-pad down", "minimise", false, "Move the orange box"),
        new RemappableButton("DpadLeft", "D-pad left", "snap-left", false, "Text cursor left"),
        new RemappableButton("DpadRight", "D-pad right", "snap-right", false, "Text cursor right"),
        new RemappableButton("LeftStickClick", "Left stick press", "undo", true, "Numbers, symbols"),
        new RemappableButton("RightStickClick", "Right stick press", "undo", true),
    };

    public static ButtonJob? Find(string? id) => All.FirstOrDefault(job => job.Id == id);

    public static RemappableButton? Button(string name) => Buttons.FirstOrDefault(b => b.Name == name);

    /// <summary>
    /// The job each button has for this student: their own choice where they have one,
    /// the standard job otherwise
    /// </summary>
    public static Dictionary<string, ButtonJob> Resolve(IReadOnlyDictionary<string, string>? chosen)
    {
        var jobs = new Dictionary<string, ButtonJob>();
        foreach (var button in Buttons)
        {
            jobs[button.Name] = (chosen != null && chosen.TryGetValue(button.Name, out var id) ? Find(id) : null)
                ?? Find(button.StandardJob)!;
        }
        return jobs;
    }

    /// <summary>
    /// The engine's mappings for this student's jobs
    /// </summary>
    public static Dictionary<string, ButtonMapping> ToMappings(IReadOnlyDictionary<string, string>? chosen)
    {
        var mappings = new Dictionary<string, ButtonMapping>();
        foreach (var (name, job) in Resolve(chosen))
        {
            if (job.Id != Nothing && name != LeftTrigger)
            {
                mappings[name] = new ButtonMapping { Default = job.ToMapping() };
            }
        }
        return mappings;
    }

    /// <summary>
    /// Whether this job can go on this button. The keyboard only goes where it can be
    /// closed again.
    /// </summary>
    public static bool Allowed(RemappableButton button, ButtonJob job) =>
        (job.Id != Keyboard || button.KeepsJobWhileTyping) && (job.OnlyOn == null || job.OnlyOn == button.Name);

    /// <summary>
    /// Why this button cannot be given a different job right now, or null if it can: it
    /// holds the last click, or the last way to open the keyboard.
    /// </summary>
    public static string? WhyLocked(IReadOnlyDictionary<string, string>? chosen, string buttonName)
    {
        var jobs = Resolve(chosen);
        string current = jobs[buttonName].Id;

        if (current is not (Click or Keyboard))
        {
            return null;
        }

        bool elsewhere = jobs.Any(pair => pair.Key != buttonName && pair.Value.Id == current);
        if (elsewhere)
        {
            return null;
        }

        return current == Click
            ? "This is the only button that clicks. Give Click to another button first, then this one can change."
            : "This is the only button that opens the keyboard. Give it to X, Back, Start or a stick press first, then this one can change.";
    }

    /// <summary>
    /// The saved choices with anything unknown or unsafe taken out: unknown buttons and
    /// jobs are dropped, and if no button would click or open the keyboard, every button
    /// goes back to standard. Null when every button has its standard job.
    /// </summary>
    public static Dictionary<string, string>? Clean(Dictionary<string, string>? chosen)
    {
        if (chosen == null)
        {
            return null;
        }

        var clean = new Dictionary<string, string>();
        foreach (var (name, id) in chosen)
        {
            var button = Button(name);
            var job = Find(id);
            if (button != null && job != null && Allowed(button, job) && job.Id != button.StandardJob)
            {
                clean[name] = id;
            }
        }

        return clean.Count == 0 || !IsSafe(clean) ? null : clean;
    }

    /// <summary>
    /// Whether some button still clicks, and some button that works while typing still
    /// opens and closes the keyboard
    /// </summary>
    public static bool IsSafe(IReadOnlyDictionary<string, string>? chosen)
    {
        var jobs = Resolve(chosen);
        bool clicks = jobs.Values.Any(job => job.Id == Click);
        bool opensKeyboard = jobs.Any(pair => pair.Value.Id == Keyboard && Button(pair.Key)!.KeepsJobWhileTyping);
        return clicks && opensKeyboard;
    }
}
