namespace KofgeClicker;

public sealed class AppSettings
{
    public bool AutoEnabled { get; set; }
    public string CurrentMode { get; set; } = "hold";
    public string TriggerKey { get; set; } = "F2";
    public string PanicHotkey { get; set; } = "F12";
    public string ShowWindowHotkey { get; set; } = "F10";
    public string TogglePowerHotkey { get; set; } = "F7";
    public string ProfileHotkey { get; set; } = "F9";
    public bool StartMinimized { get; set; }
    public bool MinimizeToTrayOnMinimize { get; set; }
    public bool RememberLastProfile { get; set; }
    public bool RunOnWindowsStartup { get; set; }
    public bool RunAsAdministrator { get; set; }
    public bool CloseToTrayOnClose { get; set; }
    public bool RestrictToFocusedWindow { get; set; }
    public string TargetWindowTitle { get; set; } = string.Empty;
    public string TargetWindowClass { get; set; } = string.Empty;
    public string TargetWindowExe { get; set; } = string.Empty;
    public string ClickButton { get; set; } = "Left";
    public string ClickPattern { get; set; } = "Standard";
    public string ClickRateMode { get; set; } = "Ordinary";
    public int BurstClickCount { get; set; } = 3;
    public int BurstGapMs { get; set; } = 14;
    public int HoldThenBurstHoldMs { get; set; } = 70;
    public int PressDelayMs { get; set; }
    public int ReleaseDelayMs { get; set; }
    public int Cps { get; set; } = 15;
    public bool HumanizedCpsEnabled { get; set; }
    public string HumanizedPreset { get; set; } = "Natural";
}

public sealed class ProfileInfo
{
    public required string Id { get; init; }
    public required string Name { get; set; }
}

public sealed class TargetWindowInfo
{
    public required string Title { get; init; }
    public required string Class { get; init; }
    public required string Exe { get; init; }
    public required string Display { get; init; }
}
