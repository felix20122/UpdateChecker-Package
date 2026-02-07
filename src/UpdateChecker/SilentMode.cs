using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;

namespace UpdateChecker;

public static class SilentMode
{
    public static void Run()
    {
        var logDir = AppInstaller.LogDir;
        System.IO.Directory.CreateDirectory(logDir);

        using var logger = new FileLogger(logDir);
        logger.CleanOldLogs(logDir);
        logger.Log("Update-Checker gestartet (Silent Mode)");

        // Wait for network
        logger.Log("Warte auf Netzwerk (max. 30s)...");
        if (!WaitForNetwork(30))
        {
            logger.Log("Kein Netzwerk verfuegbar!", "ERROR");
            ToastNotifier.Show("Update-Checker Fehler", "Keine Netzwerkverbindung.");
            return;
        }
        logger.Log("Netzwerk verfuegbar.");

        // Self-update check
        logger.Log("Pruefe auf UpdateChecker-Eigenupdates...");
        try
        {
            var (newVersion, downloadUrl) = SelfUpdater.CheckForUpdateAsync(msg => logger.Log(msg))
                .GetAwaiter().GetResult();

            if (newVersion != null && downloadUrl != null)
            {
                logger.Log($"Neue Version {newVersion} gefunden – lade herunter...");
                var downloaded = SelfUpdater.DownloadUpdateAsync(downloadUrl, msg => logger.Log(msg))
                    .GetAwaiter().GetResult();

                if (downloaded)
                {
                    logger.Log("Wende Self-Update an...");
                    var (ok, msg) = SelfUpdater.ApplyUpdate(System.Diagnostics.Process.GetCurrentProcess().Id);
                    logger.Log(ok ? "Self-Update gestartet. Beende Prozess." : $"Self-Update fehlgeschlagen: {msg}");

                    if (ok)
                    {
                        logger.Log("Update-Checker beendet (Self-Update).");
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Log($"Self-Update Fehler: {ex.Message}");
        }

        // Find winget
        var winget = WingetRunner.FindWinget();
        if (winget == null)
        {
            logger.Log("winget nicht gefunden – versuche Installation...");
            winget = WingetRunner.InstallWinget(msg => logger.Log(msg));
            if (winget == null)
            {
                logger.Log("winget konnte nicht installiert werden!", "ERROR");
                ToastNotifier.Show("Update-Checker Fehler",
                    "winget nicht verfuegbar und konnte nicht installiert werden!");
                return;
            }
        }
        logger.Log($"winget: {winget}");

        // Check for updates
        logger.Log("Pruefe auf Updates...");
        var (checkOutput, _) = WingetRunner.Run(winget, "upgrade", "--include-unknown");
        var updates = UpdateParser.Parse(checkOutput);

        if (updates.Count == 0)
        {
            logger.Log("Alles aktuell.");
            return;
        }

        logger.Log($"{updates.Count} Update(s) gefunden:");
        foreach (var u in updates.Take(10))
            logger.Log($"  -> {u}");

        // Install updates
        logger.Log("Starte Installation...");
        var (installOutput, _) = WingetRunner.Run(winget,
            "upgrade", "--all",
            "--accept-package-agreements",
            "--accept-source-agreements",
            "--include-unknown");
        logger.Log("Installation abgeschlossen.");
        logger.Log(installOutput);

        // Toast notification
        var preview = string.Join("\n", updates.Take(3));
        var more = updates.Count > 3 ? $"\n...und {updates.Count - 3} weitere" : "";
        ToastNotifier.Show("Updates installiert", preview + more);

        logger.Log("Update-Checker beendet.");
    }

    private static bool WaitForNetwork(int timeoutSeconds)
    {
        var deadline = DateTime.Now.AddSeconds(timeoutSeconds);
        while (DateTime.Now < deadline)
        {
            if (NetworkInterface.GetIsNetworkAvailable())
                return true;
            Thread.Sleep(1000);
        }
        return NetworkInterface.GetIsNetworkAvailable();
    }
}
