@echo off
echo Deleting old ProgramData plugin...
del /F /Q "C:\ProgramData\Autodesk\Revit\Addins\2026\AutoPileCap.addin"
rmdir /S /Q "C:\ProgramData\Autodesk\Revit\Addins\2026\AutoPileCap"
echo Done!
pause
