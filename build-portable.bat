@echo off
echo Building HIDra - Portable Single-File Version (No Runtime Required)
echo ================================================================

REM Clean previous builds
if exist "publish-portable" rmdir /s /q "publish-portable"

REM Build self-contained single-file version
dotnet publish src\HIDra.UI\HIDra.UI.csproj -c Release -r win-x64 -o publish-portable ^
    --self-contained ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:DebugType=None ^
    -p:DebugSymbols=false

if %ERRORLEVEL% == 0 (
    echo.
    echo ✅ Portable single-file build completed!
    echo 📦 Output: publish-portable\HIDra.UI.exe
    
    REM Show file size in MB
    for %%F in (publish-portable\HIDra.UI.exe) do (
        set /a sizeMB=%%~zF/1048576
        echo 📏 Size: !sizeMB! MB
    )
    
    echo.
    echo ✅ This version includes everything needed - no runtime installation required
    echo 🎒 Perfect for USB sticks and portable use
) else (
    echo ❌ Build failed!
)

echo.
pause