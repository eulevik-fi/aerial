@echo off
call buildsetup.bat || exit /b 1

rem --- Screensaver (Aerial.scr) ---
dotnet publish AerialScreenSaver\AerialScreenSaver.csproj -c Release -r win-x64 --self-contained true -p:Version=%VERSION% -o out || exit /b 1

if exist "out\Aerial.scr" del /f /q "out\Aerial.scr" >nul
if exist "out\Aerial.exe" ren "out\Aerial.exe" "Aerial.scr" >nul

echo.
echo Build complete: %~dp0out\Aerial.scr
