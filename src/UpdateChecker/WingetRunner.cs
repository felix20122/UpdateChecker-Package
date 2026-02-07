using System;
using System.Diagnostics;
using System.IO;

namespace UpdateChecker;

public static class WingetRunner
{
    public static string? FindWinget()
    {
        // 1. Try PATH
        try
        {
            var psi = new ProcessStartInfo("where", "winget.exe")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc != null)
            {
                var output = proc.StandardOutput.ReadLine();
                proc.WaitForExit();
                if (proc.ExitCode == 0 && !string.IsNullOrEmpty(output) && File.Exists(output))
                    return output;
            }
        }
        catch { }

        // 2. Known paths
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var knownPath = Path.Combine(localAppData, "Microsoft", "WindowsApps", "winget.exe");
        if (File.Exists(knownPath))
            return knownPath;

        // 3. Program Files WindowsApps
        try
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var windowsApps = Path.Combine(programFiles, "WindowsApps");
            if (Directory.Exists(windowsApps))
            {
                foreach (var dir in Directory.GetDirectories(windowsApps, "Microsoft.DesktopAppInstaller_*"))
                {
                    var candidate = Path.Combine(dir, "winget.exe");
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Installs winget via the official Microsoft DesktopAppInstaller MSIX bundle from GitHub.
    /// Requires admin rights. Returns the path to winget.exe on success, null on failure.
    /// </summary>
    public static string? InstallWinget(Action<string>? log = null)
    {
        log?.Invoke("winget nicht gefunden – versuche automatische Installation...");

        try
        {
            // Use PowerShell to download and install the latest App Installer bundle
            var script = @"
$ErrorActionPreference = 'Stop'
$progressPreference = 'SilentlyContinue'
$tempDir = Join-Path $env:TEMP 'WingetInstall'
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

# Download latest release info from GitHub
$apiUrl = 'https://api.github.com/repos/microsoft/winget-cli/releases/latest'
$release = Invoke-RestMethod -Uri $apiUrl -Headers @{ 'User-Agent' = 'UpdateChecker' }

# Find the .msixbundle asset
$bundleAsset = $release.assets | Where-Object { $_.name -like '*.msixbundle' } | Select-Object -First 1
if (-not $bundleAsset) { throw 'msixbundle not found in release' }

# Also need VCLibs and UI.Xaml dependencies
$vcLibsUrl = 'https://aka.ms/Microsoft.VCLibs.x64.14.00.Desktop.appx'
$xamlUrl = ($release.assets | Where-Object { $_.name -like '*Microsoft.UI.Xaml*.appx' -and $_.name -like '*x64*' } | Select-Object -First 1).browser_download_url

$bundlePath = Join-Path $tempDir $bundleAsset.name
$vcLibsPath = Join-Path $tempDir 'VCLibs.appx'

Write-Host 'Downloading VCLibs...'
Invoke-WebRequest -Uri $vcLibsUrl -OutFile $vcLibsPath -UseBasicParsing

if ($xamlUrl) {
    $xamlPath = Join-Path $tempDir 'UIXaml.appx'
    Write-Host 'Downloading UI.Xaml...'
    Invoke-WebRequest -Uri $xamlUrl -OutFile $xamlPath -UseBasicParsing
}

Write-Host 'Downloading winget...'
Invoke-WebRequest -Uri $bundleAsset.browser_download_url -OutFile $bundlePath -UseBasicParsing

Write-Host 'Installing dependencies...'
Add-AppxPackage -Path $vcLibsPath -ErrorAction SilentlyContinue
if ($xamlUrl) { Add-AppxPackage -Path $xamlPath -ErrorAction SilentlyContinue }

Write-Host 'Installing winget...'
Add-AppxPackage -Path $bundlePath -ForceApplicationShutdown

Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host 'OK'
";

            var psi = new ProcessStartInfo("powershell.exe")
            {
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command -",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi)!;
            proc.StandardInput.Write(script);
            proc.StandardInput.Close();

            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode != 0)
            {
                log?.Invoke($"winget-Installation fehlgeschlagen (Exit {proc.ExitCode}): {stderr.Trim()}");
                return null;
            }

            log?.Invoke("winget erfolgreich installiert.");

            // Try to find winget again
            // Small delay to let the system register the package
            System.Threading.Thread.Sleep(2000);
            return FindWinget();
        }
        catch (Exception ex)
        {
            log?.Invoke($"winget-Installation Fehler: {ex.Message}");
            return null;
        }
    }

    public static (string Output, int ExitCode) Run(string wingetPath, params string[] arguments)
    {
        var psi = new ProcessStartInfo(wingetPath)
        {
            Arguments = string.Join(" ", arguments),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        return (stdout, proc.ExitCode);
    }
}
