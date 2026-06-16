namespace AutoClickerCs;

internal static class InputDiagnostics
{
    private const long MaxLogBytes = 256 * 1024;
    private static readonly object Sync = new();
    private static readonly string LogPath = AppPaths.InputDiagnosticsLogPath;

    internal static void Write(string message)
    {
        try
        {
            lock (Sync)
            {
                RotateIfNeeded();
                File.AppendAllText(
                    LogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
        }
    }

    private static void RotateIfNeeded()
    {
        var file = new FileInfo(LogPath);
        if (!file.Exists || file.Length <= MaxLogBytes)
        {
            return;
        }

        var oldPath = Path.ChangeExtension(LogPath, ".old.log");
        if (File.Exists(oldPath))
        {
            File.Delete(oldPath);
        }

        File.Move(LogPath, oldPath);
    }
}
