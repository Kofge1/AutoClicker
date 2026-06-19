using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace AutoClickerCs;

internal static class AdminLaunchHelper
{
    internal static bool RelaunchAsAdministratorIfRequested(Action<string> log)
    {
        if (IsAdministrator() || !IsAdminLaunchRequested())
        {
            return false;
        }

        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            log("Admin relaunch skipped: process path is empty");
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(processPath)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            });
            log("Admin relaunch requested");
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            log("Admin relaunch canceled by user");
            return false;
        }
        catch (Exception ex)
        {
            log($"Admin relaunch failed: {ex.Message}");
            return false;
        }
    }

    private static bool IsAdminLaunchRequested()
    {
        var settingsPath = ResolveSettingsPath();
        if (!File.Exists(settingsPath))
        {
            return false;
        }

        return new IniFile(settingsPath).ReadBool("Main", "RunAsAdministrator");
    }

    private static string ResolveSettingsPath()
    {
        if (File.Exists(AppPaths.SettingsPath))
        {
            return AppPaths.SettingsPath;
        }

        if (File.Exists(AppPaths.ExecutableSettingsPath))
        {
            return AppPaths.ExecutableSettingsPath;
        }

        if (File.Exists(AppPaths.LegacySettingsPath))
        {
            return AppPaths.LegacySettingsPath;
        }

        return AppPaths.SettingsPath;
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
