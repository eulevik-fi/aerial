@echo off
setlocal
if not defined VERSION set "VERSION=1.0.0"
set "VCVARS=C:\Program Files\Microsoft Visual Studio\18\Insiders\VC\Auxiliary\Build\vcvars64.bat"
if not exist "%VCVARS%" (
    echo [ERROR] vcvars64.bat not found at "%VCVARS%"
    exit /b 1
)
call "%VCVARS%"
if errorlevel 1 exit /b 1

rem --- Screensaver (Aerial.scr) ---
dotnet publish AerialScreenSaver\AerialScreenSaver.csproj -c Release -r win-x64 --self-contained false -p:Version=%VERSION% -o out
if errorlevel 1 exit /b 1

copy /y out\Aerial.exe out\Aerial.scr >nul
echo.
echo Build complete: %~dp0out\Aerial.scr

rem --- Standalone app (AerialApp.exe) ---
dotnet publish AerialApp\AerialApp.csproj -c Release -r win-x64 --self-contained false -p:Version=%VERSION% -o out
if errorlevel 1 exit /b 1
echo.
echo Build complete: %~dp0out\AerialApp.exe

rem --- Installer (Aerial Screen Saver <VERSION>.msi) ---
wix build --acceptEula true -arch x64 -d SourceDir=%~dp0out -d ProductVersion=%VERSION% -o "out\Aerial Screen Saver %VERSION%.msi" installer\Aerial.wxs
if errorlevel 1 exit /b 1
echo.
echo Installer complete: %~dp0out\Aerial Screen Saver %VERSION%.msi
endlocal
