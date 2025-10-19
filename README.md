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

### Accessibility Optimizations

### Current Status- **95% Linear Sensitivity Zone** - Predictable, forgiving mouse movement

✅ **Production-Ready v1.2.0**- **Controller Calibration** - Compensates for worn controller sticks (90% max range)

- Full Xbox controller support- **Low Deadzone (5%)** - Maximum usable stick range without drift

- All button mappings hardcoded- **Smooth Scroll Accumulation** - Proportional scrolling without instant max speed

- Task switcher with Alt+Tab integration- **Auto-Connect** - Automatically detects and connects to controller on startup

- Virtual keyboard with UK layout- **No Admin Rights Required** - Runs in restricted environments (perfect for college/library computers)

- Window management via D-Pad

- Portable single-file deployment### Technical Features

- Comprehensive documentation- **Self-Contained** - No .NET runtime installation needed

- Student-proof configuration- **Portable** - Single executable that runs from USB stick

- **Configurable** - JSON configuration files for customization

## 📚 Documentation- **Visual Task Switcher** - Navigate windows with controller-only interface

- **XInput Support** - Works with Xbox 360, Xbox One, Xbox Series controllers

### For Users

- `QUICK-GUIDE.txt` - Complete usage guide (print this!)### Current Status

- `RELEASE-NOTES-v1.2.0.md` - What's new in this version✅ **Complete and Production-Ready**

- `DEPLOYMENT.md` - How to deploy to multiple machines- Full Xbox controller support

- All button mappings implemented

### For Developers- Task switcher with visual navigation

- `README.md` - This file (project overview)- Configuration system

- `DEPLOYMENT.md` - Build and deployment instructions- Portable single-file deployment

- `docs/` - Technical implementation details- Comprehensive documentation



## 🔧 Hardcoded Settings (v1.2.0)## 🛠️ Technology Stack



All settings are optimized for accessibility and cannot be changed:- **.NET 7.0** - Framework

- **WPF** - Windows Presentation Foundation for UI

```csharp- **SharpDX.XInput** - Xbox controller input detection

CursorSensitivity = 0.5f           // Moderate speed- **InputSimulatorPlus** - Mouse and keyboard simulation

ScrollSensitivity = 0.5f           // Smooth scrolling- **Newtonsoft.Json** - Configuration file handling

PrecisionModeSensitivity = 0.3f    // Very slow for precision- **CommunityToolkit.Mvvm** - MVVM pattern support

Deadzone = 0.05f                   // 5% deadzone (low for control)

PollRateMs = 10                    // 100Hz polling rate## 📚 Documentation

StickCalibrationMax = 0.90f        // Compensate for worn sticks

TriggerThreshold = 0.3f            // 30% press to activate### For Users

```- `publish/QUICK-GUIDE.txt` - Quick reference guide (comprehensive)

- `publish/CONFIGURATION.md` - How to customize settings (optional)

### Button Mappings (Hardcoded)

```### For Developers

A = Left Click- `README.md` - This file (project overview)

B = Right Click- `DEPLOYMENT.md` - Build and deployment instructions

X = Toggle Virtual Keyboard- `docs/task-switcher-implementation.md` - Technical implementation details

Y = Swap Stick Modes

LB = Previous App (Alt+Shift+Tab)## 📝 Configuration

RB = Next App (Alt+Tab)

Back = Windows Key (Start Menu)Configuration files use JSON format and are stored in the `config/` folder:

Start = Task View (Win+Tab)- `default-mappings.json` - Default settings (read-only)

D-Pad Up = Maximize Window (API call)- `user-mappings.json` - Your custom settings (auto-created when you customize)

D-Pad Down = Minimize Window (API call)

D-Pad Left = Snap Left (Win+Left)The app will use `user-mappings.json` if it exists, otherwise falls back to `default-mappings.json`.

D-Pad Right = Snap Right (Win+Right)

LT = Precision Mode (hold)**Portable Mode:** Keep the `config/` folder next to `HIDra.UI.exe` on your USB stick.

RT = Click & Hold (hold)

```See `publish/CONFIGURATION.md` for customization options.



## 🧪 Testing## 🧪 Testing



Run tests with:Run tests with:

```powershell```powershell

dotnet testdotnet test

``````



## 💡 Tips for Users## 💡 Tips for Users



1. **Precision Mode**: Hold Left Trigger for slower, more precise cursor control1. **Precision Mode**: Hold Left Trigger for slower, more precise cursor control

2. **Task Switcher**: Tap RB/LB to navigate windows, release to switch2. **Task Switcher**: Tap RB/LB to navigate windows, press A to select

3. **Portable Setup**: Just copy `HIDra.UI.exe` to USB stick and run - no config files needed!3. **Portable Setup**: Just copy `HIDra.UI.exe` to USB stick and run

4. **Window Snapping**: Use D-Pad Left/Right to work with two programs side-by-side4. **Auto-Connect**: The app automatically connects to your controller on startup

5. **Virtual Keyboard**: Press X to open UK layout keyboard with big, clear letters5. **Stick Drift**: The app is calibrated for worn controllers (compensates for 90% max range)

6. **Auto-Connect**: The app automatically connects to your controller on startup

7. **Stick Drift**: The app is calibrated for worn controllers (compensates for 90% max range)---



## 🎓 College Deployment**Built with ❤️ for accessibility**



Perfect for accessibility in educational environments:##  Known Limitations



✅ **Single file** - Just `HIDra.UI.exe`, nothing else needed  - May not work with anti-cheat software that blocks input simulation

✅ **No installation** - Runs directly from USB or network drive  - Controller must be connected before launching the app

✅ **No admin rights** - Works on locked-down college computers  - Some keyboard shortcuts may not work in apps running as Administrator

✅ **Student-proof** - No config files to break  

✅ **Consistent** - Works exactly the same on every machine  ##  Contributing

✅ **UK layout** - Proper £ symbol and UK punctuation  

✅ **Documentation** - Print `QUICK-GUIDE.txt` for reference  This project is designed for accessibility and welcomes contributions that improve usability for users with mobility challenges.



See `DEPLOYMENT.md` for deployment strategies.##  License



---Open Source - See LICENSE file for details



## 📦 Project Structure---



```**Built with  for accessibility**

HIDra/
├── src/
│   ├── HIDra.Core/              # Core business logic
│   │   ├── Controllers/         # Xbox controller via XInput
│   │   ├── Input/              # Input processing and debouncing
│   │   ├── Actions/            # Button action execution
│   │   └── Simulation/         # Mouse/keyboard simulation
│   ├── HIDra.UI/               # WPF application
│   │   ├── Views/              # XAML views and virtual keyboard
│   │   ├── ViewModels/         # MVVM view models
│   │   └── Resources/          # Images, styles
│   └── HIDra.Models/           # Shared data models
├── docs/                       # Technical documentation
├── tests/                      # Unit tests
├── QUICK-GUIDE.txt            # User reference guide
├── README.md                  # This file
└── DEPLOYMENT.md              # Deployment instructions
```

## 🚨 Known Limitations

- May not work with anti-cheat software that blocks input simulation
- Controller must be connected before launching the app
- Some keyboard shortcuts may not work in apps running as Administrator
- Windows-only (requires .NET 7.0 and Windows 10/11)
- Ctrl+Alt+Delete cannot be simulated (Windows security restriction)

## 🔄 Changelog

### v1.2.0 (Current) - Simplified Edition
- ✅ Removed all configuration files (hardcoded everything)
- ✅ Removed L3/R3 modifier system (simplified)
- ✅ Remapped D-Pad to window management (maximize/minimize/snap)
- ✅ Windows API calls for reliable maximize/minimize
- ✅ Updated virtual keyboard with UK layout
- ✅ Made shift symbols highly visible (13px, light gray)
- ✅ 14-column balanced keyboard layout
- ✅ Fallback configuration if files missing
- ✅ Student-proof deployment

### v1.1.x - Enhanced Features
- Virtual keyboard with shift symbol visibility
- Task switcher improvements
- Configuration system
- UK keyboard layout

### v1.0.x - Initial Release
- Basic controller support
- Mouse and keyboard simulation
- Configurable mappings

---

## 💡 Contributing

This project is designed for accessibility and welcomes contributions that improve usability for users with mobility challenges.

**Focus areas:**
- Accessibility improvements
- Bug fixes
- Documentation
- Testing on different controller types

## 📄 License

Open Source - See LICENSE file for details

---

**Built with ❤️ for accessibility**  
**Optimized for college accessibility deployment**  
**v1.2.0 - Simplified Edition**
