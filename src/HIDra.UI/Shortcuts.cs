using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using HIDra.Models;

namespace HIDra.UI;

/// <summary>
/// What a shortcut key does when pressed
/// </summary>
public enum ShortcutKind
{
    /// <summary>Sends its keys</summary>
    Keys,

    /// <summary>Turns selecting on and off</summary>
    SelectSwitch,

    /// <summary>Reads the selected text aloud</summary>
    ReadAloud,

    /// <summary>Shows the keyboard's own emoji in place of the letters</summary>
    EmojiLayer
}

/// <summary>
/// The four fixed rows of the shortcut panel. A key can only be put in its own group's
/// row, so each row keeps meaning one kind of thing however it is customised.
/// </summary>
public enum ShortcutGroup
{
    Edit,
    Select,
    Style,
    Tools
}

/// <summary>
/// A shortcut key: its word, the icon shown above the word, and the keys it sends.
///
/// The icons are Windows' own (Segoe Fluent Icons, or Segoe MDL2 Assets on Windows 10),
/// so nothing needs bundling - and they are the same icons as the buttons in PowerPoint
/// and Word, so recognising one helps with the other. Some students use AAC, where a
/// picture with its word is easier to find than a word alone.
/// </summary>
public sealed record Shortcut(string Label, char Icon, params VirtualKey[] Keys)
{
    public ShortcutKind Kind { get; init; } = ShortcutKind.Keys;

    /// <summary>What is saved in a student's settings. Never renamed once released.</summary>
    public string Id { get; init; } = "";

    public ShortcutGroup Group { get; init; }

    /// <summary>A line for staff choosing keys, saying what it is for</summary>
    public string Description { get; init; } = "";
}

/// <summary>
/// Every key the shortcut panel can hold, and the standard set a new student starts with.
///
/// Staff choose from this list rather than typing key combinations: a mistyped
/// combination could do something unexpected, and everything here is known to be safe
/// and has a proper icon and word. Anything missing is added here.
/// </summary>
public static class ShortcutCatalogue
{
    public const int Rows = 4;
    public const int RowLength = 5;
    public const int SlotCount = Rows * RowLength;

    public static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    private static VirtualKey K(char letter) => (VirtualKey)char.ToUpperInvariant(letter);

    private static Shortcut Make(string id, ShortcutGroup group, string label, char icon, string description,
        params VirtualKey[] keys) =>
        new(label, icon, keys) { Id = id, Group = group, Description = description };

    public static readonly IReadOnlyList<Shortcut> All = new[]
    {
        Make("undo", ShortcutGroup.Edit, "Undo", '\uE7A7', "Take back the last change", VirtualKey.Control, K('z')),
        Make("redo", ShortcutGroup.Edit, "Redo", '\uE7A6', "Put back what Undo took away", VirtualKey.Control, K('y')),
        Make("cut", ShortcutGroup.Edit, "Cut", '\uE8C6', "Move the selection, ready to paste", VirtualKey.Control, K('x')),
        Make("copy", ShortcutGroup.Edit, "Copy", '\uE8C8', "Copy the selection, ready to paste", VirtualKey.Control, K('c')),
        Make("paste", ShortcutGroup.Edit, "Paste", '\uE77F', "Put in what was cut or copied", VirtualKey.Control, K('v')),
        Make("find", ShortcutGroup.Edit, "Find", '\uE721', "Search for words on the page", VirtualKey.Control, K('f')),
        Make("clipboard", ShortcutGroup.Edit, "Clipboard", '\uE81C', "Pick from things copied earlier", VirtualKey.LeftWindows, K('v')),

        // A switch, not a shortcut: while on, moving the cursor selects text as it goes
        Make("select", ShortcutGroup.Select, "Select", '\uE7E6', "While on, moving highlights text") with { Kind = ShortcutKind.SelectSwitch },
        Make("select-all", ShortcutGroup.Select, "Select all", '\uE8B3', "Highlight everything", VirtualKey.Control, K('a')),
        Make("word-left", ShortcutGroup.Select, "Word left", '\uE72B', "Move back one word", VirtualKey.Control, VirtualKey.Left),
        Make("word-right", ShortcutGroup.Select, "Word right", '\uE72A', "Move on one word", VirtualKey.Control, VirtualKey.Right),
        Make("delete-word", ShortcutGroup.Select, "Delete word", '\uE750', "Delete the word before the cursor", VirtualKey.Control, VirtualKey.Back),
        Make("top", ShortcutGroup.Select, "Top", '\uE70E', "Go to the start of the document", VirtualKey.Control, VirtualKey.Home),
        Make("bottom", ShortcutGroup.Select, "Bottom", '\uE70D', "Go to the end of the document", VirtualKey.Control, VirtualKey.End),

        Make("bold", ShortcutGroup.Style, "Bold", '\uE8DD', "Bold text", VirtualKey.Control, K('b')),
        Make("italic", ShortcutGroup.Style, "Italic", '\uE8DB', "Sloping text", VirtualKey.Control, K('i')),
        Make("underline", ShortcutGroup.Style, "Underline", '\uE8DC', "Underlined text", VirtualKey.Control, K('u')),
        // Bigger and smaller text work alike in PowerPoint, Word and Publisher
        Make("bigger", ShortcutGroup.Style, "Bigger", '\uE8E8', "Make the text bigger", VirtualKey.Control, VirtualKey.Shift, VirtualKey.OemPeriod),
        Make("smaller", ShortcutGroup.Style, "Smaller", '\uE8E7', "Make the text smaller", VirtualKey.Control, VirtualKey.Shift, VirtualKey.OemComma),
        Make("centre", ShortcutGroup.Style, "Centre", '\uE8E3', "Put the line in the middle", VirtualKey.Control, K('e')),
        Make("align-left", ShortcutGroup.Style, "Left", '\uE8E4', "Line the text up on the left", VirtualKey.Control, K('l')),

        // Windows' emoji picker, for posters and messages
        // HIDra's own emoji keys: Windows' emoji panel takes the controller away
        Make("emoji", ShortcutGroup.Tools, "Emoji", '\uE76E', "Pick an emoji") with { Kind = ShortcutKind.EmojiLayer },
        // Windows 11 Live Captions: words on screen for any sound - videos, calls
        Make("captions", ShortcutGroup.Tools, "Captions", '\uE7F0', "Show words for any sound on the PC", VirtualKey.LeftWindows, VirtualKey.Control, K('l')),
        // Windows voice typing: speak instead of type, into whatever has the cursor
        Make("voice", ShortcutGroup.Tools, "Voice", '\uE720', "Type by speaking", VirtualKey.LeftWindows, K('h')),
        Make("read", ShortcutGroup.Tools, "Read", '\uE767', "Hear the selected words") with { Kind = ShortcutKind.ReadAloud },
        Make("save", ShortcutGroup.Tools, "Save", '\uE74E', "Save the work", VirtualKey.Control, K('s')),
        Make("files", ShortcutGroup.Tools, "Files", '\uE8B7', "Open File Explorer", VirtualKey.LeftWindows, K('e')),
        // Choose an area of the screen (drag with RT), or the whole screen from the bar
        // along the top; it is copied, ready to paste
        Make("snip", ShortcutGroup.Tools, "Snip", '\uE7A8', "Picture of part of the screen, to paste", VirtualKey.LeftWindows, VirtualKey.Shift, K('s')),
        Make("screenshot", ShortcutGroup.Tools, "Screen shot", '\uE722', "Picture of the whole screen, saved in Pictures", VirtualKey.LeftWindows, VirtualKey.Snapshot),
        Make("print", ShortcutGroup.Tools, "Print", '\uE749', "Print the work", VirtualKey.Control, K('p')),
        // Windows Magnifier. Named apart from the Page row's Zoom in, which makes the page
        // itself bigger in the program in front.
        Make("magnify", ShortcutGroup.Tools, "Magnify", '\uE8A3', "Magnify the screen round the pointer; again for more", VirtualKey.LeftWindows, VirtualKey.OemPlus),
        Make("magnify-off", ShortcutGroup.Tools, "Magnify off", '\uE71F', "Close the Magnifier", VirtualKey.LeftWindows, VirtualKey.Escape),
        Make("desktop", ShortcutGroup.Tools, "Desktop", '\uE8FC', "Show the desktop, or bring the windows back", VirtualKey.LeftWindows, K('d')),
    };

    /// <summary>
    /// What a new student starts with. Sound and speech keys other than Voice are left
    /// for staff to add for the students they help; Captions is on for everyone.
    /// </summary>
    public static readonly IReadOnlyList<string> Standard = new[]
    {
        "undo", "redo", "cut", "copy", "paste",
        "select", "select-all", "word-left", "word-right", "delete-word",
        "bold", "italic", "underline", "bigger", "smaller",
        "emoji", "captions", "voice", "save", "snip",
    };

    private static readonly Dictionary<string, Shortcut> ById = All.ToDictionary(s => s.Id);

    public static ShortcutGroup GroupOfRow(int row) => (ShortcutGroup)row;

    public static IEnumerable<Shortcut> InGroup(ShortcutGroup group) => All.Where(s => s.Group == group);

    public static Shortcut? Find(string? id) => id != null && ById.TryGetValue(id, out var s) ? s : null;

    /// <summary>
    /// A student's saved choices as the twenty keys, row by row: null where a slot is
    /// left empty. No choices saved means the standard set; an unknown or misplaced
    /// choice, from an older or edited file, falls back to the standard key for that slot.
    /// </summary>
    public static Shortcut?[] Resolve(IList<string>? ids)
    {
        var keys = new Shortcut?[SlotCount];

        for (int i = 0; i < SlotCount; i++)
        {
            string standard = Standard[i];
            string? chosen = ids != null && i < ids.Count ? ids[i] : standard;

            if (chosen == "")
            {
                keys[i] = null;
            }
            else if (Find(chosen) is { } key && key.Group == GroupOfRow(i / RowLength))
            {
                keys[i] = key;
            }
            else
            {
                keys[i] = Find(standard);
            }
        }

        return keys;
    }

    /// <summary>
    /// The choices as they are saved: an id per slot, "" for an empty one
    /// </summary>
    public static List<string> ToIds(Shortcut?[] keys) => keys.Select(k => k?.Id ?? "").ToList();
}
