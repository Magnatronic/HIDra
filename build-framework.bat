@echo off
setlocal
echo Building HIDra - framework-dependent (requires .NET 10 Desktop Runtime)
echo ======================================================================

call "%~dp0find-dotnet.bat"
if errorlevel 1 exit /b 1

if exist "%~dp0publish-framework" rmdir /s /q "%~dp0publish-framework"

REM Published as a plain folder rather than a single file on purpose. Single-file
REM builds extract their native libraries to %TEMP%\.net at run time, and managed
REM environments routinely block execution from user-writable paths - which would
REM make HIDra fail on exactly the locked-down college machines it is meant for.
"%DOTNET_EXE%" publish "%~dp0src\HIDra.UI\HIDra.UI.csproj" ^
    -c Release ^
    -o "%~dp0publish-framework" ^
    --self-contained false ^
    -p:DebugType=None ^
    -p:DebugSymbols=false

if errorlevel 1 (
    echo.
    echo BUILD FAILED
    exit /b 1
)

echo.
echo Done: publish-framework\HIDra.UI.exe
echo Needs the .NET 10 Desktop Runtime on the target machine.
endlocal
