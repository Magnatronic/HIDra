# Changelog

HIDra is distributed as a folder that gets copied onto machines and USB sticks, so the
version number is often the only way to tell two copies apart. This file says what
changed between them.

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
