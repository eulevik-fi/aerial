@echo off
setlocal
if not defined VERSION set "VERSION=1.0.1"
set "VCVARS=C:\Program Files\Microsoft Visual Studio\18\Insiders\VC\Auxiliary\Build\vcvars64.bat"
if not exist "%VCVARS%" (
    echo [ERROR] vcvars64.bat not found at "%VCVARS%"
    exit /b 1
)
call "%VCVARS%"
if errorlevel 1 exit /b 1

rem --- Standalone app (AerialApp.exe) ---
dotnet publish AerialApp\AerialApp.csproj -c Release -r win-x64 --self-contained true -p:Version=%VERSION% -o out
if errorlevel 1 exit /b 1

echo.
echo Build complete: %~dp0out\AerialApp.exe
endlocal
