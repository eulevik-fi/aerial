@echo off
setlocal
if not defined VERSION set /p VERSION=<version.txt
set "VCVARS=C:\Program Files\Microsoft Visual Studio\18\Insiders\VC\Auxiliary\Build\vcvars64.bat"
if not exist "%VCVARS%" (
    echo [ERROR] vcvars64.bat not found at "%VCVARS%"
    exit /b 1
)
call "%VCVARS%"
if errorlevel 1 exit /b 1

rem --- Screensaver (Aerial.scr) ---
dotnet publish AerialScreenSaver\AerialScreenSaver.csproj -c Release -r win-x64 --self-contained true -p:Version=%VERSION% -o out\installer-payload
if errorlevel 1 exit /b 1

copy /y out\installer-payload\Aerial.exe out\installer-payload\Aerial.scr >nul
copy /y out\installer-payload\Aerial.scr out\Aerial.scr >nul
copy /y "Setup Aerial.vbs" "out\installer-payload\Setup Aerial.vbs" >nul
copy /y Aerial.ico out\installer-payload\Aerial.ico >nul
echo.
echo Build complete: %~dp0out\installer-payload\Aerial.scr
endlocal
