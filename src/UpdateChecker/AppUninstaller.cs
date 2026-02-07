using System;
using System.Diagnostics;
using System.IO;

namespace UpdateChecker;

public static class AppUninstaller
{
    public static (bool Success, string Message) Uninstall()
    {
        try
        {
            // Delete scheduled task
            TaskSchedulerManager.DeleteTask();

            // Delete desktop shortcut
            try
            {
                var shortcut = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                    "UpdateChecker.lnk");
                if (File.Exists(shortcut))
                    File.Delete(shortcut);
            }
            catch { }

            var installDir = AppInstaller.InstallDir;

            // Delete logs
            var logDir = AppInstaller.LogDir;
            if (Directory.Exists(logDir))
                Directory.Delete(logDir, recursive: true);

            // Delete all files except the running exe
            var currentExe = Path.GetFullPath(Environment.ProcessPath!);
            if (Directory.Exists(installDir))
            {
                foreach (var file in Directory.GetFiles(installDir))
                {
                    if (!string.Equals(Path.GetFullPath(file), currentExe, StringComparison.OrdinalIgnoreCase))
                        File.Delete(file);
                }
            }

            // Self-delete via cmd.exe (delayed)
            var isRunningFromInstallDir = currentExe.StartsWith(installDir, StringComparison.OrdinalIgnoreCase);
            if (isRunningFromInstallDir)
            {
                var cmdArgs = $"/c timeout /t 2 /nobreak >nul & del /f /q \"{currentExe}\" >nul 2>&1 & rmdir /q \"{installDir}\" >nul 2>&1";
                Process.Start(new ProcessStartInfo("cmd.exe", cmdArgs)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            else
            {
                // Not running from install dir - can delete directly
                if (Directory.Exists(installDir))
                    Directory.Delete(installDir, recursive: true);
            }

            return (true, "UpdateChecker wurde deinstalliert.");
        }
        catch (Exception ex)
        {
            return (false, $"Fehler: {ex.Message}");
        }
    }
}
