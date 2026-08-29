@echo off
setlocal
if not defined VERSION set /p VERSION=<version.txt

rem --- Call buildapp.bat ---
call buildapp.bat
if errorlevel 1 exit /b 1

rem --- Call buildss.bat ---
call buildss.bat
if errorlevel 1 exit /b 1

rem --- Call buildmsi.bat ---
call buildmsi.bat
if errorlevel 1 exit /b 1

endlocal
