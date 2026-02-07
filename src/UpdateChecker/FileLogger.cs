using System;
using System.IO;
using System.Linq;

namespace UpdateChecker;

public sealed class FileLogger : IDisposable
{
    private const int RetentionDays = 30;
    private readonly StreamWriter _writer;
    public string LogFilePath { get; }

    public FileLogger(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        var fileName = $"update-check_{DateTime.Now:yyyy-MM-dd_HHmmss}.log";
        LogFilePath = Path.Combine(logDirectory, fileName);
        _writer = new StreamWriter(LogFilePath, append: true) { AutoFlush = true };
    }

    public void Log(string message, string level = "INFO")
    {
        var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
        _writer.WriteLine(entry);
    }

    public void CleanOldLogs(string logDirectory)
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-RetentionDays);
            foreach (var file in Directory.GetFiles(logDirectory, "*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch { /* best effort */ }
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
