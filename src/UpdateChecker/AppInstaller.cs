using System;
using System.IO;
using System.Runtime.InteropServices;

namespace UpdateChecker;

public static class AppInstaller
{
    public static string InstallDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "UpdateChecker");

    public static string LogDir => Path.Combine(InstallDir, "logs");

    public static string InstalledExePath => Path.Combine(InstallDir, "UpdateChecker.exe");

    public static bool IsInstalled =>
        File.Exists(InstalledExePath) && TaskSchedulerManager.TaskExists();

    public static (bool Success, string Message) Install()
    {
        try
        {
            // Create directories
            Directory.CreateDirectory(InstallDir);
            Directory.CreateDirectory(LogDir);

            // Copy exe
            var currentExe = Environment.ProcessPath!;
            var targetExe = InstalledExePath;

            if (!string.Equals(Path.GetFullPath(currentExe), Path.GetFullPath(targetExe), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(currentExe, targetExe, overwrite: true);
            }

            // Create scheduled task
            var (taskOk, taskError) = TaskSchedulerManager.CreateTask(targetExe);
            if (!taskOk)
                return (false, $"Task Scheduler Fehler: {taskError}");

            // Create desktop shortcut
            CreateDesktopShortcut(targetExe);

            return (true, $"Installiert nach {InstallDir}");
        }
        catch (Exception ex)
        {
            return (false, $"Fehler: {ex.Message}");
        }
    }

    private static void CreateDesktopShortcut(string targetExe)
    {
        try
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            var shortcutPath = Path.Combine(desktopPath, "UpdateChecker.lnk");

            var type = Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8"))!; // WScript.Shell
            dynamic shell = Activator.CreateInstance(type)!;
            var shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetExe;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetExe);
            shortcut.IconLocation = $"{targetExe},0";
            shortcut.Description = "Windows Update-Checker via winget | Shareflix";
            shortcut.Save();
            Marshal.ReleaseComObject(shortcut);
            Marshal.ReleaseComObject(shell);
        }
        catch { /* best effort */ }
    }
}
