using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace UpdateChecker;

static class Program
{
    // Temp file for communicating results from elevated process back to GUI
    public static string ResultFile =>
        Path.Combine(Path.GetTempPath(), "UpdateChecker_result.txt");

    [STAThread]
    static void Main(string[] args)
    {
        var arg = args.FirstOrDefault()?.ToLowerInvariant();

        switch (arg)
        {
            case "/silent":
                SilentMode.Run();
                break;

            case "/install":
                if (!IsAdmin())
                {
                    RelaunchElevated("/install");
                    return;
                }
                var (installOk, installMsg) = AppInstaller.Install();
                WriteResult(installOk, installMsg);
                Environment.Exit(installOk ? 0 : 1);
                break;

            case "/uninstall":
                if (!IsAdmin())
                {
                    RelaunchElevated("/uninstall");
                    return;
                }
                var (uninstallOk, uninstallMsg) = AppUninstaller.Uninstall();
                WriteResult(uninstallOk, uninstallMsg);
                Environment.Exit(uninstallOk ? 0 : 1);
                break;

            default:
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                break;
        }
    }

    private static void RelaunchElevated(string flag)
    {
        var exePath = Environment.ProcessPath!;
        try
        {
            var psi = new ProcessStartInfo(exePath, flag)
            {
                Verb = "runas",
                UseShellExecute = true
            };
            var proc = Process.Start(psi);
            proc?.WaitForExit();
            Environment.Exit(proc?.ExitCode ?? 1);
        }
        catch
        {
            Environment.Exit(1);
        }
    }

    private static void WriteResult(bool success, string message)
    {
        try { File.WriteAllText(ResultFile, $"{(success ? "OK" : "FAIL")}|{message}"); }
        catch { }
    }

    public static (bool Success, string Message) ReadResult()
    {
        try
        {
            if (!File.Exists(ResultFile)) return (false, "Keine Rueckmeldung vom Prozess.");
            var content = File.ReadAllText(ResultFile);
            File.Delete(ResultFile);
            var parts = content.Split('|', 2);
            return (parts[0] == "OK", parts.Length > 1 ? parts[1] : "");
        }
        catch { return (false, "Ergebnis konnte nicht gelesen werden."); }
    }

    private static bool IsAdmin()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}
