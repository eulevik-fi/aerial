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
dotnet publish AerialScreenSaver\AerialScreenSaver.csproj -c Release -r win-x64 --self-contained true -p:Version=%VERSION% -o out
if errorlevel 1 exit /b 1

if exist "out\Aerial.scr" del /f /q "out\Aerial.scr" >nul
if exist "out\Aerial.exe" ren "out\Aerial.exe" "Aerial.scr" >nul

echo.
echo Build complete: %~dp0out\Aerial.scr
endlocal
