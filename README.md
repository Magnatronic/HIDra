# aXXess – Controller Accessibility Tool (v1.0.0)

Control Windows with an Xbox controller. aXXess is a single‑file, student‑proof app designed for accessibility: no configuration files, simple controls, and reliable window management.

## Features
- Mouse control with Left Stick, precision with LT
- Scroll with Right Stick
- D‑Pad window management: Up=Maximize, Down=Minimize, Left/Right=Snap
- X toggles on‑screen keyboard (UK layout, large keys)
- Y swaps cursor/scroll sticks
- RB/LB task switcher (Alt+Tab forward/back)
- Start opens Task View, Back opens Start menu

## Getting Started
1) Plug in an Xbox controller
2) Launch the app
3) It auto‑connects and you’re ready to go

## No Configuration Needed
All settings are hardcoded for reliability. No JSON files. Behavior is consistent on every machine.

## Button Mapping
- A: Left click
- B: Right click
- X: Toggle virtual keyboard
- Y: Swap stick modes (cursor ↔ scroll)
- RB/LB: Next/Previous app (Alt+Tab)
- Back: Windows key, Start: Win+Tab
- D‑Pad: Up=Maximize, Down=Minimize, Left=Win+Left, Right=Win+Right
- LT: Precision mode, RT: Click & hold (drag)

## License
MIT – see LICENSE.

## Notes
This repository is intentionally minimal for a clean v1.0.0 start.

| D-Pad Right | **Snap Right** | Position window on right half of screen || B (while switcher open) | Cancel | Close switcher without switching |

| Back Button | Windows Key | Open Start Menu |

### Application Switching| Start Button | Task View | Windows+Tab (view all windows) |

| Input | Action | Description |

|-------|--------|-------------|### Navigation & Shortcuts

| RB (Right Bumper) | Next Application | Navigate forward (Alt+Tab) || Input | Action | Description |

| LB (Left Bumper) | Previous Application | Navigate backward (Alt+Shift+Tab) ||-------|--------|-------------|

| Back Button | Windows Key | Open Start Menu || D-Pad Up | Page Up | Scroll up one page |

| Start Button | Task View | Windows+Tab (view all windows) || D-Pad Down | Page Down | Scroll down one page |

| D-Pad Left | Backspace | Delete previous character |

### Virtual Keyboard Features| D-Pad Right | Delete | Delete next character |

- **UK Layout** - " above 2, £ above 3

- **Large Text** - 20px base font, 18px bold letters### Modifier Modes

- **Visible Shift Symbols** - 13px light gray (#CCCCCC) above numbers| Input | Mode | Description |

- **Full Punctuation** - All UK standard characters|-------|------|-------------|

- **14-Column Balanced Layout** - Easy navigation| L3 (Left Stick Click) | Ctrl Mode | Hold for Ctrl combinations |

- **Dual Character Display** - See both normal and shift symbols| R3 (Right Stick Click) | Shift Mode | Hold for Shift combinations |



## 🏗️ Architecture Changes (v1.2.0)#### When Holding L3 (Ctrl Mode):

- **A** → Copy (Ctrl+C)

### Simplified Configuration- **B** → Paste (Ctrl+V)

✅ **All settings hardcoded** - No JSON config files  - **X** → Undo (Ctrl+Z)

✅ **Student-proof** - Cannot be accidentally broken  - **Y** → Redo (Ctrl+Y)

✅ **Consistent behavior** - Works the same every time  - **LB** → Next Tab (Ctrl+Tab)

✅ **Single file deployment** - Just `HIDra.UI.exe`  - **RB** → Previous Tab (Ctrl+Shift+Tab)

✅ **No config folder needed** - Everything built-in  

#### When Holding R3 (Shift Mode):

### Key Improvements- **Back** → On-Screen Keyboard

- **Removed L3/R3 modifier system** (simplified for accessibility)- **Start** → Close Window (Alt+F4)

- **Direct Windows API calls** for maximize/minimize (reliable)

- **Hardcoded optimal settings** (0.5 sensitivity, 0.05 deadzone)## 🏗️ Project Structure

- **UK keyboard layout** with high-contrast shift symbols

- **Window management** via D-Pad (practical for students)```

HIDra/

## 🛠️ Technology Stack├── src/

│   ├── HIDra.Core/              # Core business logic

- **.NET 7.0** - Framework│   │   ├── Controllers/         # Controller detection and input handling

- **WPF** - Windows Presentation Foundation for UI  │   │   ├── Input/              # Input processing and debouncing

- **SharpDX.XInput** - Xbox controller input detection│   │   ├── Mapping/            # Button/axis mapping logic

- **InputSimulatorPlus** - Mouse and keyboard simulation│   │   └── Simulation/         # Mouse/keyboard simulation

- **Windows API (P/Invoke)** - Direct window management calls│   ├── HIDra.UI/               # WPF application

- **CommunityToolkit.Mvvm** - MVVM pattern support│   │   ├── Views/              # XAML views

│   │   ├── ViewModels/         # MVVM view models

## 🚀 Getting Started│   │   └── Resources/          # Images, styles

│   └── HIDra.Models/           # Shared data models and DTOs

### For End Users (Running the App)├── config/

│   └── default-mappings.json   # Default Xbox controller mapping

**Quick Start:**├── docs/

1. Copy `HIDra.UI.exe` to your computer or USB stick│   └── user-guide.md           # End-user documentation

2. Connect your Xbox controller (USB or wireless adapter)└── tests/

3. Double-click `HIDra.UI.exe`    └── HIDra.Tests/            # Unit and integration tests

4. The app will auto-connect - start using your controller immediately!```



**That's it!** No configuration files, no installation, no admin rights required.## 🛠️ Technology Stack



See `QUICK-GUIDE.txt` for complete usage instructions.- **.NET 8** - Latest LTS version

- **WPF** - Windows Presentation Foundation for UI

### For Developers (Building from Source)- **HidLibrary** - HID device communication

- **InputSimulatorPlus** - Keyboard and mouse simulation

**Prerequisites:**- **JSON** - Configuration file format

- .NET 7.0 SDK or later

- Windows 10/11 (64-bit)## 🚀 Getting Started

- Xbox controller for testing

### For End Users (Running the App)

**Build Commands:**

```powershell**Quick Start:**

# Clone and navigate to the project1. Copy `HIDra.UI.exe` from the `publish/` folder to your computer or USB stick

cd HIDra2. Connect your Xbox controller (USB or wireless adapter)

3. Double-click `HIDra.UI.exe`

# Development build4. The app will auto-connect - start using your controller immediately!

dotnet build

**Optional:** Copy the `config/` folder if you want to customize settings later.

# Release build (portable single-file executable)

dotnet publish src/HIDra.UI/HIDra.UI.csproj `See `publish/QUICK-GUIDE.txt` for complete usage instructions.

  --configuration Release `

  --runtime win-x64 `### For Developers (Building from Source)

  --self-contained true `

  -p:PublishSingleFile=true `**Prerequisites:**

  --output publish- .NET 7.0 SDK or later

```- Windows 10/11 (64-bit)

- Xbox controller for testing

The portable executable will be in `publish/HIDra.UI.exe` (approximately 70 MB).

**Build Commands:**

## ✨ Key Features```powershell

# Clone and navigate to the project

### Accessibility Optimizationscd HIDra

- **95% Linear Sensitivity Zone** - Predictable, forgiving mouse movement

- **Controller Calibration** - Compensates for worn controller sticks (90% max range)# Development build

- **Low Deadzone (5%)** - Maximum usable stick range without driftdotnet build

- **Smooth Scroll Accumulation** - Proportional scrolling without instant max speed

- **Auto-Connect** - Automatically detects and connects to controller on startup# Release build (portable single-file executable)

- **No Admin Rights Required** - Runs in restricted environments (perfect for college/library computers)dotnet publish src/HIDra.UI -c Release -r win-x64 --self-contained true `

- **Large Virtual Keyboard** - UK layout with 20px font, high-contrast shift symbols  -p:PublishSingleFile=true `

- **Hardcoded Settings** - Cannot be accidentally changed or corrupted  -p:IncludeNativeLibrariesForSelfExtract=true `

  -p:EnableCompressionInSingleFile=true `

### Technical Features  -o publish

- **Self-Contained** - No .NET runtime installation needed```

- **Portable** - Single executable that runs from USB stick

- **No Configuration Files** - Everything hardcoded for reliabilityThe portable executable will be in `publish/HIDra.UI.exe` (approximately 68.5 MB).

- **Visual Task Switcher** - Navigate windows with controller-only interface

- **XInput Support** - Works with Xbox 360, Xbox One, Xbox Series controllers## ✨ Key Features

- **Windows API Integration** - Direct maximize/minimize calls (always works)

```markdown
# aXXess – Controller Accessibility Tool (v1.0.0)

Control Windows with an Xbox controller. aXXess is a single‑file, student‑proof app designed for accessibility: no configuration files, simple controls, and reliable window management.

## Features
- Mouse control with Left Stick; hold LT for precision
- Scroll with Right Stick
- D‑Pad window management: Up=Maximize, Down=Minimize, Left/Right=Snap
- X toggles on‑screen keyboard (UK layout, large keys)
- Y swaps cursor/scroll sticks
- RB/LB task switcher (Alt+Tab forward/back)
- Start opens Task View; Back opens Start menu

## Getting Started
1) Plug in an Xbox controller
2) Launch aXXess.UI.exe
3) It auto‑connects and you’re ready to go

## No Configuration Needed
All settings are hardcoded for reliability. No JSON files. Behavior is consistent on every machine.

## Button Mapping
- A: Left click
- B: Right click
- X: Toggle virtual keyboard
- Y: Swap stick modes (cursor ↔ scroll)
- RB/LB: Next/Previous app (Alt+Tab)
- Back: Windows key; Start: Win+Tab
- D‑Pad: Up=Maximize, Down=Minimize, Left=Win+Left, Right=Win+Right
- LT: Precision mode; RT: Click & hold (drag)

## Build/Publish (optional)
Published single‑file build will appear in the `publish/` folder.

## License
MIT – see LICENSE.

## Notes
This repository is intentionally minimal for a clean v1.0.0 start. See `QUICK-GUIDE.txt` for a printable reference.
```

