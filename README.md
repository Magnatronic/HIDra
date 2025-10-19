# aXXess – Controller Accessibility Tool (v1.0.0)

Control Windows with an Xbox controller. Simple, reliable, zero‑config.

## Features
- Mouse with Left Stick; LT = precision mode
- Scroll with Right Stick
- D‑Pad window management: Up = Maximize, Down = Minimize, Left/Right = Snap
- X toggles on‑screen keyboard (UK layout, large keys)
- Y swaps cursor/scroll sticks
- RB/LB task switcher (Alt+Tab forward/back)
- Start opens Task View; Back opens Start menu
- No configuration files — sensible defaults are baked in

## Getting Started
1) Plug in an Xbox controller (USB or Bluetooth)
2) Launch the app
3) It auto‑connects and you’re ready to go

## Button Mapping
- A: Left click
- B: Right click
- X: Toggle virtual keyboard
- Y: Swap stick modes (cursor ↔ scroll)
- RB/LB: Next/Previous app (Alt+Tab)
- Back: Windows key, Start: Win+Tab
- D‑Pad: Up = Maximize, Down = Minimize, Left = Win+Left, Right = Win+Right
- LT: Precision mode, RT: Click & hold (drag)

## System Requirements
- Windows 10/11 (x64)
- Xbox One/Series controller
- No .NET install required when using the published binary (self‑contained)

## Build and Publish (for developers)
- Prerequisite: .NET 7 SDK
- Build the solution: dotnet build aXXess.sln
- Publish a Release, self‑contained, single‑file build:
  - Project: `src/HIDra.UI/HIDra.UI.csproj`
  - Runtime: `win-x64`
  - Output: `publish/`
  - Note: With WPF, a few native runtime DLLs are emitted alongside the EXE — this is expected.

## Smoke Test
1) After publishing, run `publish/aXXess.UI.exe`
2) Confirm the window title reads: “aXXess - Controller to Mouse/Keyboard”
3) Move the cursor with the Left Stick; scroll with the Right Stick
4) Press X to toggle the on‑screen keyboard; use D‑Pad Up/Down to maximize/minimize the active window

## Distribution
- Default release is self‑contained, single‑file for Windows x64 (~150 MB). It includes the .NET and WPF runtimes so it “just runs.”
- Advanced users may build a framework‑dependent variant (~1.5 MB EXE) but must pre‑install the .NET Desktop Runtime.

## License
MIT — see `LICENSE`.

