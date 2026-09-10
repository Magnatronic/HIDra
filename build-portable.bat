@echo off
setlocal
echo Building HIDra - self-contained portable (no runtime needed)
echo ============================================================

call "%~dp0find-dotnet.bat"
if errorlevel 1 exit /b 1

if exist "%~dp0publish-portable" rmdir /s /q "%~dp0publish-portable"

REM Folder rather than single file, for the same reason as the framework build.
"%DOTNET_EXE%" publish "%~dp0src\HIDra.UI\HIDra.UI.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -o "%~dp0publish-portable" ^
    -p:DebugType=None ^
    -p:DebugSymbols=false

if errorlevel 1 (
    echo.
    echo BUILD FAILED
    exit /b 1
)

echo.
echo Done: publish-portable\HIDra.UI.exe
echo Carries its own runtime - runs on a machine with no .NET installed.
endlocal
