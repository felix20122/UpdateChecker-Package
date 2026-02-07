using System.Diagnostics;

namespace UpdateChecker;

public static class TaskSchedulerManager
{
    public const string TaskName = "UpdateChecker-Login";

    public static bool TaskExists()
    {
        var psi = new ProcessStartInfo("schtasks", $"/Query /TN \"{TaskName}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi)!;
        proc.WaitForExit();
        return proc.ExitCode == 0;
    }

    public static (bool Success, string Error) CreateTask(string exePath)
    {
        // Delete existing task first
        DeleteTask();

        // Build argument list for schtasks
        // Use ProcessStartInfo.ArgumentList to avoid quoting hell
        var psi = new ProcessStartInfo("schtasks")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("/Create");
        psi.ArgumentList.Add("/TN");
        psi.ArgumentList.Add(TaskName);
        psi.ArgumentList.Add("/TR");
        psi.ArgumentList.Add($"\"{exePath}\" /silent");
        psi.ArgumentList.Add("/SC");
        psi.ArgumentList.Add("ONLOGON");
        psi.ArgumentList.Add("/RL");
        psi.ArgumentList.Add("HIGHEST");
        psi.ArgumentList.Add("/F");

        using var proc = Process.Start(psi)!;
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        return (proc.ExitCode == 0, stderr.Trim());
    }

    public static bool DeleteTask()
    {
        var psi = new ProcessStartInfo("schtasks", $"/Delete /TN \"{TaskName}\" /F")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi)!;
        proc.WaitForExit();
        return proc.ExitCode == 0;
    }

    public static string? GetTaskInfo()
    {
        var psi = new ProcessStartInfo("schtasks", $"/Query /TN \"{TaskName}\" /FO LIST")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi)!;
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        return proc.ExitCode == 0 ? output : null;
    }
}
