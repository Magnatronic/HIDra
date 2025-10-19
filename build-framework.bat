@echo off
echo Building HIDra - Framework Dependent Version (Requires .NET 8 Runtime)
echo =====================================================================

REM Clean previous builds
if exist "publish-framework" rmdir /s /q "publish-framework"

REM Build framework-dependent version
dotnet publish src\HIDra.UI\HIDra.UI.csproj -c Release -o publish-framework

if %ERRORLEVEL% == 0 (
    echo.
    echo ✅ Framework-dependent build completed successfully!
    echo 📦 Output: publish-framework\HIDra.UI.exe
    
    REM Show file size
    for %%F in (publish-framework\HIDra.UI.exe) do (
        set /a size=%%~zF/1024
        echo 📏 Size: !size! KB
    )
    echo.
    echo ℹ️  Users need to install .NET 8 Desktop Runtime from:
    echo    https://dotnet.microsoft.com/download/dotnet/8.0
) else (
    echo ❌ Build failed!
    exit /b 1
)

pause