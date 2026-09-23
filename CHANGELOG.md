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
- **The main screen has two tabs: Guide and Settings.** It opens maximised, on the Guide.
  - **Guide**, for the student: a controller drawing labelled with what every button
    does. A switch shows the buttons "using the pointer" or "typing"; it follows the
    keyboard opening and closing, and staff can flip it to look ahead. Y swapping the
    sticks swaps their labels too. Every control lights up orange, with its label,
    while it is pressed - so the student can press a button to find out what it does,
    and staff can see at once that the controller is working. Beside it, "How do I...":
    click, type, type faster, numbers and symbols, open a program, switch programs,
    copy and paste, move the keyboard - a line or two each, with the buttons drawn in
    their controller colours.
  - **Settings**, for staff: every setting, grouped into Pointer, Keyboard, Phrases and
    Windows, each saying which button it goes with.
  - Whether the controller is connected, the battery, and Pause stay above both.
- **Numbers and symbols are on a second layer**, behind a 123 #+ key, as on a phone.
  The number row and the symbols typed least ([ ] \ ; # = and Caps, Win, PgUp, PgDn)
  took a full key each on the way to the letters. They now share the letters' places,
  and every key round the edge stays where it is on both layers. ! @ £ & ( ) : " and
  € have keys of their own there. The keyboard always opens on the letters. With the
  number row gone the word suggestions are a step nearer, and the keyboard is shorter.

### Steadier pointer, reading aloud, and trying settings out

- **Smooth out wobbles** (Settings, Pointer): the sticks are averaged over a short time,
  a little, more or most, so a shaky hand does not make a shaky pointer. Letting go
  settles three times as fast as pushing builds up, so stopping on a target stays
  prompt. It steadies the keyboard highlight and scrolling as well.
- **Ignore quick repeat presses**: a press that starts within 0.2 to 0.75 seconds of
  letting go of the same button is ignored until it is released - a tremor or a bounce,
  not a decision. Off by default.
- **Pointer feel: Steady or Gentle.** Steady is the slow, even speed there has always
  been. Gentle is slower for a small push and faster for a big one - for creeping onto a
  target and then crossing the screen quickly. Half a push, and full, are the same speed
  either way.
- **Read aloud**: a Read key in the keyboard's Tools row reads the selected text in any
  program, with the voices built into Windows. Press it again to stop. Whatever was on
  the clipboard is put back afterwards. It takes the place of Files, which Apps opens.
- **Four tabs.** Guide and Practice for the student; Controller and Keyboard, set apart
  under "Settings", for staff. **Practice** has circles of three sizes to click, counting
  hits and misses, a box to type in and a list to scroll - for the student, and for
  staff to try a setting straight after changing it.
- **LT has a job while the keyboard is closed**, chosen per student on the Controller
  tab: **Zoom** (Windows Magnifier, the default - LT zooms in around the pointer, LT
  again zooms out), **Escape**, **Slow pointer** (switched on and off, never held, at a
  chosen share of normal speed), or **Nothing**. Each shows what it did on screen. While
  the keyboard is open LT still moves it. The Guide's LT label and a "How do I..." card
  follow the choice.
- **Export and import settings** on the Controller tab, to give another student the same
  starting point. Windows' own keyboard setting, which belongs to the PC, is not copied.
- Slow pointer on the right stick (after Y swaps them) was the slow share instead of a
  share of the normal speed; it is now the same for both sticks.

### Shortcut keys and apps chosen for each student

HIDra is used by students with different needs - one is deaf, others rely on voice - so
what is on the keyboard is now chosen per student rather than the same for everyone.

- **Shortcut keys** (Keyboard tab): click a key in a copy of the four rows and pick
  from that row's list, each with its icon and a line saying what it does - or leave the
  place empty. Choices come from a fixed list of safe keys, never typed-in combinations.
  Choosing a key already in the row swaps the two. **Back to standard** undoes it all.
  New to the list: Captions (Windows Live Captions - words on screen for any sound),
  Find, Clipboard history, Top and Bottom, Centre and Left, Print, Screen shot, Magnify
  and Magnify off (Windows Magnifier), Desktop.
- **The standard set** now has Captions in Tools, and Voice; Read aloud and Files are
  there to add for the students who want them. The Typing guide only shows the Read
  aloud card for a student who has the key.
- **Apps key** (Keyboard tab): choose the six programs it offers, or leave places empty.
  Excel, OneNote, Outlook, Firefox, Notepad and Calculator can be added to the original
  six; only programs installed on the PC are offered.

### Buttons chosen for each student, and practice that keeps score

- **Button jobs** (Controller tab): the Guide's controller drawing again, where clicking
  a label - or the button itself - lists the jobs it can have: Click, Right click,
  Double click, Open the keyboard, Swap the sticks, Show open programs, Start menu, All
  windows, Maximise, Minimise, Snap left and right, Undo, Redo, Copy, Paste, Escape,
  Enter, Tab, Close window, or Nothing. A fixed list, never typed-in key combinations.
  The D-pad and the stick presses can each be set one direction or stick at a time. LT's
  choice has moved here too. **Back to standard** undoes it all.
- **What cannot be lost, is not.** Holding Back and Start together always brings HIDra
  back, whatever they are given. The last button that clicks, and the last that opens
  the keyboard, cannot be changed until another button has the job - the list says so,
  in yellow. The keyboard can only go on X, Back, Start or a stick press, the buttons
  that keep their job while typing, so it can always be closed again. A settings file
  that breaks these rules, edited by hand or imported, goes back to standard.
- **The Guide follows the student's buttons**: every label on the drawing, and the "How
  do I..." cards, name the buttons this student actually has - a card for a job no
  button has is left out. So does the Keyboard tab's "X opens and closes it".
- **Practice has three activities, and remembers.**
  - **Circles**: one circle at a time. Five hits with no more than one miss and it gets
    smaller, through eight sizes, down to the size of a small button; a run of misses
    makes it bigger again. Dots show how small it has got, and the student carries on
    at that size next time. **Biggest again** starts over.
  - **Type the word**: a word in big letters, its letters turning orange as they are
    typed right. Three-letter words first, longer ones as more are typed. Wrong letters
    are counted; capitals and the space a word suggestion adds are not held against it.
  - **Keyboard on and off**: open the keyboard, then close it, each step lit in turn,
    with how long it took.
  - Every change is shown on screen, never only as a sound.
  - **Progress**: each day's hits, misses, smallest circle, words, wrong letters and
    keyboard rounds, the last week shown beside the activities, all of it kept in
    `practice.json` beside the student's settings. **Save for a review...** writes every
    day as a spreadsheet. Exporting settings does not copy it.

### Tabs that look like tabs, pages that fit, and tests that measure progress

From trying the last round: the tabs looked like buttons, the drawing blew up on a big
screen, pages needed scrolling on a laptop, and Practice was more play than practice.

- **Real tabs**: a word with an orange bar under the page showing. **Guide, Practice**,
  then under Settings **Buttons, Pointer, Keyboard** - the Controller tab is split in
  two, so nothing is below the fold.
- **Every page fits** a 1366x768 laptop without scrolling, and the window cannot be made
  smaller than that. On a big screen the Guide's drawing stops growing instead of
  blowing its words up. **How do I...** opens one answer at a time - click a question.
- **Settings rows line up**: every row has the same columns - its On/Off switch, then
  its value between - and + (or its two choices) - so the controls form straight lines
  down the page.
- **LT can have any job**, the same list as the other buttons, plus Zoom and Slow
  pointer. New students start on **Escape**; a student's earlier choice is kept.
- **Buttons tab**: beside the drawing, a card for the chosen button - what it **does**,
  what it does **while typing**, and its **standard** job with **Reset this button** -
  then its possible jobs, grouped (Clicks, Keyboard and pointer, Windows, Editing,
  Keys), each with an icon, growing to fill a bigger screen. A dot marks the standard
  job; a job that cannot go on the button shows a lock, and pointing at any job says
  what it does, or why not, in the line at the bottom. With no button chosen, the panel
  lists every button changed for this student, each one click from its jobs.
- **Keyboard tab**: the shortcut rows and the Apps row together, with the choices
  opening over the page, where there is room for them all.
- **The Apps key can open any program on the Start menu** - Store apps too, such as the
  new Outlook, which HIDra could not see before - with a Find box to pick it out.
- **Practice is two repeatable tests**, the same every time, so each run compares with
  the last:
  - **Pointer test**: twelve circles, one at a time, in the same places every run. Four
    are further down or up the page, so reaching them means scrolling as well as
    pointing; an orange "Scroll down" or "Scroll up" says which way. Time, misses,
    overshoots (scrolling past a circle) and a speed score are kept. The speed score is
    Fitts's law, as in ISO 9241-9: how hard each move was for its distance and size,
    over how long it took - so it stays comparable as the circles shrink. At most one
    miss makes the circles smaller next time; a bad run makes them bigger.
  - **Typing test**: words, phrases or sentences, a fixed set for each. The box is at
    the top of the page for one and the bottom for the next, so the keyboard is
    sometimes in the way and has to be moved with LT. Letters a minute, wrong letters,
    corrections and keyboard moves are kept; a clean run suggests the next level.
  - The scroll list and "Keyboard on and off" are gone. The right of the page shows the
    latest runs of each test and the best so far. **Save for a review...** writes every
    run as a spreadsheet.
- **The right stick jumps around the keyboard** while it is open: up to the word row,
  right to the shortcuts, back to the letters - landing on the nearest key - instead of
  a long walk across it. (With the sticks swapped it stays the pointer.)
- **More buttons type while the keyboard is open**, as on Windows' own gamepad
  keyboard: **D-pad left and right move the text cursor** (repeating when held), so a
  mistake a few letters back is reached without leaving the keys; **pressing the left
  stick in** swaps to the numbers and symbols; **Start** is Caps Lock and **Back** is
  Escape. D-pad up and down still move the orange box. Whichever button opens a
  student's keyboard keeps closing it, even if it is Back, Start or the left stick. The
  Guide's Typing view, a new "Fix a mistake" card and the Buttons tab say so.
- **Drag the keyboard anywhere**: hold LT and push the left stick, and the keyboard
  follows - slowly for a small push, quickly for a big one - never off the screen. A tap
  of LT still flips it between top and bottom; that now happens when LT is let go, once
  it is clear it was a tap. With the keyboard closed, LT acts on the press as before.
- **The icon's controller has a dark outline**, so it stands out from the orange, and
  the logo at the top left of the main screen is sharp instead of blocky.
- **No "HIDra is still running" pop-up** every time the window is closed - it got in
  the way. The Guide says how to bring the window back.
- **No close key on the keyboard.** The button that opens it closes it; the red key was
  one overshoot from ending typing by mistake.

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
