@echo off
setlocal enabledelayedexpansion
echo Building HIDra - Portable Standalone Version (No Runtime Required)
echo ================================================================

REM Clean previous builds
if exist "publish-portable" rmdir /s /q "publish-portable"

REM Build self-contained portable version
dotnet publish src\HIDra.UI\HIDra.UI.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish-portable

if %ERRORLEVEL% == 0 (
    echo.
    echo ✅ Portable standalone build completed successfully!
    echo 📦 Output: publish-portable\HIDra.UI.exe
    
    REM Show file size
    for %%F in (publish-portable\HIDra.UI.exe) do (
        set /a sizeMB=%%~zF/1048576
        echo 📏 Size: !sizeMB! MB
    )
    echo.
    echo ℹ️  This version includes everything needed - no runtime installation required!
    echo 💾 Perfect for USB sticks and portable use
) else (
    echo ❌ Build failed!
    exit /b 1
)

pause