using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UpdateChecker;

public sealed class MainForm : Form
{
    private readonly Label _installStatusLabel;
    private readonly Label _taskStatusLabel;
    private readonly Label _lastRunLabel;
    private readonly Label _installPathLabel;
    private readonly Button _installButton;
    private readonly Button _uninstallButton;
    private readonly Button _checkNowButton;
    private readonly TextBox _logBox;

    public MainForm()
    {
        Text = "UpdateChecker";
        Size = new Size(600, 520);
        MinimumSize = new Size(500, 420);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.FromArgb(245, 245, 245);
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);

        // --- Title ---
        var titleLabel = new Label
        {
            Text = "UpdateChecker",
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 30, 30),
            AutoSize = true,
            Location = new Point(20, 16)
        };

        var subtitleLabel = new Label
        {
            Text = "Automatische Windows-Updates via winget  |  Shareflix",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(120, 120, 120),
            AutoSize = true,
            Location = new Point(22, 48)
        };

        // --- Status GroupBox ---
        var statusGroup = new GroupBox
        {
            Text = "Status",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(60, 60, 60),
            Location = new Point(20, 76),
            Size = new Size(0, 110),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var statusFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        _installStatusLabel = new Label { AutoSize = true, Font = statusFont, Location = new Point(12, 24) };
        _taskStatusLabel = new Label { AutoSize = true, Font = statusFont, Location = new Point(12, 46) };
        _lastRunLabel = new Label { AutoSize = true, Font = statusFont, Location = new Point(12, 68) };
        _installPathLabel = new Label { AutoSize = true, Font = statusFont, ForeColor = Color.FromArgb(100, 100, 100), Location = new Point(12, 90) };

        statusGroup.Controls.AddRange(new Control[] { _installStatusLabel, _taskStatusLabel, _lastRunLabel, _installPathLabel });

        // --- Buttons ---
        _installButton = CreateButton("Install", Color.FromArgb(0, 120, 212));
        _installButton.Location = new Point(20, 196);
        _installButton.Click += OnInstallClick;

        _uninstallButton = CreateButton("Uninstall", Color.FromArgb(200, 60, 60));
        _uninstallButton.Location = new Point(140, 196);
        _uninstallButton.Click += OnUninstallClick;

        _checkNowButton = CreateButton("Check Now", Color.FromArgb(16, 124, 16));
        _checkNowButton.Location = new Point(260, 196);
        _checkNowButton.Click += OnCheckNowClick;

        // --- Log ---
        var logLabel = new Label
        {
            Text = "Log",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(60, 60, 60),
            AutoSize = true,
            Location = new Point(20, 240)
        };

        _logBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Cascadia Mono, Consolas", 9f),
            BackColor = Color.FromArgb(25, 25, 25),
            ForeColor = Color.FromArgb(210, 210, 210),
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(20, 262),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        Controls.AddRange(new Control[] { titleLabel, subtitleLabel, statusGroup, _installButton, _uninstallButton, _checkNowButton, logLabel, _logBox });

        // Initial sizing
        Resize += (_, _) => AdjustLayout();
        AdjustLayout();
        RefreshStatus();
    }

    private void AdjustLayout()
    {
        var w = ClientSize.Width;
        var h = ClientSize.Height;

        var statusGroup = Controls.OfType<GroupBox>().First();
        statusGroup.Width = w - 40;

        _logBox.Width = w - 40;
        _logBox.Height = h - _logBox.Top - 16;
    }

    private static Button CreateButton(string text, Color color)
    {
        var btn = new Button
        {
            Text = text,
            Size = new Size(110, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private void RefreshStatus()
    {
        var installed = File.Exists(AppInstaller.InstalledExePath);
        var taskExists = TaskSchedulerManager.TaskExists();

        _installStatusLabel.Text = installed ? "Installiert" : "Nicht installiert";
        _installStatusLabel.ForeColor = installed ? Color.FromArgb(16, 124, 16) : Color.FromArgb(200, 60, 60);

        _taskStatusLabel.Text = taskExists
            ? $"Task: {TaskSchedulerManager.TaskName} (aktiv)"
            : "Task: nicht vorhanden";
        _taskStatusLabel.ForeColor = taskExists ? Color.FromArgb(16, 124, 16) : Color.FromArgb(200, 60, 60);

        _lastRunLabel.Text = "Letzter Lauf: -";
        _lastRunLabel.ForeColor = Color.FromArgb(60, 60, 60);
        if (Directory.Exists(AppInstaller.LogDir))
        {
            var newest = Directory.GetFiles(AppInstaller.LogDir, "*.log")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();
            if (newest != null)
                _lastRunLabel.Text = $"Letzter Lauf: {newest.LastWriteTime:dd.MM.yyyy HH:mm}";
        }

        _installPathLabel.Text = $"Pfad: {AppInstaller.InstallDir}";

        _installButton.Enabled = !installed || !taskExists;
        _uninstallButton.Enabled = installed || taskExists;
    }

    private void AppendLog(string text)
    {
        if (InvokeRequired)
        {
            Invoke(() => AppendLog(text));
            return;
        }
        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
    }

    private async void OnInstallClick(object? sender, EventArgs e)
    {
        _installButton.Enabled = false;
        AppendLog("Starte Installation (erfordert Admin-Rechte)...");
        await RunElevatedAndReport("/install");
        RefreshStatus();
    }

    private async void OnUninstallClick(object? sender, EventArgs e)
    {
        if (MessageBox.Show("UpdateChecker wirklich deinstallieren?", "Deinstallation",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _uninstallButton.Enabled = false;
        AppendLog("Starte Deinstallation (erfordert Admin-Rechte)...");

        // Pass our PID so the elevated cleanup PowerShell can wait for us to exit
        var success = await RunElevatedAndReport($"/uninstall {Environment.ProcessId}");

        if (success)
        {
            Application.Exit();
            return;
        }

        RefreshStatus();
    }

    private async Task<bool> RunElevatedAndReport(string flag)
    {
        var exePath = Environment.ProcessPath!;
        var exitCode = await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo(exePath, flag)
                {
                    Verb = "runas",
                    UseShellExecute = true
                };
                var proc = Process.Start(psi);
                proc?.WaitForExit();
                return proc?.ExitCode ?? 1;
            }
            catch { return 1; }
        });

        // Read detailed result from temp file
        var (success, message) = Program.ReadResult();
        if (success)
            AppendLog(message);
        else if (exitCode != 0)
            AppendLog($"Fehlgeschlagen: {message}");
        else
            AppendLog("Abgeschlossen.");

        return success;
    }

    private async void OnCheckNowClick(object? sender, EventArgs e)
    {
        _checkNowButton.Enabled = false;
        AppendLog("Suche winget...");

        await Task.Run(() =>
        {
            var winget = WingetRunner.FindWinget();
            if (winget == null)
            {
                AppendLog("FEHLER: winget nicht gefunden!");
                return;
            }
            AppendLog($"winget: {winget}");
            AppendLog("Pruefe auf Updates...");

            var (output, _) = WingetRunner.Run(winget, "upgrade", "--include-unknown");
            var updates = UpdateParser.Parse(output);

            if (updates.Count == 0)
            {
                AppendLog("Alles aktuell - keine Updates verfuegbar.");
                return;
            }

            AppendLog($"{updates.Count} Update(s) gefunden:");
            foreach (var u in updates.Take(15))
                AppendLog($"  -> {u}");

            AppendLog("Starte Installation...");
            var (installOutput, _) = WingetRunner.Run(winget,
                "upgrade", "--all",
                "--accept-package-agreements",
                "--accept-source-agreements",
                "--include-unknown");

            var lines = installOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines.TakeLast(5))
                AppendLog(line.Trim());

            AppendLog("Updates abgeschlossen.");
        });

        _checkNowButton.Enabled = true;
    }
}
