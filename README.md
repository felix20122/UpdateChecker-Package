# UpdateChecker

Windows Update-Checker via winget. Prueft und installiert Updates automatisch beim Login und zeigt eine Toast-Benachrichtigung.

## Voraussetzungen

- Windows 10/11
- [winget](https://aka.ms/getwinget) (App Installer aus dem Microsoft Store)
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (nur zum Bauen)

## Bauen

```
build.bat
```

Output: `publish\UpdateChecker.exe` (~130 MB, self-contained, braucht kein .NET auf dem Ziel-PC)

> **Hinweis:** Die exe ist in `.gitignore` und wird nicht ins Repo committed. Repo-Groesse bleibt unter 1 MB.

## Installation

1. `build.bat` ausfuehren
2. `publish\UpdateChecker.exe` auf den Ziel-PC kopieren
3. Doppelklicken → GUI oeffnet sich
4. **"Install"** klicken → UAC-Prompt bestaetigen

Das wars. Ab jetzt prueft und installiert es bei jedem Windows-Login automatisch Updates.

## Deinstallation

GUI oeffnen → **"Uninstall"** klicken → raeumt alles auf (Task + Dateien + loescht sich selbst)

## Modi

| Aufruf | Modus |
|---|---|
| `UpdateChecker.exe` | GUI mit Install/Uninstall/Check Now |
| `UpdateChecker.exe /silent` | Headless: Updates pruefen + installieren + Toast (fuer Task Scheduler) |
| `UpdateChecker.exe /install` | CLI-Install mit UAC-Elevation |
| `UpdateChecker.exe /uninstall` | CLI-Uninstall mit UAC-Elevation |

## Was passiert im Silent-Modus?

1. Alte Logs aufraeumen (>30 Tage)
2. Max. 30s auf Netzwerk warten
3. winget finden
4. `winget upgrade --include-unknown` → verfuegbare Updates parsen
5. `winget upgrade --all --accept-package-agreements --accept-source-agreements --include-unknown`
6. Toast-Benachrichtigung mit den ersten 3 Paketnamen
7. Log schreiben nach `%USERPROFILE%\UpdateChecker\logs\`

## Dateistruktur

```
UpdateChecker-Package/
├── .gitignore
├── build.bat
├── README.md
└── src/UpdateChecker/
    ├── UpdateChecker.csproj
    ├── App.manifest
    ├── Program.cs               # Entry point, Arg-Dispatch
    ├── SilentMode.cs            # Headless update flow
    ├── WingetRunner.cs          # winget finden + ausfuehren
    ├── UpdateParser.cs          # winget-Output parsen
    ├── ToastNotifier.cs         # Windows Toast + Fallback
    ├── TaskSchedulerManager.cs  # schtasks.exe wrapper
    ├── AppInstaller.cs          # exe kopieren, Task anlegen
    ├── AppUninstaller.cs        # Task + Dateien entfernen
    ├── FileLogger.cs            # Log schreiben + aufraeumen
    └── MainForm.cs              # WinForms GUI
```

## Installationspfade

| Was | Wo |
|---|---|
| Exe | `%USERPROFILE%\UpdateChecker\UpdateChecker.exe` |
| Logs | `%USERPROFILE%\UpdateChecker\logs\` |
| Task | `UpdateChecker-Login` (ONLOGON, HIGHEST) |
