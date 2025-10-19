@echo off
echo Building HIDra - Framework Dependent Single-File Version
echo ======================================================

REM Clean previous builds
if exist "publish-framework" rmdir /s /q "publish-framework"

REM Build framework-dependent single-file version
dotnet publish src\HIDra.UI\HIDra.UI.csproj -c Release -o publish-framework ^
    -p:PublishSingleFile=true ^
    -p:SelfContained=false ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:DebugType=None ^
    -p:DebugSymbols=false

if %ERRORLEVEL% == 0 (
    echo.
    echo ✅ Framework-dependent single-file build completed!
    echo 📦 Output: publish-framework\HIDra.UI.exe
    
    REM Show file size in KB
    for %%F in (publish-framework\HIDra.UI.exe) do (
        set /a sizeKB=%%~zF/1024
        echo 📏 Size: !sizeKB! KB
    )
    
    echo.
    echo ⚠️  Users need to install .NET 8 Desktop Runtime from:
    echo    https://dotnet.microsoft.com/download/dotnet/8.0
) else (
    echo ❌ Build failed!
)

echo.
pause