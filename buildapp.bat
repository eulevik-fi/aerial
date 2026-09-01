@echo off
call buildsetup.bat || exit /b 1

rem --- Standalone app (AerialApp.exe) ---
dotnet publish AerialApp\AerialApp.csproj ^
-c Release -r win-x64 --self-contained true ^
-p:Version=%VERSION% -o out || exit /b 1

echo.
echo Build complete: %~dp0out\AerialApp.exe
