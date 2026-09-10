@echo off
setlocal enabledelayedexpansion
echo Building HIDra release assets
echo ============================

call "%~dp0find-dotnet.bat"
if errorlevel 1 exit /b 1

REM The version comes from the csproj so the file names cannot drift from what the
REM application reports about itself.
REM No pipe in the PowerShell here on purpose: cmd's for /f mangles one even escaped.
for /f "delims=" %%v in ('powershell -NoProfile -Command "([xml](Get-Content -Raw '%~dp0src\HIDra.UI\HIDra.UI.csproj')).Project.PropertyGroup.Version -join ''"') do set "VERSION=%%v"
if "%VERSION%"=="" (
    echo Could not read ^<Version^> from HIDra.UI.csproj
    exit /b 1
)
echo Version: %VERSION%
echo.

if exist "%~dp0release" rmdir /s /q "%~dp0release"
mkdir "%~dp0release"

REM ---------------------------------------------------------------------------
REM 1. Framework-dependent, single file.
REM
REM Single file is safe here and only here. Every dependency this build carries is
REM managed, so the runtime loads them straight out of the bundle: launching it
REM leaves %TEMP%\.net untouched, which was verified rather than assumed. That is
REM what makes it usable on machines that block execution from user-writable paths.
REM ---------------------------------------------------------------------------
echo [1/3] Framework-dependent single file...
"%DOTNET_EXE%" publish "%~dp0src\HIDra.UI\HIDra.UI.csproj" ^
    -c Release ^
    -o "%~dp0release\_fx" ^
    --self-contained false ^
    -p:PublishSingleFile=true ^
    -p:DebugType=None ^
    -p:DebugSymbols=false ^
    -v q --nologo
if errorlevel 1 exit /b 1
move /y "%~dp0release\_fx\HIDra.UI.exe" "%~dp0release\HIDra-v%VERSION%-framework-dependent.exe" >nul
rmdir /s /q "%~dp0release\_fx"

REM ---------------------------------------------------------------------------
REM 2. Self-contained portable, as a folder in a zip.
REM
REM This is the build for locked-down machines, so it must not write anywhere at
REM run time. Keep the folder whole - it is not a pick-one-file-out archive.
REM
REM Published straight into the staging folder rather than reused from
REM publish-portable: a running HIDra holds its own DLLs open, so building a
REM release would otherwise fail whenever the app happened to be in use.
REM ---------------------------------------------------------------------------
echo [2/3] Self-contained portable folder...
"%DOTNET_EXE%" publish "%~dp0src\HIDra.UI\HIDra.UI.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -o "%~dp0release\HIDra-v%VERSION%-portable" ^
    -p:DebugType=None ^
    -p:DebugSymbols=false ^
    -v q --nologo
if errorlevel 1 exit /b 1

REM The guide travels with the build. Whoever unzips this onto a college machine is
REM usually not the person who will be using the controller, and this is the only
REM copy of the instructions they get.
copy /y "%~dp0QUICK-GUIDE.txt" "%~dp0release\HIDra-v%VERSION%-portable\QUICK-GUIDE.txt" >nul

powershell -NoProfile -Command "Compress-Archive -Path '%~dp0release\HIDra-v%VERSION%-portable' -DestinationPath '%~dp0release\HIDra-v%VERSION%-portable.zip' -CompressionLevel Optimal"
if errorlevel 1 exit /b 1
rmdir /s /q "%~dp0release\HIDra-v%VERSION%-portable"

REM ---------------------------------------------------------------------------
REM 3. Self-contained portable, single file.
REM
REM Offered for convenience on unrestricted machines only. Unlike the build above,
REM this one unpacks about 8 MB of native libraries into %TEMP%\.net on every
REM launch, so it fails wherever that path is blocked. The zip is the one to hand
REM to a managed site.
REM ---------------------------------------------------------------------------
echo [3/3] Self-contained portable single file...
"%DOTNET_EXE%" publish "%~dp0src\HIDra.UI\HIDra.UI.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -o "%~dp0release\_sc" ^
    -p:PublishSingleFile=true ^
    -p:DebugType=None ^
    -p:DebugSymbols=false ^
    -v q --nologo
if errorlevel 1 exit /b 1
move /y "%~dp0release\_sc\HIDra.UI.exe" "%~dp0release\HIDra-v%VERSION%-portable.exe" >nul
rmdir /s /q "%~dp0release\_sc"

echo.
echo Release assets in release\:
dir /b "%~dp0release"
endlocal
