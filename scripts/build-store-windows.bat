@echo off
echo ========================================
echo  RadioE45 - Store Package x64 + arm64
echo ========================================

set PROJECT=..\RadioE45\RadioE45.csproj
set FRAMEWORK=net10.0-windows10.0.19041.0
set OUTDIR=store-packages
set MSIX_SRC=..\RadioE45\store-packages

:: Pulisce le cartelle di output
if exist %OUTDIR% rmdir /s /q %OUTDIR%
if exist %MSIX_SRC% rmdir /s /q %MSIX_SRC%
mkdir %OUTDIR%

:: Pulisce gli intermediati di build Windows: contengono Package.appxmanifest e
:: AppxManifest.xml con il numero di versione precedente che dotnet publish riusa
:: dalla cache senza rigenarare, producendo MSIX col vecchio numero di versione.
set OBJ=..\RadioE45\obj
if exist %OBJ%\x64\Release\net10.0-windows10.0.19041.0 rmdir /s /q %OBJ%\x64\Release\net10.0-windows10.0.19041.0
if exist %OBJ%\arm64\Release\net10.0-windows10.0.19041.0 rmdir /s /q %OBJ%\arm64\Release\net10.0-windows10.0.19041.0
if exist %OBJ%\Release\net10.0-windows10.0.19041.0 rmdir /s /q %OBJ%\Release\net10.0-windows10.0.19041.0

:: ----------------------------------------
:: PUBLISH x64 (MSIX per Store)
:: ----------------------------------------
echo.
echo [1/2] Publish MSIX x64...
dotnet publish %PROJECT% ^
    -f %FRAMEWORK% ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:WindowsPackageType=MSIX ^
    -p:AppxPackageSigningEnabled=false ^
    -p:GenerateAppxPackageOnBuild=true
if %ERRORLEVEL% NEQ 0 (
    echo [ERRORE] Publish x64 fallita.
    pause
    exit /b 1
)
echo [OK] Publish x64 completata.

:: ----------------------------------------
:: PUBLISH arm64 (MSIX per Store)
:: ----------------------------------------
echo.
echo [2/2] Publish MSIX arm64...
dotnet publish %PROJECT% ^
    -f %FRAMEWORK% ^
    -c Release ^
    -r win-arm64 ^
    --self-contained true ^
    -p:WindowsPackageType=MSIX ^
    -p:AppxPackageSigningEnabled=false ^
    -p:GenerateAppxPackageOnBuild=true
if %ERRORLEVEL% NEQ 0 (
    echo [ERRORE] Publish arm64 fallita.
    pause
    exit /b 1
)
echo [OK] Publish arm64 completata.

:: ----------------------------------------
:: Copia i pacchetti finali
:: (MAUI mette l'output in RadioE45\store-packages\)
:: ----------------------------------------
echo.
echo [3/3] Raccolta pacchetti...

set FOUND=0
for /f "delims=" %%f in ('dir /s /b "%MSIX_SRC%\*_x64.msix" 2^>nul') do (
    copy "%%f" "%OUTDIR%\" >nul
    echo [OK] Copiato: %%~nxf
    set FOUND=1
)
for /f "delims=" %%f in ('dir /s /b "%MSIX_SRC%\*_arm64.msix" 2^>nul') do (
    copy "%%f" "%OUTDIR%\" >nul
    echo [OK] Copiato: %%~nxf
    set FOUND=1
)
if %FOUND%==0 (
    echo [ERRORE] Nessun file .msix trovato in %MSIX_SRC%.
    echo Contenuto della cartella:
    dir /s /b "%MSIX_SRC%" 2>nul
    pause
    exit /b 1
)

:: ----------------------------------------
:: DONE
:: ----------------------------------------
echo.
echo ========================================
echo  Pacchetti pronti per il Microsoft Store
echo  Output: scripts\%OUTDIR%\
echo.
echo  Carica entrambi i file .msix su
echo  Partner Center — Microsoft li firmera'
echo  e gestira' la distribuzione per arch.
echo ========================================
echo.
pause
