@echo off
setlocal
if not defined VERSION set /p VERSION=<version.txt
if not exist releases mkdir releases
set "VCVARS=C:\Program Files\Microsoft Visual Studio\18\Insiders\VC\Auxiliary\Build\vcvars64.bat"
if not exist "%VCVARS%" (
    echo [ERROR] vcvars64.bat not found at "%VCVARS%"
    exit /b 1
)
call "%VCVARS%"
if errorlevel 1 exit /b 1

rem --- Installer (Install Aerial Screen Saver <VERSION>.msi) ---
wix build --acceptEula true -arch x64 -ext WixToolset.Util.wixext -d SourceDir=%~dp0out\installer-payload -d ProductVersion=%VERSION% -o "releases\Install Aerial Screen Saver %VERSION%.msi" installer\Aerial.wxs
if errorlevel 1 exit /b 1
echo.
echo Installer complete: %~dp0releases\Install Aerial Screen Saver %VERSION%.msi
endlocal
