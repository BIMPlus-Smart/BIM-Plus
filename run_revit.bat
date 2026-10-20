@echo off
setlocal
set "VER=%~1"
if "%VER%"=="2027" (
    echo Starting Autodesk Revit 2027 with BIM Plus Plugin...
    start "" "C:\Program Files\Autodesk\Revit 2027\Revit.exe"
    exit /b 0
)

if "%VER%"=="2026" (
    echo Starting Autodesk Revit 2026 with BIM Plus Plugin...
    start "" "C:\Program Files\Autodesk\Revit 2026\Revit.exe"
    exit /b 0
)

if exist "C:\Program Files\Autodesk\Revit 2027\Revit.exe" (
    echo ===========================================
    echo   Select Revit Version to launch BIM Plus:
    echo ===========================================
    echo   [1] Autodesk Revit 2027 (.NET 10.0)
    echo   [2] Autodesk Revit 2026 (.NET 8.0)
    echo ===========================================
    set "CHOICE=1"
    set /p CHOICE="Choose version [1 or 2, default 1]: "
    if "%CHOICE%"=="2" (
        echo Starting Autodesk Revit 2026...
        start "" "C:\Program Files\Autodesk\Revit 2026\Revit.exe"
    ) else (
        echo Starting Autodesk Revit 2027...
        start "" "C:\Program Files\Autodesk\Revit 2027\Revit.exe"
    )
) else (
    echo Starting Autodesk Revit 2026 with BIM Plus Plugin...
    start "" "C:\Program Files\Autodesk\Revit 2026\Revit.exe"
)
