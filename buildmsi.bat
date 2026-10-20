@echo off
call buildsetup.bat || exit /b 1

rem --- Setup Aerial UX (AerialSetup.exe) ---
msbuild AerialSetup\AerialSetup.vcxproj /p:Configuration=Release /p:Platform=x64 /m || exit /b 1
msbuild installer\AerialInstallerCA.vcxproj /p:Configuration=Release /p:Platform=x64 /m || exit /b 1

copy /y "installer\Aerial.ico" out\Aerial.ico >nul

rem --- Installer (Install Aerial Screen Saver <VERSION>.msi) ---
if not exist releases mkdir releases

wix build --acceptEula true ^
-arch x64 -ext WixToolset.Util.wixext -d SourceDir=%~dp0out -d ProductVersion=%VERSION% ^
-o "releases\Install Aerial Screen Saver %VERSION%.msi" installer\Aerial.wxs || exit /b 1

echo.
echo Installer complete: %~dp0releases\Install Aerial Screen Saver %VERSION%.msi
