@echo off
setlocal
echo Building HIDra - both versions
echo ==============================

call "%~dp0build-framework.bat"
if errorlevel 1 exit /b 1

call "%~dp0build-portable.bat"
if errorlevel 1 exit /b 1

echo.
echo Both builds completed:
echo   publish-framework\HIDra.UI.exe  - needs .NET 10 Desktop Runtime
echo   publish-portable\HIDra.UI.exe   - standalone, no runtime needed
endlocal
