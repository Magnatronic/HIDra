# Changelog

HIDra is distributed as a folder that gets copied onto machines and USB sticks, so the
version number is often the only way to tell two copies apart. This file says what
changed between them.

## v1.7.0

A redesign from top to bottom, shaped by watching HIDra used and testing it on a real
controller. The 1.6.0 in the source was never tagged or published, so this release
carries that work too (below).

### The main screen

- **Three tabs: Guide, Practice and Settings**, each a word with an orange bar under
  the page showing. It opens on the Guide.
- **One fixed layout, scaled to fill the screen.** Every page is designed once, 720
  high, and scaled as a whole; its width follows the screen's shape a little, so there
  are no dark bands. It looks the same on a laptop and a big monitor, just bigger, and
  nothing needs scrolling.
- **Guide**: a controller drawing labelled with what every button does, switching
  between "using the pointer" and "typing" as the keyboard opens and closes (or by
  hand, to look ahead). Y swapping the sticks swaps their labels too. Every control
  lights up orange while it is pressed, so pressing a button shows what it does and
  that the controller is working. Beside it, "How do I..." cards for everyday jobs,
  with the buttons drawn in their controller colours. The labels and cards always name
  the buttons this person actually has; a card for a job no button has is left out.
- **Settings** has a list down the left - Buttons, Pointer, Keyboard, Shortcut keys,
  Apps, Phrases, General - and each section gets the whole page.
- **One set of controls everywhere**: a segmented control (one pill, the chosen part
  orange) for every "choose one of a few"; On/Off switches that also say On or Off; and
  steppers with big - and + and a level bar showing where the setting sits. Sliders
  were left out: they need a precise drag. One type scale and 8px spacing throughout,
  and every settings row has one control in the same column.
- **Choosing opens a panel over the page**: a button's job, a shortcut key or a program.
- **Orange is the one accent colour**: an orange border shows where you are, an orange
  fill shows something is on. Warnings such as a low battery are yellow, so they cannot
  be mistaken for it.
- **High contrast** (Settings, General): black and white with thicker borders on the
  main screen, and white-edged black keys with a thicker highlight on the keyboard.
- **Plain words**: Reset and Reset all, and Default, instead of "Back to standard".
- **Scroll bars are wide, with a bright thumb** that turns orange under the pointer.
- **No "HIDra is still running" pop-up** every time the window is closed. The Guide
  says how to bring the window back (hold Back and Start).
- **The icon's controller has a dark outline**, and the logo is sharp at any size.

### Buttons chosen for each person

- **Any button's job can be changed** (Settings, Buttons): click a button on the
  controller drawing and pick from a fixed list, never typed-in key combinations. The
  D-pad directions and the stick presses can each be set on their own. Everyday jobs -
  clicks, the keyboard, Start menu, windows, Undo, Copy, Paste, Escape, Enter and more -
  and **Windows tools**: Voice typing, Live captions, the Magnifier on and off, Slow
  pointer, Emoji, Snip, Clipboard history, Find, Save, File Explorer, Show desktop,
  Back a page, Page up and down, and the volume.
- **LT can have any job too.** New settings start on Escape. While the keyboard is open
  LT always moves it.
- **What cannot be lost, is not.** Holding Back and Start together always brings HIDra
  back, whatever they are given. The last button that clicks, and the last that opens
  the keyboard, cannot be changed until another button has the job. A settings file that
  breaks these rules, edited by hand or imported, goes back to the defaults.
- Buttons changed from their default are listed under the drawing; Reset puts one back,
  Reset all puts them all back.
- **Export and import settings** (Settings, General), to give someone else the same
  starting point. Windows' own keyboard setting, which belongs to the PC, is not copied.

### The keyboard

- **Laid out like the keyboards made for controllers** (Xbox and Windows gamepad
  keyboards, and phone keyboards for symbols and emoji): every key the same size except
  Space, Backspace and Enter, the letters in QWERTY order in straight columns so up and
  down always go straight, and the arrows as an inverted T with Home and End beside
  them.
- **Numbers and symbols are on a second layer**, behind 123 #+, as on a phone. Each
  symbol appears once, the most used first; the rarer ones sit small above a key and
  come from B. The keyboard always opens on the letters.
- **Emoji in colour**: an emoji key beside 123 shows the 30 most used emoji (by
  Unicode's frequency figures), drawn from Microsoft's Fluent Emoji (MIT licence).
  Windows' own emoji panel took the controller away, so HIDra has its own.
- **More buttons type while the keyboard is open**, as on Windows' gamepad keyboard:
  LB, RB and Y type Backspace, Space and Enter from anywhere on the board (the keys say
  so); D-pad left and right move the text cursor; pressing the left stick in swaps to
  numbers and symbols; Start is Caps Lock and Back is Escape. Their usual jobs come back
  when the keyboard closes. Whichever button opens the keyboard keeps closing it.
- **The right stick jumps around the keyboard**: the first push goes to the edge of the
  part the highlight is in, the next jumps to the next part - up to the word row, right
  to the shortcuts, back to the letters.
- **Move the keyboard out of the way**: tap LT to flip it between the top and bottom of
  the screen, or hold LT and push the left stick to drag it anywhere.
- **No close key.** The button that opens the keyboard closes it; the old red key was
  one overshoot from ending typing by mistake.
- **Shortcut panel in labelled rows**, each its own colour: Edit, Select, Sound and
  video (Play, Quieter, Louder, Mute, Captions) and Tools (Voice, Save, Snip, Find,
  Print), and a bottom row for the program in front - Slides, Word, Web, Folders or
  Page - with Bold, Italic and text size in Word and PowerPoint.
- **Shortcut keys chosen per person** (Settings, Shortcut keys): click a place in the
  rows and pick from that row's list, or leave it empty. Read aloud, Clipboard history,
  Top and Bottom, Centre and Left, Magnify, Desktop and more can be added.
- **Read aloud**: a Read key reads the selected text in any program with the voices
  built into Windows. Press it again to stop. Whatever was copied is kept.
- **The Apps key opens any program on the Start menu**, Store apps such as the new
  Outlook included, with a Find box. It starts with Edge, Word, PowerPoint, Outlook,
  File Explorer and Teams, whichever are installed.
- **Phrases can be typed with HIDra's keyboard**: opening it no longer takes the cursor
  out of the phrase boxes.

### The pointer

- **Smooth out wobbles** (Settings, Pointer): the sticks are averaged a little, more or
  most, so a shaky hand does not make a shaky pointer. Letting go settles faster than
  pushing builds up, so stopping on a target stays prompt.
- **Ignore quick repeat presses**: a press soon after letting go of the same button is
  ignored - a tremor or a bounce, not a decision. Off by default.
- **Pointer feel: Steady or Gentle.** Gentle is slower for a small push and faster for
  a big one, for creeping onto a target and then crossing the screen quickly.
- **Slow pointer**, on any button, switched on and off at a chosen share of normal
  speed - the same for both sticks.

### Practice

- **Two repeatable tests**, the same every time, so each run compares with the last:
  - **Pointer test**: twelve circles in fixed places, some further down or up the page
    so reaching them means scrolling too. Time, misses, overshoots and a speed score
    (Fitts's law, as in ISO 9241-9) are kept. A good run makes the circles smaller next
    time; a bad one makes them bigger.
  - **Typing test**: words, phrases or sentences. The box moves between the top and
    bottom of the page, so the keyboard is sometimes in the way and has to be moved.
    Letters a minute, wrong letters and corrections are kept; a clean run suggests the
    next level.
- Every run is kept in `practice.json` beside the person's settings, with the latest
  runs and the best so far shown beside the tests. **Save as a spreadsheet...** writes
  them all out for a review. Exporting settings does not copy them.
- Every change is shown on screen, never only as a sound.

## v1.6.0 (never released)

Included in v1.7.0 above.

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
