using System;
using System.Drawing;
using System.Windows.Forms;

namespace UpdateChecker;

public static class ToastNotifier
{
    public static void Show(string title, string message)
    {
        try
        {
            ShowModernToast(title, message);
        }
        catch
        {
            ShowFallbackToast(title, message);
        }
    }

    private static void ShowModernToast(string title, string message)
    {
        var builder = new Microsoft.Toolkit.Uwp.Notifications.ToastContentBuilder()
            .AddText(title)
            .AddText(message)
            .AddButton(new Microsoft.Toolkit.Uwp.Notifications.ToastButton()
                .SetDismissActivation());

        builder.Show();
    }

    private static void ShowFallbackToast(string title, string message)
    {
        try
        {
            var icon = new NotifyIcon
            {
                Icon = SystemIcons.Information,
                BalloonTipIcon = ToolTipIcon.Info,
                BalloonTipTitle = title,
                BalloonTipText = message,
                Visible = true
            };
            icon.ShowBalloonTip(10000);
            System.Threading.Thread.Sleep(15000);
            icon.Dispose();
        }
        catch { }
    }
}
