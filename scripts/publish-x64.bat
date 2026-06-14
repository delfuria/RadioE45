@echo off
echo ========================================
echo  RadioE45 - Publish x64
echo ========================================

set PROJECT=..\RadioE45\RadioE45.csproj
set FRAMEWORK=net10.0-windows10.0.19041.0
set RUNTIME=win-x64
set OUTPUT=publish\x64

echo Pulizia cartella output...
if exist %OUTPUT% rmdir /s /q %OUTPUT%

echo.
echo Pubblicazione in corso per %RUNTIME%...
dotnet publish %PROJECT% -f %FRAMEWORK% -c Release -r %RUNTIME% --self-contained true -p:WindowsPackageType=None -o %OUTPUT%

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERRORE] Publish fallita. Controlla i messaggi sopra.
    pause
    exit /b 1
)

echo.
echo [OK] Publish completata in: %OUTPUT%
echo Ora puoi compilare RadioE45_x64.iss con Inno Setup.
echo.
pause