#Requires -Version 5.1
# Update-Checker - Installiert verfuegbare Updates via winget beim Login

# ============================================================
# ADMIN-RECHTE PRUEFEN & ANFORDERN
# ============================================================
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    try {
        $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$($MyInvocation.MyCommand.Path)`""
        Start-Process "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe" -Verb RunAs -ArgumentList $arguments -WindowStyle Hidden
        exit 0
    } catch {}
}

# ============================================================
# KONFIGURATION
# ============================================================
$LogPath = "$env:USERPROFILE\UpdateChecker\logs"
$LogRetentionDays = 30

# ============================================================
# FUNKTIONEN
# ============================================================

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $logEntry = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] [$Level] $Message"
    Write-Host $logEntry
    Add-Content -Path $script:LogFile -Value $logEntry -Encoding UTF8
}

function Show-Toast {
    param([string]$Title, [string]$Message)
    try {
        if (Get-Module -ListAvailable -Name BurntToast -ErrorAction SilentlyContinue) {
            Import-Module BurntToast
            $dismissButton = New-BTButton -Dismiss
            New-BurntToastNotification -Text $Title, $Message -AppLogo $null `
                -ExpirationTime (Get-Date).AddSeconds(30) -Button $dismissButton
            return
        }
        # Fallback
        Add-Type -AssemblyName System.Windows.Forms
        $balloon = New-Object System.Windows.Forms.NotifyIcon
        $balloon.Icon = [System.Drawing.SystemIcons]::Information
        $balloon.BalloonTipIcon = "Info"
        $balloon.BalloonTipTitle = $Title
        $balloon.BalloonTipText = $Message
        $balloon.Visible = $true
        $balloon.ShowBalloonTip(10000)
        Start-Sleep -Seconds 15
        $balloon.Dispose()
    } catch {
        Write-Log "Toast fehlgeschlagen: $_" "WARN"
    }
}

function Find-Winget {
    $cmd = Get-Command winget.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $knownPaths = @(
        "$env:LOCALAPPDATA\Microsoft\WindowsApps\winget.exe",
        "C:\Program Files\WindowsApps\Microsoft.DesktopAppInstaller_*\winget.exe"
    )
    foreach ($p in $knownPaths) {
        $resolved = Resolve-Path $p -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($resolved -and (Test-Path $resolved.Path)) { return $resolved.Path }
    }
    return $null
}

function Run-Winget {
    param([string]$Exe, [string[]]$Arguments)
    $pinfo = New-Object System.Diagnostics.ProcessStartInfo
    $pinfo.FileName = $Exe
    $pinfo.Arguments = $Arguments -join " "
    $pinfo.RedirectStandardOutput = $true
    $pinfo.RedirectStandardError = $true
    $pinfo.UseShellExecute = $false
    $pinfo.CreateNoWindow = $true
    $p = New-Object System.Diagnostics.Process
    $p.StartInfo = $pinfo
    $p.Start() | Out-Null
    $stdout = $p.StandardOutput.ReadToEnd()
    $p.WaitForExit()
    return $stdout
}

function Get-Updates {
    param([string]$Exe)
    $rawOutput = Run-Winget -Exe $Exe -Arguments @("upgrade", "--include-unknown")
    $lines = $rawOutput -split "`n" | Where-Object { $_.Trim() -ne "" }

    $separatorIndex = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "^-{10,}") { $separatorIndex = $i; break }
    }
    if ($separatorIndex -eq -1) { return @() }

    $updates = @()
    for ($i = $separatorIndex + 1; $i -lt $lines.Count; $i++) {
        $line = $lines[$i].Trim()
        if ($line -eq "" -or $line -match "^\d+ (Upgrades|upgrades)") { continue }
        if ($line.Length -gt 20) { $updates += $line }
    }
    return $updates
}

# ============================================================
# HAUPTPROGRAMM
# ============================================================
if (-not (Test-Path $LogPath)) { New-Item -Path $LogPath -ItemType Directory -Force | Out-Null }
$script:LogFile = Join-Path $LogPath "update-check_$(Get-Date -Format 'yyyy-MM-dd_HHmmss').log"

Write-Log "Update-Checker gestartet (Admin: $(if ($isAdmin) {'JA'} else {'NEIN'}))"

# Alte Logs aufraeumen
Get-ChildItem -Path $LogPath -Filter "*.log" -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$LogRetentionDays) } |
    ForEach-Object { Remove-Item $_.FullName -Force }

# Auf Netzwerk warten
Write-Log "Warte 30 Sekunden auf Netzwerk..."
Start-Sleep -Seconds 30

# winget finden
$winget = Find-Winget
if (-not $winget) {
    Write-Log "winget nicht gefunden!" "ERROR"
    Show-Toast -Title "Update-Checker Fehler" -Message "winget nicht verfuegbar! Bitte App Installer aus dem Microsoft Store installieren."
    exit 1
}
Write-Log "winget: $winget"

# Updates pruefen
Write-Log "Pruefe auf Updates..."
$updates = Get-Updates -Exe $winget

if ($updates.Count -eq 0) {
    Write-Log "Alles aktuell."
    exit 0
}

Write-Log "$($updates.Count) Update(s) gefunden:"
$updates | Select-Object -First 10 | ForEach-Object { Write-Log "  -> $_" }

# Installieren
Write-Log "Starte Installation..."
$result = Run-Winget -Exe $winget -Arguments @("upgrade", "--all", "--accept-package-agreements", "--accept-source-agreements", "--include-unknown")
Write-Log "Installation abgeschlossen."
Write-Log $result

# Benachrichtigung
$preview = ($updates | Select-Object -First 3) -join "`n"
$more = if ($updates.Count -gt 3) { "`n...und $($updates.Count - 3) weitere" } else { "" }
Show-Toast -Title "Updates installiert" -Message "$preview$more"

Write-Log "Update-Checker beendet."
