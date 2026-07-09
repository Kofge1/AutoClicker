namespace KofgeClicker;

internal static class AppPaths
{
    private const string AppFolderName = "Kofge-Clicker";
    private const string LegacyAppFolderName = "AutoClicker";

    internal static string DataDirectory
    {
        get
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppFolderName);
            MigrateLegacyDataDirectory(directory);
            Directory.CreateDirectory(directory);
            return directory;
        }
    }

    internal static string SettingsPath => Path.Combine(DataDirectory, "settings.ini");

    internal static string StartupLogPath => Path.Combine(DataDirectory, "startup.log");

    internal static string InputDiagnosticsLogPath => Path.Combine(DataDirectory, "input-diagnostics.log");

    internal static string ExecutableSettingsPath => Path.Combine(AppContext.BaseDirectory, "settings.ini");

    internal static string LegacySettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        "Clicker",
        "settings.ini");

    private static void MigrateLegacyDataDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            return;
        }

        var legacyDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            LegacyAppFolderName);
        if (!Directory.Exists(legacyDirectory))
        {
            return;
        }

        try
        {
            CopyDirectory(legacyDirectory, directory);
        }
        catch
        {
        }
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory))
        {
            var targetFile = Path.Combine(targetDirectory, Path.GetFileName(sourceFile));
            if (!File.Exists(targetFile))
            {
                File.Copy(sourceFile, targetFile);
            }
        }

        foreach (var sourceSubdirectory in Directory.EnumerateDirectories(sourceDirectory))
        {
            var targetSubdirectory = Path.Combine(targetDirectory, Path.GetFileName(sourceSubdirectory));
            CopyDirectory(sourceSubdirectory, targetSubdirectory);
        }
    }
}
