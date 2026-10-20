@echo off
setlocal
echo =======================================================
echo    BIM Plus (Revit 2026 & 2027) - Build and Installer
echo =======================================================

cd /d "%~dp0"

echo [1/2] Building the Revit Plugin for 2026 and 2027 (Release mode)...
powershell -NoProfile -ExecutionPolicy Bypass -Command "dotnet build AutoPileCap.csproj -c Release"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Build failed! Check error messages above.
    pause
    exit /b %ERRORLEVEL%
)

echo [1.5/2] Obfuscating code with Obfuscar to protect against hackers...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$exe = \"$env:USERPROFILE\.nuget\packages\obfuscar\2.2.50\tools\Obfuscar.Console.exe\"; if (Test-Path $exe) { & $exe \"%~dp0obfuscar.xml\"; Copy-Item \"%~dp0bin\Release\net8.0-windows\Obfuscated\AutoPileCap.dll\" \"%~dp0bin\Release\net8.0-windows\AutoPileCap.dll\" -Force; & $exe \"%~dp0obfuscar_2027.xml\"; Copy-Item \"%~dp0bin\Release\net10.0-windows\Obfuscated\AutoPileCap.dll\" \"%~dp0bin\Release\net10.0-windows\AutoPileCap.dll\" -Force; Write-Host '[SUCCESS] Assemblies obfuscated successfully!' -ForegroundColor Green }"

echo.
echo [2/2] Compiling Installer using Inno Setup...
set "INNO_SETUP_COMPILER=C:\Program Files\Inno Setup 7\ISCC.exe"

if not exist "%INNO_SETUP_COMPILER%" (
    set "INNO_SETUP_COMPILER=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

if not exist "%INNO_SETUP_COMPILER%" (
    set "INNO_SETUP_COMPILER=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
)

if not exist "%INNO_SETUP_COMPILER%" (
    set "INNO_SETUP_COMPILER=%LOCALAPPDATA%\Programs\Inno Setup 7\ISCC.exe"
)

if not exist "%INNO_SETUP_COMPILER%" (
    echo [ERROR] Inno Setup compiler not found.
    echo Please download and install Inno Setup from: https://jrsoftware.org/isdl.php
    pause
    exit /b 1
)

"%INNO_SETUP_COMPILER%" "Installer.iss"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Installer creation failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [SUCCESS] Installer created successfully! Look in the Output folder.
pause >nul
