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

copy /y "installer\Setup Aerial.vbs" "out\Setup Aerial.vbs" >nul
copy /y "installer\Aerial.ico" out\Aerial.ico >nul

rem --- Installer (Install Aerial Screen Saver <VERSION>.msi) ---
if not exist releases mkdir releases
wix build --acceptEula true -arch x64 -ext WixToolset.Util.wixext -d SourceDir=%~dp0out -d ProductVersion=%VERSION% -o "releases\Install Aerial Screen Saver %VERSION%.msi" installer\Aerial.wxs
if errorlevel 1 exit /b 1
echo.
echo Installer complete: %~dp0releases\Install Aerial Screen Saver %VERSION%.msi
endlocal
