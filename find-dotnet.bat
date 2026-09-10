@echo off
REM Locates a dotnet.exe that actually has an SDK, and sets DOTNET_EXE to it.
REM
REM This machine has the runtime-only install at "C:\Program Files\dotnet" on the
REM machine PATH, and the SDK in the user profile. Windows always puts machine PATH
REM entries ahead of user ones, so a bare "dotnet build" finds the runtime-only copy
REM and fails with "No .NET SDKs were found". Checking the user install first avoids
REM that without needing administrator rights.
REM
REM If the SDK is ever installed properly under Program Files, this still works: that
REM copy is found by the fallback below.

set "DOTNET_EXE="

REM 1) SDK installed into the user profile by dotnet-install.ps1
if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" (
    "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" --list-sdks >nul 2>&1
    if not errorlevel 1 (
        set "DOTNET_EXE=%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"
        goto :found
    )
)

REM 2) Standard machine-wide install
if exist "%ProgramFiles%\dotnet\dotnet.exe" (
    "%ProgramFiles%\dotnet\dotnet.exe" --list-sdks >nul 2>&1
    if not errorlevel 1 (
        set "DOTNET_EXE=%ProgramFiles%\dotnet\dotnet.exe"
        goto :found
    )
)

REM 3) Whatever is on PATH, as a last resort
where dotnet >nul 2>&1
if not errorlevel 1 (
    for /f "delims=" %%D in ('where dotnet') do (
        set "DOTNET_EXE=%%D"
        goto :found
    )
)

echo.
echo ERROR: No .NET SDK found.
echo.
echo HIDra needs the .NET 10 SDK to build. Install it with:
echo     winget install --id Microsoft.DotNet.SDK.10
echo (approve the administrator prompt when it appears)
echo.
exit /b 1

:found
exit /b 0
