@echo off
setlocal EnableDelayedExpansion
:: ============================================================
:: UpdateChecker Deinstaller v2.0
:: Doppelklick zum Deinstallieren
:: ============================================================

:: Admin-Rechte anfordern
net session >nul 2>&1
if %errorLevel% neq 0 (
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

title UpdateChecker - Deinstallation
color 0C
echo.
echo  ================================================
echo    UpdateChecker Deinstaller
echo  ================================================
echo.

set "INSTALLDIR=%USERPROFILE%\UpdateChecker"
set "TASKNAME=UpdateChecker-Login"

:: --------------------------------------------------------
:: PRUEFEN OB UEBERHAUPT INSTALLIERT
:: --------------------------------------------------------
set "FOUND=0"

schtasks /Query /TN "%TASKNAME%" >nul 2>&1
if %errorLevel% equ 0 set "FOUND=1"

if exist "%INSTALLDIR%\UpdateChecker.ps1" set "FOUND=1"

if %FOUND% equ 0 (
    color 0E
    echo  UpdateChecker scheint nicht installiert zu sein.
    echo.
    echo    - Task '%TASKNAME%' nicht gefunden
    echo    - Ordner '%INSTALLDIR%' nicht gefunden
    echo.
    pause
    exit /b
)

:: --------------------------------------------------------
:: ANZEIGEN WAS ENTFERNT WIRD
:: --------------------------------------------------------
echo  Folgendes wird entfernt:
echo.

schtasks /Query /TN "%TASKNAME%" >nul 2>&1
if %errorLevel% equ 0 (
    echo    [x] Task Scheduler: %TASKNAME%
) else (
    echo    [ ] Task Scheduler: %TASKNAME% (nicht vorhanden)
)

if exist "%INSTALLDIR%" (
    echo    [x] Ordner: %INSTALLDIR%
) else (
    echo    [ ] Ordner: %INSTALLDIR% (nicht vorhanden)
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "if (Get-Module -ListAvailable -Name BurntToast) { exit 0 } else { exit 1 }" >nul 2>&1
if %errorLevel% equ 0 (
    echo    [?] BurntToast Modul (optional)
) else (
    echo    [ ] BurntToast Modul (nicht installiert)
)
echo.

set /p CONFIRM="  Wirklich deinstallieren? (J/N): "
if /i not "!CONFIRM!"=="J" (
    echo.
    echo  Abgebrochen.
    pause
    exit /b
)

echo.

:: --------------------------------------------------------
:: DEINSTALLATION
:: --------------------------------------------------------

:: Task entfernen
echo  [1/3] Entferne Task Scheduler Eintrag...
schtasks /Query /TN "%TASKNAME%" >nul 2>&1
if %errorLevel% equ 0 (
    schtasks /Delete /TN "%TASKNAME%" /F >nul 2>&1
    if !errorLevel! equ 0 (
        echo        Task entfernt
    ) else (
        echo        FEHLER beim Entfernen des Tasks
    )
) else (
    echo        Task war bereits entfernt
)
echo.

:: Dateien loeschen
echo  [2/3] Loesche Dateien und Ordner...
set "DELETED=0"
if exist "%INSTALLDIR%\logs" (
    rmdir /s /q "%INSTALLDIR%\logs" >nul 2>&1
    echo        logs\ geloescht
    set /a DELETED+=1
)
if exist "%INSTALLDIR%\UpdateChecker.ps1" (
    del /f /q "%INSTALLDIR%\UpdateChecker.ps1" >nul 2>&1
    echo        UpdateChecker.ps1 geloescht
    set /a DELETED+=1
)
if !DELETED! equ 0 (
    echo        Keine Dateien zum Loeschen gefunden
)
echo.

:: BurntToast optional entfernen
echo  [3/3] BurntToast Modul...
powershell -NoProfile -ExecutionPolicy Bypass -Command "if (Get-Module -ListAvailable -Name BurntToast) { exit 0 } else { exit 1 }" >nul 2>&1
if %errorLevel% equ 0 (
    set /p REMOVEBT="        BurntToast auch entfernen? (J/N): "
    if /i "!REMOVEBT!"=="J" (
        powershell -NoProfile -ExecutionPolicy Bypass -Command "Remove-Module BurntToast -Force -ErrorAction SilentlyContinue; Uninstall-Module BurntToast -Force -AllVersions -ErrorAction SilentlyContinue"
        echo        BurntToast entfernt
    ) else (
        echo        BurntToast beibehalten
    )
) else (
    echo        BurntToast war nicht installiert - uebersprungen
)
echo.

:: --------------------------------------------------------
:: SELBSTLOESCH-SCRIPT
:: --------------------------------------------------------
set "CLEANUP=%TEMP%\uc_cleanup.bat"
echo @echo off > "%CLEANUP%"
echo timeout /t 2 /nobreak ^>nul >> "%CLEANUP%"
echo del /f /q "%INSTALLDIR%\Deinstaller.bat" ^>nul 2^>^&1 >> "%CLEANUP%"
echo rmdir /q "%INSTALLDIR%" ^>nul 2^>^&1 >> "%CLEANUP%"
echo del /f /q "%%~f0" ^>nul 2^>^&1 >> "%CLEANUP%"

color 0A
echo  ================================================
echo    Deinstallation abgeschlossen!
echo  ================================================
echo.
echo    UpdateChecker vollstaendig entfernt.
echo.
echo  ================================================
echo.
pause

start /b "" "%CLEANUP%"
exit
