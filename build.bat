@echo off
setlocal
echo Building HIDra release
echo ======================

call "%~dp0build\find-dotnet.bat"
if errorlevel 1 exit /b 1

REM The version comes from the csproj so the folder name cannot drift from what the
REM application reports about itself - copies get passed around, and the version is
REM often the only way to tell two apart.
REM No pipe in the PowerShell here on purpose: cmd's for /f mangles one even escaped.
for /f "delims=" %%v in ('powershell -NoProfile -Command "([xml](Get-Content -Raw '%~dp0src\HIDra.UI\HIDra.UI.csproj')).Project.PropertyGroup.Version -join ''"') do set "VERSION=%%v"
if "%VERSION%"=="" (
    echo Could not read ^<Version^> from HIDra.UI.csproj
    exit /b 1
)

set "OUT=%~dp0release\HIDra-v%VERSION%"
echo Version %VERSION%  -^>  release\HIDra-v%VERSION%
echo.

REM Rename the old release out of the way before deleting it. Windows refuses to rename
REM a folder while anything inside it is open, so a HIDra running from it is caught here
REM with nothing touched - deleting first left a half-removed folder behind.
if exist "%OUT%" (
    ren "%OUT%" "HIDra-v%VERSION%-old" 2>nul
    if errorlevel 1 (
        echo HIDra is running from release\HIDra-v%VERSION%, which holds its files open.
        echo Exit it from the tray icon, then run this again. Nothing has been changed.
        call :pause_if_double_clicked
        exit /b 1
    )
    rmdir /s /q "%~dp0release\HIDra-v%VERSION%-old"
)
mkdir "%OUT%"

set "PROJECT=%~dp0src\HIDra.UI\HIDra.UI.csproj"
REM Minimal rather than quiet output: step 1 compiles everything and takes the best part
REM of a minute, and with nothing on screen that looked exactly like a hang.
set "COMMON=-c Release -p:DebugType=None -p:DebugSymbols=false -v m --nologo"

REM ---------------------------------------------------------------------------
REM 1. Network share - the one for shared or managed PCs.
REM
REM Self-contained, so nothing needs installing. All managed code is bundled into
REM HIDra.UI.exe (one network read instead of ~480), while WPF's five native DLLs sit
REM beside it. IncludeNativeLibrariesForSelfExtract=false is the point: an ordinary
REM single-file build unpacks those DLLs to %%TEMP%%\.net on every launch, which
REM locked-down machines block. This one leaves %%TEMP%%\.net untouched - checked,
REM not assumed.
REM ---------------------------------------------------------------------------
echo [1/3] Network share... (the longest step - about a minute)
"%DOTNET_EXE%" publish "%PROJECT%" %COMMON% -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=false ^
    -o "%OUT%\Network-share"
if errorlevel 1 goto :failed

REM ---------------------------------------------------------------------------
REM 2. USB stick, or a PC where nothing can be installed.
REM
REM A plain self-contained folder. It writes nowhere at run time; keep it whole.
REM ---------------------------------------------------------------------------
echo [2/3] USB portable folder...
"%DOTNET_EXE%" publish "%PROJECT%" %COMMON% -r win-x64 --self-contained true ^
    -o "%OUT%\USB-portable"
if errorlevel 1 goto :failed

REM ---------------------------------------------------------------------------
REM 3. PCs that already have the .NET 10 Desktop Runtime.
REM
REM Single file is safe here: every dependency it carries is managed, so the runtime
REM loads them straight out of the bundle and %%TEMP%%\.net is never touched.
REM ---------------------------------------------------------------------------
echo [3/3] Needs .NET 10...
"%DOTNET_EXE%" publish "%PROJECT%" %COMMON% --self-contained false ^
    -p:PublishSingleFile=true ^
    -o "%OUT%\Needs-dotNET-10"
if errorlevel 1 goto :failed

REM The guide travels with every build. Whoever copies HIDra onto a shared machine is
REM usually not the person using the controller, and each folder often goes on its own.
for %%F in (Network-share USB-portable Needs-dotNET-10) do copy /y "%~dp0QUICK-GUIDE.txt" "%OUT%\%%F\QUICK-GUIDE.txt" >nul
copy /y "%~dp0build\README-FIRST.txt" "%OUT%\README-FIRST.txt" >nul

echo.
echo Done: release\HIDra-v%VERSION%
echo   README-FIRST.txt   which one to use
echo   Network-share\     for shared or managed PCs running HIDra from the network
echo   USB-portable\      for a USB stick or a PC where nothing can be installed
echo   Needs-dotNET-10\   smallest, for PCs with the .NET 10 Desktop Runtime
call :pause_if_double_clicked
endlocal
exit /b 0

:failed
echo.
echo BUILD FAILED
call :pause_if_double_clicked
exit /b 1

REM Started by double-clicking, the window would close the moment the build ends, so
REM neither "Done" nor an error would ever be seen. Run from a terminal, no pause.
:pause_if_double_clicked
echo %cmdcmdline% | find /i "%~0" >nul
if not errorlevel 1 pause
exit /b 0
