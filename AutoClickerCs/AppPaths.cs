namespace AutoClickerCs;

internal static class AppPaths
{
    private const string AppFolderName = "AutoClicker";

    internal static string DataDirectory
    {
        get
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppFolderName);
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
}
