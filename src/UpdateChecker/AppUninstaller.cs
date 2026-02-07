using System;
using System.Diagnostics;
using System.IO;

namespace UpdateChecker;

public static class AppUninstaller
{
    public static (bool Success, string Message) Uninstall(int guiPid = 0)
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

            // Delete install directory
            var installDir = AppInstaller.InstallDir;
            if (Directory.Exists(installDir))
            {
                try
                {
                    Directory.Delete(installDir, recursive: true);
                }
                catch
                {
                    // EXE locked - write a .cmd cleanup script to %TEMP% and launch it
                    // via explorer.exe (fully independent process, inherits no parent).
                    ScheduleDirectoryCleanup(installDir, guiPid);
                }
            }

            return (true, "UpdateChecker wurde deinstalliert.");
        }
        catch (Exception ex)
        {
            return (false, $"Fehler: {ex.Message}");
        }
    }

    private static void ScheduleDirectoryCleanup(string installDir, int guiPid)
    {
        var script = Path.Combine(Path.GetTempPath(), "UpdateChecker_cleanup.cmd");

        // Build a .cmd script that:
        // 1. Waits for all UpdateChecker processes to exit (polls via tasklist)
        // 2. Tries to delete the install directory
        // 3. Cleans up the script itself
        var bat =
@$"@echo off
:waitloop
tasklist /FI ""IMAGENAME eq UpdateChecker.exe"" 2>nul | find /I ""UpdateChecker.exe"" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto waitloop
)
timeout /t 2 /nobreak >nul
rd /s /q ""{installDir}"" >nul 2>&1
timeout /t 1 /nobreak >nul
rmdir ""{installDir}"" >nul 2>&1
if exist ""{installDir}"" (
    timeout /t 3 /nobreak >nul
    rd /s /q ""{installDir}"" >nul 2>&1
    timeout /t 1 /nobreak >nul
    rmdir ""{installDir}"" >nul 2>&1
)
if exist ""{installDir}"" (
    timeout /t 3 /nobreak >nul
    rd /s /q ""{installDir}"" >nul 2>&1
    timeout /t 1 /nobreak >nul
    rmdir ""{installDir}"" >nul 2>&1
)
del /f /q ""%~f0"" >nul 2>&1
";

        File.WriteAllText(script, bat);

        // Launch via cmd /c with UseShellExecute=true so it's a fully independent process
        // that survives our exit. It inherits admin rights from this elevated process.
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }
}
