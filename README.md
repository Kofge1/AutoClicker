# Kofge-Clicker

### Advanced Windows Auto Clicker with Humanized Clicking, Profiles and Window Targeting

Free • Portable • Open Source • Built with C#

[![Download](https://img.shields.io/badge/Download-Latest%20Release-success?style=for-the-badge)](https://github.com/Kofge1/Kofge-Clicker/releases/latest)

![Platform](https://img.shields.io/badge/Windows-10%20%7C%2011-blue?style=for-the-badge) ![.NET](https://img.shields.io/badge/.NET-8-purple?style=for-the-badge) ![License](https://img.shields.io/github/license/Kofge1/Kofge-Clicker?style=for-the-badge)

<img width="1099" height="634" alt="image" src="https://github.com/user-attachments/assets/4df56db5-5a00-4de8-8fad-e1312fd5bb8a" />


Kofge-Clicker is a free and open-source Windows auto clicker designed for precision, flexibility and ease of use.

It includes advanced click patterns, humanized clicking, profile management, customizable hotkeys and window targeting while remaining lightweight and fully portable.

## Gallery

<img width="1098" height="634" alt="image" src="https://github.com/user-attachments/assets/e78e88ae-afcf-4da7-9c4c-99a5951642f0" />

<img width="1100" height="634" alt="image" src="https://github.com/user-attachments/assets/bab677c3-c778-4fde-9826-49c95cac533a" />

<img width="1098" height="634" alt="image" src="https://github.com/user-attachments/assets/e55a5567-2996-43d7-9260-8cfc700de74e" />

<img width="1099" height="634" alt="image" src="https://github.com/user-attachments/assets/1a9b03f1-e1c9-4e9d-95e3-faa29e6c5437" />

## Features

### Clicking
- Adjustable CPS (1–100)
- Humanized Clicking
- Standard
- Burst
- Double
- Hold+Burst

---

### Profiles
- Save unlimited profiles
- Switch instantly

---

### Automation
- Window Targeting
- Toggle Mode
- Hold Mode
- Left / Right Mouse

---

### Hotkeys
- Panic Stop
- Toggle
- Show Window
- Next Profile

## Download

[![Download Latest Release](https://img.shields.io/github/v/release/Kofge1/AutoClicker?label=Download%20Latest%20Release&style=for-the-badge)](https://github.com/Kofge1/AutoClicker/releases/latest)

Download the latest Windows build from the GitHub Releases page:

➡️ **[Download Kofge-Clicker](https://github.com/Kofge1/AutoClicker/releases/latest)**

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

Kofge-Clicker stores profiles, settings and logs here:

```text
%LocalAppData%\AutoClicker
```

This folder contains:

- `settings.ini` for settings and profiles.
- `startup.log` for startup diagnostics.
- `input-diagnostics.log` for input/click diagnostics when events are recorded.

## Support

This project is currently free to use.

If you want to support development, you can donate via USDT:

```text
ERC20 - 0x5701793453c1d73a527af74f9b615717052c4738
TRC20 - TMTvgkSzEARmZ81HG2SE7nRf2KbC63tcBJ
```

Please double-check the network before sending. Transfers sent through the wrong network may be lost.

## Important Note

Use this tool only in apps, games and workflows where automation is allowed. Some games, apps and online services restrict auto clickers, macros or automated input.
