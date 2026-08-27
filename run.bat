@echo off
rem Run the screensaver. Usage: run.bat [/s | /c | /p hwnd]
rem   /s = full screen on all displays (default if no arg given)
rem   /c = options dialog
rem   /p <hwnd> = preview inside a parent window handle

set "SCR=%~dp0out\Aerial.scr"
if not exist "%SCR%" (
    echo [ERROR] %SCR% not found. Run buildall.bat first.
    exit /b 1
)

set "ARG1=%~1"
if not defined ARG1 set "ARG1=/s"

start "" "%SCR%" %ARG1% %2
