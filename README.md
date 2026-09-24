# HIDra - Controller Accessibility Tool

Control Windows with an Xbox controller. Simple, reliable, zero-config.

## Features
- Mouse with Left Stick (slow by default; speed is set in Settings, Pointer and remembered per person)
- Scroll with Right Stick
- DPad window management: Up = Maximize, Down = Minimize, Left/Right = Snap
- X toggles onscreen keyboard (UK layout, large keys in straight columns, numbers and symbols on a second layer behind 123 #+); left stick moves the highlight, A types, B gives the shifted symbol or capital - no aiming needed
- Six word suggestions on the keyboard, from the prediction engine built into Windows - nothing extra to install
- Dwell: click by resting the cursor (a ring by the cursor fills as the click approaches), and type by resting the keyboard highlight (both optional)
- Keyboard extras: automatic capitals, your own phrases on a Phrases key, and an adjustable size
- A shortcut panel on the keyboard, with icons, in labelled rows - Edit (Undo, Copy, Paste...), Select (a Select switch for highlighting text, word moves), Sound (Play, Quieter, Louder, Mute, Captions), Tools (voice typing, Save, Snip, Find, Print) - and a row that follows the program in front (PowerPoint and Word with Bold and other formatting, web browser, File Explorer); it can be switched off
- The keyboard is laid out like controller keyboards: equal-size keys with the letters in QWERTY order in straight columns (so up and down always go straight), the arrows as an inverted T, each symbol once on the 123 #+ layer, and an emoji key beside 123
- The most used emoji, in colour (Microsoft Fluent Emoji, MIT licence), from the emoji key
- A High contrast option: black and white, with thicker borders and highlight, on the keyboard and the main screen
- The Apps key starts with Edge, Word, PowerPoint, Outlook (classic or new), File Explorer and Teams, whichever are installed
- Shortcut keys and the Apps key's programs are chosen per person in Settings, from a fixed list of safe keys (Read aloud, Print, Magnify, Clipboard history, Find...) and any program on the Start menu, Store apps such as the new Outlook included, with a Find box; a place can be left empty
- While the keyboard is open, LB types Backspace, RB Space and Y Enter, from wherever the highlight is; D-pad left and right move the text cursor, pressing the left stick in swaps to numbers and symbols, Start is Caps Lock and Back is Escape
- Read aloud: a Read key (added per person) reads the selected text in any program, with the voices built into Windows
- For unsteady hands: stick smoothing, ignoring a repeat press that comes too soon after letting go, and a Gentle pointer curve (slower for a small push, faster for a big one)
- Settings can be exported from one person and imported for another, as a starting point
- Windows' own tools can go on a button: voice typing, live captions, the Magnifier (on and off), HIDra's emoji keys, snip, clipboard history, find, save, File Explorer, show desktop, back a page, page up and down, and the volume
- Button jobs chosen per person in Settings, Buttons, by clicking the button on a drawing of the controller and picking from a fixed list; Back + Start held together, a button that clicks and a button that opens the keyboard can never be lost, and the Guide always names the buttons this person has
- Practice tests that measure progress: a pointer test (twelve circles in fixed places, some needing a scroll to reach, shrinking as accuracy improves; time, misses, overshoots and a Fitts's-law speed score) and a typing test (words, phrases or sentences, the box sometimes where the keyboard covers it; letters a minute, wrong letters and corrections). Every run is kept per person and can be saved as a spreadsheet for reviews
- While the keyboard is open, the right stick jumps between its parts: up to the word row, right to the shortcuts, back to the letters
- One main screen with three tabs: **Guide** (what opens) - a controller drawing labelled with what every button does, switching between "using the pointer" and "typing" as the keyboard opens and closes, each button lighting up orange as it is pressed, and "How do I..." for everyday tasks; **Practice** - the pointer and typing tests and a record of every run; and **Settings**, with a list down the left: Buttons, Pointer, Keyboard, Shortcut keys, Apps, Phrases and General. The screen is one fixed layout scaled to fill any screen, so it looks the same on every PC
- LT moves the keyboard while it is open, so it never has to cover your work: a tap flips it between the top and bottom of the screen, and holding LT while pushing the left stick drags it anywhere. While it is closed, LT does a job chosen per person, from the same list as the other buttons: Escape by default
- Y swaps cursor/scroll sticks
- RB double-click; LB opens the window switcher (LB again moves along, A switches, B cancels)
- Start opens Task View; Back opens Start menu
- No configuration files - sensible defaults are baked in; every setting on the main screen is remembered per person (see *Where settings are saved*)
- Stops Windows' own gamepad keyboard popping up and taking over the controller (optional, on by default; puts the person's own Windows setting back if turned off)

## Reliability
HIDra is intended to be the only way its user can operate the computer, so it is built
not to leave them stranded:

- **Automatic reconnection.** If the controller disconnects - flat battery, Bluetooth
  dropout, knocked cable - HIDra keeps searching and reattaches on its own. It never
  needs a mouse click to recover.
- **Starts before the controller does.** Launching at logon with nothing plugged in is
  a normal state; HIDra waits and connects as soon as a controller appears.
- **Battery warning.** The controller's battery level is shown in the window and in the
  notification area, with a warning while there is still time to act on it.
- **Closing hides, it does not quit.** The window closes to the notification area and
  the controller keeps working. Hold **Back + Start together for one second** to bring
  the window back, or use the notification-area icon. Exit properly from that icon.
- **Nothing is left held down.** If the controller vanishes mid-action, any held keys
  or mouse buttons are released, so a stuck Alt key cannot lock up the machine.
- **Settings cannot be half-saved.** Each save is written in full before it replaces
  the last one, which is kept as a backup - so logging off or losing the network mid-save
  never leaves someone with their settings gone.
- **A slow network does not hold it up.** A home drive that does not answer within 5
  seconds is passed over for the next place to save.
- **Unexpected errors are logged, not fatal.** HIDra carries on where it can, and writes
  what happened to `HIDra-errors.log` beside the settings, so there is something to look
  at afterwards.

## Where settings are saved
HIDra is often run from a network share at logon, so each person's settings are kept
somewhere that follows them, not the PC. When HIDra starts it uses the first of these
it can write to:

1. **A folder you choose**, named in `HIDra-settings-folder.txt` (see below).
2. **Their network home drive**, if their account has one: `%HOMESHARE%\HIDra`.
   `%HOMESHARE%` is set by Windows at logon to the full network path of the home
   folder - the same place as the home drive letter (such as `H:`), reached without
   depending on the letter being connected yet.
3. **`%APPDATA%\HIDra` on the PC**, which only follows the person where the network
   uses roaming profiles or folder redirection.

A place that cannot be written to, or does not answer within 5 seconds, is skipped
without any message, so a settings problem never stops HIDra starting. To see which was
used, open **Settings, General**: the Settings file card says "Saved in ...".

### Choosing the folder: HIDra-settings-folder.txt
Only needed if the home drive is not the right place, or people have no home drive.

1. Create a plain text file named exactly `HIDra-settings-folder.txt`. Windows hides
   file extensions by default, so check it has not become
   `HIDra-settings-folder.txt.txt` (turn on View, File name extensions to see).
2. Put it in the same folder as the HIDra program file (`HIDra.UI.exe`, or whatever
   it has been renamed to).
3. Write the folder on the first line. Environment variables are filled in, so
   `%USERNAME%` gives each person a folder of their own:

   ```
   # Where HIDra keeps each person's settings
   \\server\hidra-settings\%USERNAME%
   ```

   Lines starting with `#` and blank lines are ignored; the first other line is used.
   A drive letter works too (`H:\HIDra`), if every account has it.
4. Make sure every person can **create and change files** there. HIDra creates their
   folder the first time, if they are allowed to create folders in the one above it.
5. Log on as one of them, start HIDra, and check Settings, General shows the folder.

Always include `%USERNAME%` (or another per-person part) in a shared location.
Without it, everyone would share one settings file.

### What is in the folder
- `settings.json` - their settings, saved as each one changes
- `practice.json` - their Practice runs; not part of an exported settings file
- `settings.json.bak`, `practice.json.bak` - the previous save of each. A save is
  written in full before it replaces the old file, so one cut off part way (at
  logoff, or a network drop) never leaves a broken file; if the main file cannot be
  read, the backup is used
- `HIDra-startup.log` - how HIDra's last start went, step by step (see below)
- `HIDra-errors.log` - only if something went wrong that HIDra did not expect; worth
  sending with any report of a problem
- `HIDra-check.txt` - the last report from `--check` (see below)

Settings found in `%APPDATA%\HIDra` are carried over the first time a better location
is used, so moving them never loses anything.

## When HIDra does not start
**1. Did it run at all?** Look at `HIDra-startup.log` in the person's settings folder
(see above). HIDra rewrites it every time it starts, one line per step:

```
2026-01-12 09:01:14  +  1695 ms  HIDra 1.7.0.0 starting
2026-01-12 09:01:14  +  1697 ms  Program: \\server\apps\HIDra\HIDra.UI.exe
2026-01-12 09:01:14  +  1707 ms  Settings folder: \\server\home\someone\HIDra (Home drive (%HOMESHARE%), 40 ms)
2026-01-12 09:01:15  +  2753 ms  Window created
2026-01-12 09:01:15  +  3059 ms  Waiting for a controller
2026-01-12 09:01:15  +  3093 ms  Window shown
```

- **Its time has not changed since they logged on:** HIDra never ran. Either nothing
  started it, or Windows refused to. Double-click the program file itself. If Windows
  says it "cannot access the specified device, path, or file", it is blocking the file:
  - Files downloaded from the internet carry a mark that unzipping and copying keep,
    and managed PCs often refuse to run them. Someone who can change the program folder
    clears it, in PowerShell:
    `Get-ChildItem "<program folder>" -Recurse | Unblock-File`
  - Or the person's account cannot read and run the file (check with `icacls`), or the
    PC only runs approved programs (Event Viewer, Applications and Services Logs,
    Microsoft, Windows, AppLocker).
- **It stops part way:** the last line shows how far HIDra got, and
  `HIDra-errors.log` beside it says what went wrong.

**2. Run the check.** Start the program with `--check`, from Command Prompt:

```
"\\server\apps\HIDra\HIDra.UI.exe" --check
```

or from a copy of the shortcut with ` --check` added after the path in Target. Instead
of HIDra it opens a report of what it can see:
- the program file, and whether it carries the internet mark;
- `%HOMESHARE%`, `HIDra-settings-folder.txt`, and each place settings could go, tried
  now;
- the files in the settings folder;
- the controller;
- the last start-up and any errors.

**Copy** puts it on the clipboard to send on; it is also saved as `HIDra-check.txt` in
the settings folder. It does not use the controller or stop a running HIDra, and it is
not on HIDra's own screens. Starting a program from Command Prompt skips the
internet-mark check that double-clicking goes through, so `--check` often runs even
when double-clicking does not - which itself points at the mark.

## Getting Started
1) Plug in an Xbox controller (USB or Bluetooth)
2) Launch the app
3) It autoconnects and you're ready to go

## Button Mapping
The default jobs. Any button except the sticks and RT can be given a different job
for each person, in Settings, Buttons.

- A: Left click
- B: Right click
- X: Toggle virtual keyboard
- Y: Swap stick modes (cursor ↔ scroll)
- RB: Double click
- LB: Previous app (Alt+Shift+Tab)
- Back: Windows key, Start: Win+Tab
- DPad: Up = Maximize, Down = Minimize, Left = Win+Left, Right = Win+Right
- LT: Escape (move keyboard top/bottom while it is open), RT: Click & hold (drag)
- Left/Right Stick Click: Undo (Ctrl+Z)

## System Requirements
- **Windows 10/11** (x64)
- **Xbox One/Series controller** (USB or Bluetooth)
- Nothing else, unless you choose the *Needs-dotNET-10* build below

## Building a release
Needs the .NET 10 SDK (`winget install --id Microsoft.DotNet.SDK.10`). Then run:

```
build.bat
```

Everything lands in one folder, named with the version from `HIDra.UI.csproj`:

```
release\HIDra-v1.7.1\
  README-FIRST.txt     which one to use, for whoever installs it
  Network-share\       the exe and 5 DLLs - for shared or managed PCs running HIDra from a share (recommended)
  USB-portable\        ~480 files - for a USB stick or a PC where nothing can be installed
  Needs-dotNET-10\     the exe only - smallest; only where the .NET 10 Desktop Runtime is installed
```

Each also has its own `QUICK-GUIDE.txt`, and `THIRD-PARTY-NOTICES.txt` crediting the emoji
and libraries HIDra includes.
All three are the same program; they differ only in how it is put onto a machine.

**Why no single self-contained exe.** A normal single-file build unpacks WPF's native
libraries to `%TEMP%\.net` on every launch, and managed environments routinely block
running anything from user-writable paths - so it would fail on exactly the locked-down
machines HIDra is often used on. *Network-share* bundles all the managed code into the exe
but leaves those five native DLLs beside it, so nothing is unpacked: one network read
instead of ~480, and `%TEMP%\.net` is never touched (checked, not assumed). Keep the six
files together. *Needs-dotNET-10* is a true single file safely, because everything it
carries is managed.

For day-to-day development, `dotnet run --project src/HIDra.UI` runs it straight from source.

## Smoke Test
1) Run `HIDra.UI.exe` from one of the release folders
2) Confirm the window title reads: "HIDra - Controller to Mouse/Keyboard"
3) Move the cursor with the Left Stick; scroll with the Right Stick
4) Press X to open the keyboard; push up to the green row and check word suggestions appear
5) Change the pointer speed in Settings, Pointer, restart HIDra, and check it was remembered

## License
MIT - see `LICENSE`.
