@echo off
rem Run the standalone AerialApp (full-screen red on all displays, loads the
rem video catalog). Exits on mouse movement or key press.

set "APP=%~dp0out\AerialApp.exe"
if not exist "%APP%" (
    echo [ERROR] %APP% not found. Run buildall.bat first.
    exit /b 1
)

start "" "%APP%"
