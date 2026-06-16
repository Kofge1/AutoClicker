# AutoClicker

A lightweight Windows auto clicker with profiles, hotkeys, humanized CPS, click patterns, tray status indicator and window targeting.

## Features

- Adjustable CPS from 1 to 100.
- Hold and Toggle modes.
- Left and right mouse button automation.
- Humanized clicking.
- Click patterns: Standard, Burst, Double and Hold+Burst.
- Rate behavior: Locked and Amplified.
- Profiles for saving and switching setups.
- Custom hotkeys for Panic Stop, Show Window and Toggle Power.
- Window targeting: click only while a selected app/window is active.
- Tray support with an ON/OFF status indicator.
- Settings, profiles and logs are stored in `%LocalAppData%\AutoClicker`, so the executable folder stays clean.

## Download

The easiest way to use the app is to download the latest build from the GitHub Releases page.

If Windows SmartScreen appears, choose **More info** and **Run anyway** only if you trust this project and downloaded it from the official repository.

## Build From Source

Requirements:

- Windows 10 or Windows 11.
- .NET 8 SDK.

Build:

```powershell
dotnet build .\AutoClickerCs\AutoClickerCs.csproj
```

Publish a self-contained Windows executable:

```powershell
dotnet publish .\AutoClickerCs\AutoClickerCs.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish
```

## Data Location

AutoClicker stores profiles, settings and logs here:

```text
%LocalAppData%\AutoClicker
```

This folder contains:

- `settings.ini` for settings and profiles.
- `startup.log` for startup diagnostics.
- `input-diagnostics.log` for input/click diagnostics when events are recorded.

## Support

If this app is useful to you, you can support development on Gumroad - https://kofge.gumroad.com/l/nkpwuc

## Important Note

Use this tool only in apps, games and workflows where automation is allowed. Some games, apps and online services restrict auto clickers, macros or automated input.
