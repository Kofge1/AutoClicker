using System.Diagnostics;

namespace KofgeClicker;

public sealed partial class MainForm
{
    private void StartUpdateCheck()
    {
        _ = CheckForUpdatesOnStartupAsync();
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        var update = await UpdateChecker.CheckForUpdateAsync(AppVersion.Display, CancellationToken.None);
        if (update is null || IsDisposed)
        {
            return;
        }

        BeginInvoke(new Action(() => ShowUpdateAvailable(update)));
    }

    private void ShowUpdateAvailable(UpdateInfo update)
    {
        if (IsDisposed)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"A new Kofge-Clicker version is available: {update.TagName}\n\nCurrent version: {AppVersion.Display}\n\nOpen GitHub Releases page?",
            "Update available",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(update.ReleaseUrl)
            {
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }
}
