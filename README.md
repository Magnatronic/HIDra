# HIDra - Controller Accessibility Tool

Control Windows with an Xbox controller. Simple, reliable, zero-config.

## Features
- Mouse with Left Stick (slow by default; speed is set on the main screen and remembered per student)
- Scroll with Right Stick
- DPad window management: Up = Maximize, Down = Minimize, Left/Right = Snap
- X toggles onscreen keyboard (UK layout, large keys in straight columns, numbers and symbols on a second layer behind 123 #+); left stick moves the highlight, A types, B gives the shifted symbol or capital - no aiming needed
- Six word suggestions on the keyboard, from the prediction engine built into Windows - nothing extra to install
- Dwell: click by resting the cursor (a ring by the cursor fills as the click approaches), and type by resting the keyboard highlight (both optional)
- Keyboard extras: automatic capitals, the student's own phrases on a Phrases key, and an adjustable size
- A shortcut panel on the keyboard, with icons, in labelled rows - Edit (Undo, Copy, Paste...), Select (a Select switch for highlighting text, word moves), Sound (Play, Quieter, Louder, Mute, Captions), Tools (voice typing, Save, Snip, Find, Print) - and a row that follows the program in front (PowerPoint and Word with Bold and other formatting, web browser, File Explorer); it can be switched off
- The keyboard's letters are in QWERTY order in straight columns, so moving the highlight up or down always goes straight
- The most used emoji, in colour (Microsoft Fluent Emoji, MIT licence), on the keyboard's 123 #+ layer
- A High contrast option: black and white, with thicker borders and highlight, on the keyboard and the main screen
- The Apps key starts with Edge, Word, PowerPoint, Outlook (classic or new), File Explorer and Teams, whichever are installed
- Shortcut keys and the Apps key's programs are chosen per student in Settings, from a fixed list of safe keys (Read aloud, Print, Magnify, Clipboard history, Find...) and any program on the Start menu, Store apps such as the new Outlook included, with a Find box; a place can be left empty
- While the keyboard is open, LB types Backspace, RB Space and Y Enter, from wherever the highlight is; D-pad left and right move the text cursor, pressing the left stick in swaps to numbers and symbols, Start is Caps Lock and Back is Escape
- Read aloud: a Read key (added per student) reads the selected text in any program, with the voices built into Windows
- For unsteady hands: stick smoothing, ignoring a repeat press that comes too soon after letting go, and a Gentle pointer curve (slower for a small push, faster for a big one)
- Settings can be exported from one student and imported for another, as a starting point
- Windows' own tools can go on a button: voice typing, live captions, the Magnifier (on and off), HIDra's emoji keys, snip, clipboard history, find, save, File Explorer, show desktop, back a page, page up and down, and the volume
- Button jobs chosen per student in Settings, Buttons, by clicking the button on a drawing of the controller and picking from a fixed list; Back + Start held together, a button that clicks and a button that opens the keyboard can never be lost, and the Guide always names the buttons this student has
- Practice tests that measure progress: a pointer test (twelve circles in fixed places, some needing a scroll to reach, shrinking as accuracy improves; time, misses, overshoots and a Fitts's-law speed score) and a typing test (words, phrases or sentences, the box sometimes where the keyboard covers it; letters a minute, wrong letters and corrections). Every run is kept per student and can be saved as a spreadsheet for reviews
- While the keyboard is open, the right stick jumps between its parts: up to the word row, right to the shortcuts, back to the letters
- An Apps key that opens PowerPoint, Word, Edge and other installed programs in one press
- One main screen with three tabs: **Guide** (what opens) - a controller drawing labelled with what every button does, switching between "using the pointer" and "typing" as the keyboard opens and closes, each button lighting up orange as it is pressed, and "How do I..." for everyday tasks; **Practice** - the pointer and typing tests and a record of every run; and **Settings**, with a list down the left: Buttons, Pointer, Keyboard, Shortcut keys, Apps, Phrases and General. The screen is one fixed layout scaled to fill any screen, so it looks the same on every PC
- HIDra's keyboard has its own emoji keys, reached from the Emoji shortcut key or an Emoji button job
- LT moves the keyboard while it is open, so it never has to cover your work: a tap flips it between the top and bottom of the screen, and holding LT while pushing the left stick drags it anywhere. While it is closed, LT does a job chosen per student: Escape (the default), any other button job, zoom in and out with Windows Magnifier, or a slow pointer switched on and off
- Y swaps cursor/scroll sticks
- RB double-click; LB opens the window switcher (A confirms, B cancels)
- Start opens Task View; Back opens Start menu
- No configuration files - sensible defaults are baked in; every setting on the main screen is remembered per student (see *Where settings are saved*)
- Stops Windows' own gamepad keyboard popping up and taking over the controller (optional, on by default; puts the student's own Windows setting back if turned off)

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

## Where settings are saved
HIDra is often run from a network share at logon, so each student's settings are kept
somewhere that follows the student, not the PC. The first of these that can be written
to is used, and the main screen shows which:

1. The folder named in `HIDra-settings-folder.txt`, if that file sits beside `HIDra.UI.exe`.
   One line, environment variables allowed - for example `H:\HIDra` or
   `\\server\hidra-settings\%USERNAME%`. Lines starting with `#` are ignored.
2. The student's network home drive, if Windows reports one: `%HOMESHARE%\HIDra`.
3. `%APPDATA%\HIDra` - which also follows the student where the college uses roaming
   profiles or folder redirection.

The student's practice is kept in `practice.json` in the same folder. It is not part of
an exported settings file.

Settings found in `%APPDATA%\HIDra` are carried over the first time a better location
is used, so moving them never loses anything.

## Getting Started
1) Plug in an Xbox controller (USB or Bluetooth)
2) Launch the app
3) It autoconnects and you're ready to go

## Button Mapping
The default jobs. Any button except the sticks and RT can be given a different job
for a student, in Settings, Buttons.

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
release\HIDra-v1.6.0\
  README-FIRST.txt     which one to use, for whoever installs it
  Network-share\       6 files  - for college PCs running HIDra from a share (recommended)
  USB-portable\        ~480 files - for a USB stick or a PC where nothing can be installed
  Needs-dotNET-10\     1 file   - smallest; only where the .NET 10 Desktop Runtime is installed
```

Each has its own `QUICK-GUIDE.txt`. All three are the same program; they differ only in
how it is put onto a machine.

**Why no single self-contained exe.** A normal single-file build unpacks WPF's native
libraries to `%TEMP%\.net` on every launch, and managed environments routinely block
running anything from user-writable paths - so it would fail on exactly the locked-down
college machines HIDra is for. *Network-share* bundles all the managed code into the exe
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
5) Change the cursor speed on the main screen, restart HIDra, and check it was remembered

## License
MIT - see `LICENSE`.
