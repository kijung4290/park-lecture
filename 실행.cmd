@echo off
setlocal
cd /d "%~dp0"
if not exist "bin\ParkLecture.exe" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "build.ps1"
)
start "" "bin\ParkLecture.exe"
