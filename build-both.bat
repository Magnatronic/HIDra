@echo off
echo Building HIDra - Both Single-File Versions
echo ==========================================

call build-framework.bat
call build-portable.bat

echo.
echo 🎯 Both builds completed!
echo.
echo 📦 Outputs:
echo    • Framework-dependent: publish-framework\HIDra.UI.exe (requires .NET 8)
echo    • Portable standalone:  publish-portable\HIDra.UI.exe  (no dependencies)
echo.
pause