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
