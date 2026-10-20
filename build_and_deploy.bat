@echo off
setlocal
echo =======================================================
echo    BIM Plus for Revit 2026 & 2027 - Build and Install
echo =======================================================

cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build_and_deploy.ps1"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Build or deployment failed! Check error messages above.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [SUCCESS] BIM Plus is installed into Revit 2026 and Revit 2027!
echo Press any key to exit.
pause >nul
