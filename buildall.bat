@echo off
call buildapp.bat || exit /b 1
call buildss.bat || exit /b 1
call buildmsi.bat || exit /b 1
