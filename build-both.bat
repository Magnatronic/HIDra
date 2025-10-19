@echo off
setlocal enabledelayedexpansion
echo Building HIDra - Both Versions
echo ==============================

call build-framework.bat
if %ERRORLEVEL% neq 0 exit /b 1

echo.
echo.

call build-portable.bat
if %ERRORLEVEL% neq 0 exit /b 1

echo.
echo.
echo 🎉 Both builds completed successfully!
echo.
echo 📁 Outputs:
echo    • Framework-dependent: publish-framework\HIDra.UI.exe (tiny, needs .NET 8)
echo    • Portable standalone:  publish-portable\HIDra.UI.exe  (large, no dependencies)
echo.

pause