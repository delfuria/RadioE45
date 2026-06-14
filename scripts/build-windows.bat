@echo off
echo ========================================
echo  RadioE45 - Build completa x64 + arm64
echo ========================================

:: Percorso di Inno Setup Compiler (aggiusta se installato in altra cartella)
set ISCC="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

:: Verifica che Inno Setup sia installato
if not exist %ISCC% (
    echo [ERRORE] Inno Setup non trovato in %ISCC%
    echo Verifica il percorso di installazione e aggiorna questa variabile.
    pause
    exit /b 1
)

set PROJECT=..\RadioE45\RadioE45.csproj
set FRAMEWORK=net10.0-windows10.0.19041.0

:: Pulisce gli installer precedenti (contengono il numero di versione nel nome)
if exist installer rmdir /s /q installer

:: ----------------------------------------
:: PUBLISH x64
:: ----------------------------------------
echo.
echo [1/4] Publish x64...
if exist publish\x64 rmdir /s /q publish\x64
dotnet publish %PROJECT% -f %FRAMEWORK% -c Release -r win-x64 --self-contained true -p:WindowsPackageType=None -o publish\x64
if %ERRORLEVEL% NEQ 0 (
    echo [ERRORE] Publish x64 fallita.
    pause
    exit /b 1
)
echo [OK] Publish x64 completata.

:: ----------------------------------------
:: PUBLISH arm64
:: ----------------------------------------
echo.
echo [2/4] Publish arm64...
if exist publish\arm64 rmdir /s /q publish\arm64
dotnet publish %PROJECT% -f %FRAMEWORK% -c Release -r win-arm64 --self-contained true -p:WindowsPackageType=None -o publish\arm64
if %ERRORLEVEL% NEQ 0 (
    echo [ERRORE] Publish arm64 fallita.
    pause
    exit /b 1
)
echo [OK] Publish arm64 completata.

:: ----------------------------------------
:: INNO BUILD x64
:: ----------------------------------------
echo.
echo [3/4] Build installer x64 con Inno Setup...
%ISCC% RadioE45_x64.iss
if %ERRORLEVEL% NEQ 0 (
    echo [ERRORE] Build installer x64 fallita.
    pause
    exit /b 1
)
echo [OK] Installer x64 creato.

:: ----------------------------------------
:: INNO BUILD arm64
:: ----------------------------------------
echo.
echo [4/4] Build installer arm64 con Inno Setup...
%ISCC% RadioE45_arm64.iss
if %ERRORLEVEL% NEQ 0 (
    echo [ERRORE] Build installer arm64 fallita.
    pause
    exit /b 1
)
echo [OK] Installer arm64 creato.

:: ----------------------------------------
:: DONE
:: ----------------------------------------
echo.
echo ========================================
echo  Build completata con successo!
echo  Gli installer sono in: installer\
echo ========================================
echo.
pause