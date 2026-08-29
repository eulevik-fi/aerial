@echo off
setlocal
if not defined VERSION set /p VERSION=<version.txt

call buildapp.bat
if errorlevel 1 exit /b 1

call buildss.bat
if errorlevel 1 exit /b 1

call buildmsi.bat
if errorlevel 1 exit /b 1

endlocal
