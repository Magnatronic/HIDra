# HIDra  Controller Accessibility Tool

Control Windows with an Xbox controller. Simple, reliable, zeroconfig.

## Features
- Mouse with Left Stick; LT = precision mode
- Scroll with Right Stick
- DPad window management: Up = Maximize, Down = Minimize, Left/Right = Snap
- X toggles onscreen keyboard (UK layout, large keys)
- Y swaps cursor/scroll sticks
- RB/LB task switcher (Alt+Tab forward/back)
- Start opens Task View; Back opens Start menu
- No configuration files  sensible defaults are baked in

## Getting Started
1) Plug in an Xbox controller (USB or Bluetooth)
2) Launch the app
3) It autoconnects and you're ready to go

## Button Mapping
- A: Left click
- B: Right click
- X: Toggle virtual keyboard
- Y: Swap stick modes (cursor  scroll)
- RB/LB: Next/Previous app (Alt+Tab)
- Back: Windows key, Start: Win+Tab
- DPad: Up = Maximize, Down = Minimize, Left = Win+Left, Right = Win+Right
- LT: Precision mode, RT: Click & hold (drag)

## System Requirements
- **Windows 10/11** (x64)
- **Xbox One/Series controller** (USB or Bluetooth)
- **For compact version**: .NET 8 Desktop Runtime
- **For portable version**: No additional requirements

## Distribution Options

**Two versions available to suit different needs:**

### Option 1: Compact Version (Recommended)
- **Size**: ~3 MB
- **Requirement**: .NET 8 Desktop Runtime
- **Best for**: Regular users, faster downloads
- **Download runtime**: [Microsoft .NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

### Option 2: Portable Version  
- **Size**: ~173 MB
- **Requirement**: None - completely standalone
- **Best for**: USB sticks, computers without admin rights, portable use
- **Trade-off**: Larger download but runs anywhere

## Build and Publish (for developers)
- **Prerequisite**: .NET 8 SDK
- **Build the solution**: `dotnet build HIDra.sln`

### Quick Build Scripts:
- **Compact version**: Run `build-framework.bat`
- **Portable version**: Run `build-portable.bat`  
- **Both versions**: Run `build-both.bat`

### Manual Build Commands:
```bash
# Framework-dependent (3 MB single-file)
dotnet publish src/HIDra.UI/HIDra.UI.csproj -c Release -o publish-framework -p:PublishSingleFile=true -p:SelfContained=false -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false

# Self-contained portable (173 MB single-file)
dotnet publish src/HIDra.UI/HIDra.UI.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o publish-portable
```

## Smoke Test
1) After publishing, run the executable from the publish folder
2) Confirm the window title reads: "HIDra - Controller to Mouse/Keyboard"
3) Move the cursor with the Left Stick; scroll with the Right Stick
4) Press X to toggle the onscreen keyboard; use DPad Up/Down to maximize/minimize the active window

## Distribution
Two deployment options are available to suit different environments and requirements:

**Framework-dependent** (~3 MB): Professional deployment requiring .NET 8 Desktop Runtime pre-installation. Ideal for managed environments and regular users.

**Self-contained portable** (~173 MB): Single executable with embedded runtime that runs anywhere on Windows x64 without dependencies. Perfect for portable use and environments without admin rights.

## License
MIT  see `LICENSE`.
