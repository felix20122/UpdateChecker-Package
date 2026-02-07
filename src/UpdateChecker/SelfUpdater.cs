using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace UpdateChecker;

public static class SelfUpdater
{
    private const string GitHubApiUrl =
        "https://api.github.com/repos/felix20122/UpdateChecker-Package/releases/latest";
    private const string AssetName = "UpdateChecker.exe";

    public static string TempUpdatePath =>
        Path.Combine(Path.GetTempPath(), "UpdateChecker_update.exe");

    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version
        ?? new Version(0, 0, 0);

    /// <summary>
    /// Checks GitHub for the latest release. Returns (newVersion, downloadUrl)
    /// or (null, null) if already up to date or on error.
    /// </summary>
    public static async Task<(Version? NewVersion, string? DownloadUrl)> CheckForUpdateAsync(
        Action<string>? log = null)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("UpdateChecker");

            log?.Invoke("Pruefe auf neue Version...");
            var json = await http.GetStringAsync(GitHubApiUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var versionString = tagName.TrimStart('v', 'V');

            if (!Version.TryParse(versionString, out var latestVersion))
            {
                log?.Invoke($"Konnte Version nicht parsen: {tagName}");
                return (null, null);
            }

            log?.Invoke($"Aktuelle Version: {CurrentVersion}");
            log?.Invoke($"Neueste Version:  {latestVersion}");

            if (latestVersion <= CurrentVersion)
            {
                log?.Invoke("Bereits aktuell.");
                return (null, null);
            }

            // Find the .exe asset in the release
            var assets = root.GetProperty("assets");
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                if (string.Equals(name, AssetName, StringComparison.OrdinalIgnoreCase))
                {
                    var url = asset.GetProperty("browser_download_url").GetString();
                    return (latestVersion, url);
                }
            }

            log?.Invoke("FEHLER: UpdateChecker.exe nicht im Release gefunden.");
            return (null, null);
        }
        catch (Exception ex)
        {
            log?.Invoke($"Update-Pruefung fehlgeschlagen: {ex.Message}");
            return (null, null);
        }
    }

    /// <summary>
    /// Downloads the new exe to %TEMP% with progress logging.
    /// </summary>
    public static async Task<bool> DownloadUpdateAsync(
        string downloadUrl, Action<string>? log = null)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("UpdateChecker");

            log?.Invoke("Lade Update herunter...");

            using var response = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = File.Create(TempUpdatePath);

            var buffer = new byte[81920];
            long downloaded = 0;
            int lastPercent = -1;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                downloaded += bytesRead;

                if (totalBytes > 0)
                {
                    var percent = (int)(downloaded * 100 / totalBytes);
                    if (percent != lastPercent && percent % 10 == 0)
                    {
                        log?.Invoke($"Download: {percent}% ({downloaded / 1024 / 1024} MB)");
                        lastPercent = percent;
                    }
                }
            }

            log?.Invoke($"Download abgeschlossen ({downloaded / 1024 / 1024} MB).");
            return true;
        }
        catch (Exception ex)
        {
            log?.Invoke($"Download fehlgeschlagen: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Called by the elevated /selfupdate handler.
    /// Creates a .cmd script that waits for all UpdateChecker processes to exit,
    /// copies the new exe over the installed one, relaunches, and self-deletes.
    /// </summary>
    public static (bool Success, string Message) ApplyUpdate(int guiPid)
    {
        try
        {
            var installedExe = AppInstaller.InstalledExePath;
            var tempExe = TempUpdatePath;

            if (!File.Exists(tempExe))
                return (false, "Update-Datei nicht gefunden.");

            var script = Path.Combine(Path.GetTempPath(), "UpdateChecker_selfupdate.cmd");
            var bat =
@$"@echo off
:waitloop
tasklist /FI ""IMAGENAME eq UpdateChecker.exe"" 2>nul | find /I ""UpdateChecker.exe"" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto waitloop
)
timeout /t 2 /nobreak >nul
copy /y ""{tempExe}"" ""{installedExe}"" >nul 2>&1
if errorlevel 1 (
    timeout /t 2 /nobreak >nul
    copy /y ""{tempExe}"" ""{installedExe}"" >nul 2>&1
)
del /f /q ""{tempExe}"" >nul 2>&1
start """" ""{installedExe}""
del /f /q ""%~f0"" >nul 2>&1
";
            File.WriteAllText(script, bat);

            Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            return (true, "Update wird angewendet...");
        }
        catch (Exception ex)
        {
            return (false, $"Update-Fehler: {ex.Message}");
        }
    }
}
