@echo off
setlocal EnableDelayedExpansion
:: ============================================================
:: UpdateChecker Installer v2.0
:: Doppelklick zum Installieren
:: ============================================================

:: Admin-Rechte anfordern
net session >nul 2>&1
if %errorLevel% neq 0 (
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

title UpdateChecker - Installation
color 0A
echo.
echo  ================================================
echo    UpdateChecker Installer
echo  ================================================
echo.

set "INSTALLDIR=%USERPROFILE%\UpdateChecker"
set "TASKNAME=UpdateChecker-Login"
set "ERRORS=0"

:: --------------------------------------------------------
:: VORAUSSETZUNGEN PRUEFEN
:: --------------------------------------------------------
echo  Pruefe Voraussetzungen...
echo.

:: PowerShell pruefen
echo    [*] PowerShell...
where powershell.exe >nul 2>&1
if %errorLevel% neq 0 (
    color 0C
    echo        FEHLER: PowerShell nicht gefunden!
    echo        Bitte Windows PowerShell installieren.
    echo.
    pause
    exit /b 1
)

:: PowerShell-Version pruefen (mindestens 5.1)
for /f "tokens=*" %%v in ('powershell -NoProfile -Command "$PSVersionTable.PSVersion.Major"') do set "PSVER=%%v"
if "%PSVER%"=="" (
    color 0C
    echo        FEHLER: PowerShell-Version konnte nicht ermittelt werden!
    pause
    exit /b 1
)
if %PSVER% LSS 5 (
    color 0C
    echo        FEHLER: PowerShell %PSVER% gefunden, mindestens Version 5 benoetigt!
    echo        Bitte Windows Management Framework 5.1 installieren.
    pause
    exit /b 1
)
echo        PowerShell %PSVER% - OK
echo.

:: Internet-Verbindung pruefen
echo    [*] Internetverbindung...
powershell -NoProfile -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; try { $r = Invoke-WebRequest -Uri 'https://www.microsoft.com' -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop; exit 0 } catch { exit 1 }"
if %errorLevel% neq 0 (
    color 0C
    echo        FEHLER: Keine Internetverbindung!
    echo        Fuer die Installation von Abhaengigkeiten wird Internet benoetigt.
    echo.
    pause
    exit /b 1
)
echo        Verbindung - OK
echo.

:: Vorherige Installation pruefen
echo    [*] Vorherige Installation...
if exist "%INSTALLDIR%\UpdateChecker.ps1" (
    echo        Vorherige Installation gefunden.
    echo.
    set /p OVERWRITE="        Ueberschreiben? (J/N): "
    if /i not "!OVERWRITE!"=="J" (
        :: Ohne delayed expansion: Workaround
        goto :checkOverwrite
    )
)
goto :afterOverwrite

:checkOverwrite
echo.
echo  Installation abgebrochen.
pause
exit /b

:afterOverwrite
echo        OK
echo.

echo  Voraussetzungen erfuellt!
echo.
echo  ------------------------------------------------
echo.

:: --------------------------------------------------------
:: INSTALLATION
:: --------------------------------------------------------

echo  [1/5] Erstelle Verzeichnisse...
if not exist "%INSTALLDIR%" mkdir "%INSTALLDIR%"
if not exist "%INSTALLDIR%\logs" mkdir "%INSTALLDIR%\logs"
echo        %INSTALLDIR%
echo.

echo  [2/5] Kopiere UpdateChecker Script...
if exist "%~dp0UpdateChecker.ps1" (
    copy /Y "%~dp0UpdateChecker.ps1" "%INSTALLDIR%\UpdateChecker.ps1" >nul
    echo        UpdateChecker.ps1 kopiert
) else (
    echo        FEHLER: UpdateChecker.ps1 nicht gefunden!
    echo        Bitte neben Installer.bat legen.
    pause
    exit /b 1
)
echo.

echo  [3/5] Installiere NuGet + BurntToast...

:: NuGet installieren und Ergebnis pruefen
echo        Installiere NuGet Provider...
powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; try { Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -Scope CurrentUser -ErrorAction Stop | Out-Null; Write-Host 'OK'; exit 0 } catch { Write-Host \"FEHLER: $_\"; exit 1 }"
if %errorLevel% neq 0 (
    echo        WARNUNG: NuGet konnte nicht installiert werden.
    echo        Toast-Benachrichtigungen sind evtl. eingeschraenkt.
    set /a ERRORS+=1
) else (
    echo        NuGet - OK
)

:: BurntToast installieren und Ergebnis pruefen
echo        Installiere BurntToast Modul...
powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; try { Install-Module -Name BurntToast -Force -Scope CurrentUser -AllowClobber -ErrorAction Stop | Out-Null; exit 0 } catch { Write-Host \"FEHLER: $_\"; exit 1 }"
if %errorLevel% neq 0 (
    echo        WARNUNG: BurntToast konnte nicht installiert werden.
    echo        Toast-Benachrichtigungen sind evtl. eingeschraenkt.
    set /a ERRORS+=1
) else (
    echo        BurntToast - OK
)

:: Verifizieren dass BurntToast geladen werden kann
powershell -NoProfile -ExecutionPolicy Bypass -Command "if (Get-Module -ListAvailable -Name BurntToast) { exit 0 } else { exit 1 }"
if %errorLevel% neq 0 (
    echo        WARNUNG: BurntToast ist nicht verfuegbar.
    echo        Fallback auf System-Benachrichtigungen.
    set /a ERRORS+=1
) else (
    echo        BurntToast verifiziert - OK
)
echo.

echo  [4/5] Erstelle Task Scheduler Eintrag...
schtasks /Delete /TN "%TASKNAME%" /F >nul 2>&1
schtasks /Create /TN "%TASKNAME%" /TR "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"%INSTALLDIR%\UpdateChecker.ps1\"" /SC ONLOGON /RL LIMITED /F >nul 2>&1
if %errorLevel% equ 0 (
    echo        Task '%TASKNAME%' erstellt
) else (
    echo        FEHLER beim Erstellen des Tasks!
    set /a ERRORS+=1
)
echo.

echo  [5/5] Kopiere Deinstaller...
if exist "%~dp0Deinstaller.bat" (
    copy /Y "%~dp0Deinstaller.bat" "%INSTALLDIR%\Deinstaller.bat" >nul
    echo        Deinstaller nach %INSTALLDIR% kopiert
) else (
    echo        Kein Deinstaller.bat gefunden (optional)
)
echo.

:: --------------------------------------------------------
:: ZUSAMMENFASSUNG
:: --------------------------------------------------------
if %ERRORS% GTR 0 (
    color 0E
    echo  ================================================
    echo    Installation mit %ERRORS% Warnung(en) abgeschlossen
    echo  ================================================
) else (
    color 0A
    echo  ================================================
    echo    Installation erfolgreich abgeschlossen!
    echo  ================================================
)
echo.
echo    Script:       %INSTALLDIR%\UpdateChecker.ps1
echo    Logs:         %INSTALLDIR%\logs\
echo    Modus:        auto
echo    Trigger:      Bei jedem Windows-Login
echo.
echo    Deinstallieren: %INSTALLDIR%\Deinstaller.bat
echo.
echo  ================================================
echo.
pause
