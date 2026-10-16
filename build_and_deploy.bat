@echo off
setlocal
echo =======================================================
echo    Auto Pile Cap for Revit 2026 - Build and Install
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
echo [SUCCESS] Auto Pile Cap is installed into Revit 2026!
echo Press any key to exit.
pause >nul
