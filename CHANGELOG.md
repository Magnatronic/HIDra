# Changelog

HIDra is distributed as a folder that gets copied onto machines and USB sticks, so the
version number is often the only way to tell two copies apart. This file says what
changed between them.

## Unreleased

### Orange, and fewer trips across the keyboard

- **Orange is the one accent colour** - the student's favourite. An orange border shows
  where you are (the keyboard highlight, which was yellow, and the setting under the
  pointer); an orange fill shows something is on (toggles, Shift, Ctrl, Caps, Select,
  which were a mix of green and blue). Warnings such as a low battery are now yellow,
  so they cannot be mistaken for it.
- **LB, RB and Y type Backspace, Space and Enter** while the keyboard is open, from
  wherever the highlight is. After letters these are the keys used most, and each sat
  at the edge of the board. The keys show LB, RB and Y on them. Their usual jobs come
  back when the keyboard closes.
- **The shortcut panel is in labelled rows**: Edit, Select, Style and Tools, each its
  own colour, and a bottom row named for the program in front (Slides, Word, Web,
  Folders). The keys used to run in no particular order. Underline is new; the
  whole-screen screenshot key is gone, as Snip does the same job and puts the picture
  where it can be pasted.
- **The keyboard is laid out like a keyboard.** Each row starts further in than the
  one above, as on any real keyboard, instead of fourteen equal keys stacked in a grid.
  Space, Enter and Backspace are the biggest keys, since after letters they are typed
  most.
- **The main screen is a guide as well as the settings.** It opens maximised, with:
  - a controller drawing labelled with what every button does. A switch shows the
    buttons "using the pointer" or "typing"; it follows the keyboard opening and
    closing, and staff can flip it to look ahead. Y swapping the sticks swaps their
    labels too.
  - every control lighting up orange, with its label, while it is pressed - so the
    student can press a button to find out what it does, and staff can see at once
    that the controller is working.
  - each setting on the label of the button it changes: pointer speed on the left
    stick, scroll speed on the right, click by resting on A, keyboard position on LT.
    Keyboard settings, phrases and the Windows options run down the right.
  - "How do I...": click, type, type faster, numbers and symbols, open a program,
    switch programs, copy and paste, move the keyboard - a line or two each, with the
    buttons drawn in their controller colours. On a screen too short for everything
    at once, a "How do I..." button swaps it with the drawing.
- **Numbers and symbols are on a second layer**, behind a 123 #+ key, as on a phone.
  The number row and the symbols typed least ([ ] \ ; # = and Caps, Win, PgUp, PgDn)
  took a full key each on the way to the letters. They now share the letters' places,
  and every key round the edge stays where it is on both layers. ! @ £ & ( ) : " and
  € have keys of their own there. The keyboard always opens on the letters. With the
  number row gone the word suggestions are a step nearer, and the keyboard is shorter.

## v1.6.0

### Keyboard that gets out of the way, and a slower start

From watching a student type into PowerPoint: the cursor was far too fast at first, and
the keyboard sat over the very text she was typing, with no way to move it.

- **The cursor starts slow (15%) and remembers its speed.** It previously started at
  50% and forgot any change on restart. Speeds are shown as percentages, from 5% to
  100%, in 1% steps below 20% where a small change matters most, then 5% and 10%.
- **Settings are remembered per student** in `%APPDATA%\HIDra\settings.json`: cursor
  speed, keyboard position and the Grid 3 option. A missing or damaged file falls back
  to the defaults.
- **The Left Trigger moves the keyboard** between the top and bottom of the screen, and
  it opens where it was last left. Precision mode is gone from the Left Trigger: it
  could not be held while steering the left stick, which is when it was needed.
- **Word suggestions.** A green row across the top of the keyboard offers six words:
  completions of the word being typed, or the likely next word after a space. A types
  the rest of the word and a space; B does the same with a capital. They come from the
  prediction engine built into Windows, so there is nothing to install - which matters
  on college machines. The keyboard cannot see the document, so suggestions start
  fresh after arrows, Home, End, Tab or a Ctrl shortcut rather than guessing.
- **Dwell, for anyone who finds pressing a button hard.** *Click by resting* clicks once
  when the cursor stops moving, and not again until it moves. *Type by resting* types the
  key the keyboard highlight rests on; a ring around the key's letter fills first, the
  same as the ring by the cursor, and moving on cancels it. Steering across the keyboard
  does not flash a ring on every key passed - it appears once the highlight settles. Both are off by default, with the time adjustable. Pressing a
  button yourself always cancels a pending dwell, so nothing happens twice.
- **A countdown ring for dwell clicks.** A small ring beside the cursor fills while a
  dwell click counts down, so a click is never a surprise; moving on cancels it. It
  appears only once the cursor has settled for a moment, and a wobble of a few pixels
  still counts as resting, so an unsteady hand neither makes it flicker nor cancels it.
- **Automatic capitals.** The first letter of a sentence, and "I" (including I'm, I'll),
  get a capital without Shift or B. The letter keys show capitals when one is due.
  Pressing B first gives a small letter instead. On by default.
- **Phrases.** Staff type up to six of the student's own phrases on the main screen -
  a name, an email address, sentences used often. The Phrases key at the start of the
  suggestion row swaps the suggestions for them, and one press types the whole phrase.
- **Keyboard size**, from 80% to 150%, never wider than the screen.
- **A shortcut panel beside Backspace and Enter.** Whole actions as one key, one step
  from where the highlight usually is, instead of a trip to Ctrl and back: Undo, Redo,
  voice typing (Windows' dictation), Save, File Explorer, Cut, Copy, Paste, Select all,
  a whole-screen screenshot, Snip, word-at-a-time moving and deleting, Bold, Italic,
  emoji and bigger/smaller text. The bottom row follows the program in front:
  PowerPoint (new slide, slideshow...), Word (heading, bullets...), a web browser
  (back, new tab, find...), File Explorer (up a folder, new folder, rename...).
  Each key shows an icon above its word - the same icons as PowerPoint's and Word's
  own buttons - since the student uses AAC and finds pictures quicker than words.
  The panel can be switched off on the main screen for a student who finds it too
  much. The suggestion row now runs the full width.
- **Selecting text.** A Select key works as a switch: while it is on (green), the
  arrows, Home, End and the word keys highlight text as they move, instead of needing
  Shift held down. Typing, Delete, Cut, Copy or Bold then use the selection and switch
  it off. B on a movement key selects just that one step.
- **Opening programs in one press.** An Apps key swaps the suggestion row for the
  programs installed on the PC - PowerPoint, Word, Publisher, Edge, Chrome, File
  Explorer - each with its own logo. Reaching them through the Start menu with a
  controller takes a long string of movements.
- **The keyboard can fade when resting.** If it covers text and moving it is not enough,
  it can fade after a few seconds without controller input, so the text behind shows
  through; any stick or button brings it straight back. It never fades below a visible
  minimum or during a dwell countdown, and while faded clicks pass through it to the text
  underneath. Off by default; the delay and how faint it goes are on the main screen.
  The keyboard no longer has a Windows title bar, which is what allows it to fade.
- **Settings follow the student across PCs.** HIDra is usually run from a network share
  at logon, so settings go to the student's home drive when there is one, or wherever
  IT points them with `HIDra-settings-folder.txt`, falling back to AppData. The main
  screen shows where they are saved.
- **One main screen.** The Help and Settings windows are gone. The main screen shows a
  controller with every button labelled, and every setting beside it with large buttons
  that are easy to hit with the controller's own pointer. Changes apply and save at once.
- **Windows' own keyboard is kept out of the way.** With a controller connected, Windows
  opens its gamepad keyboard whenever a text box gets focus - in the Start menu search,
  for example - and it takes over the controller. HIDra switches that off for the
  student (an option, on by default) and puts their own setting back if it is turned off.
- **HIDra has its own icon** - a controller on its side, forming a B - for the program,
  the window and the notification area.

The Windows On-Screen Keyboard was tried as the main keyboard and dropped: Windows
protects it from input sent by other programs, so HIDra could open it but not type on
it, click it or drag it.

## v1.5.0

Everything below v1.3.0 shipped without a release of its own. The 1.4.0 in the source
was never tagged or published, so this release carries that work as well as the typing
rework that followed it.

### Typing without aiming

The virtual keyboard no longer asks the user to steer a cursor onto every key, which
was the slowest thing in the tool and the least forgiving of an unsteady hand.

- **The left stick moves a highlight between keys, and A types the highlighted one.**
  Hold a direction to travel quickly. The D-pad works too, for anyone who prefers it.
  While the keyboard is open the left stick drives the highlight instead of the cursor;
  hold the Right Trigger if you need to click something.
- **B types the shifted character** - the symbol printed above the key, or the capital.
  Shift costs two journeys across the keyboard and back; B costs none. Shift still
  works for anyone who prefers it, and still clears after one key.
- **Home, End, PgUp, PgDn, Del and Win** are on the board. Home and End matter most:
  without them, reaching the start of a line means one arrow press per character, and
  every one of those is a deliberate movement.
- **The layout reads the way keyboards read.** Tab, Caps, Shift and Ctrl run down the
  left edge in the usual order; Backspace, Del and Enter share the right; the space bar
  is a sensible width rather than swallowing eleven of fourteen columns.
- **Close is out of the letter grid.** It sat one step from the arrow keys, where an
  overshoot ended the typing session. It is now red, in the top left corner, on its own.
- Keys look like what they do: typing keys dark with a large glyph, function keys
  lighter and smaller, close red.

### Cursor

- **Smooth movement.** Absolute positioning, frame timing and sub-pixel steps replace
  the old accumulate-and-jump approach.
- **No more stuttering.** Timer resolution is raised while polling, so the cursor keeps
  up with the stick instead of moving in visible steps.

### Reliability

- **Only one copy runs at a time.** Three copies were once found running together, each
  injecting its own input, so every stick movement was applied three times. A second
  launch now shows the running window instead of starting a rival.
- **An impatient second click during startup is no longer lost.**
- **Stop is a pause the controller can undo**, rather than a state needing a mouse.
- **The Reconnect button is gone.** It could never be clicked after a normal start, and
  recovery must never depend on a mouse click - the person who needs it is the person
  who cannot click. Reconnection was already automatic.
- **Window snapping fixed.**
- **A connected controller that is not in XInput mode now says so**, instead of looking
  like a controller that simply does not work.

### Under the hood

- The last unmaintained dependency is gone.
- Ctrl and the UK layout fixed; swapping sticks now gives feedback.

## v1.4.0 (never released)

Never-give-up reconnection, battery warning, tray recovery. Included in v1.5.0 above.

## v1.3.0

- RB performs a double-click, for users who struggle with double-clicking A
- Grid 3 auto-suspend: HIDra pauses by itself while Grid 3 is running
- A settings toggle to enable or disable Grid 3 detection

## v1.2.0

- Grid 3 auto-detection

## v1.1

## v1.0

Initial release.
