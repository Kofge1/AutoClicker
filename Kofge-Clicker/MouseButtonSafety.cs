using System.Runtime.InteropServices;

namespace KofgeClicker;

internal static class MouseButtonSafety
{
    private static readonly HashSet<string> ClickerPressedButtons = new(StringComparer.Ordinal);
    private static readonly object Sync = new();

    internal static void MarkButtonDown(string buttonName)
    {
        var normalized = NormalizeButtonName(buttonName);
        if (normalized.Length == 0)
        {
            return;
        }

        lock (Sync)
        {
            ClickerPressedButtons.Add(normalized);
        }
    }

    internal static void MarkButtonUp(string buttonName)
    {
        var normalized = NormalizeButtonName(buttonName);
        if (normalized.Length == 0)
        {
            return;
        }

        lock (Sync)
        {
            ClickerPressedButtons.Remove(normalized);
        }
    }

    internal static void ReleaseButton(string buttonName)
    {
        var normalized = NormalizeButtonName(buttonName);
        if (normalized.Length == 0)
        {
            return;
        }

        lock (Sync)
        {
            if (!ClickerPressedButtons.Remove(normalized))
            {
                return;
            }
        }

        SendMouseButtonUp(normalized);
    }

    internal static void ReleaseAllPressedButtons()
    {
        string[] buttonsToRelease;
        lock (Sync)
        {
            buttonsToRelease = [.. ClickerPressedButtons];
            ClickerPressedButtons.Clear();
        }

        foreach (var button in buttonsToRelease)
        {
            SendMouseButtonUp(button);
        }
    }

    internal static bool HasPressedButtons
    {
        get
        {
            lock (Sync)
            {
                return ClickerPressedButtons.Count > 0;
            }
        }
    }

    internal static void ReleaseAllPressedButtonsExcept(string? preservedButton)
    {
        var normalizedPreserved = NormalizeButtonName(preservedButton);
        List<string> buttonsToRelease = [];
        lock (Sync)
        {
            foreach (var button in ClickerPressedButtons)
            {
                if (!string.Equals(button, normalizedPreserved, StringComparison.Ordinal))
                {
                    buttonsToRelease.Add(button);
                }
            }

            foreach (var button in buttonsToRelease)
            {
                ClickerPressedButtons.Remove(button);
            }
        }

        foreach (var button in buttonsToRelease)
        {
            SendMouseButtonUp(button);
        }
    }

    internal static void ForceReleaseButton(string buttonName)
    {
        var normalized = NormalizeButtonName(buttonName);
        if (normalized.Length == 0)
        {
            return;
        }

        lock (Sync)
        {
            ClickerPressedButtons.Remove(normalized);
        }

        SendMouseButtonUp(normalized);
    }

    internal static void ForceReleasePrimaryButtons()
    {
        lock (Sync)
        {
            ClickerPressedButtons.Remove("Left");
            ClickerPressedButtons.Remove("Right");
        }

        SendMouse(NativeMethods.MouseeventfLeftUp);
        SendMouse(NativeMethods.MouseeventfRightUp);
    }

    internal static void ForceReleaseSideButtons()
    {
        SendMouse(NativeMethods.MouseeventfXUp, NativeMethods.XButton1MouseData);
        SendMouse(NativeMethods.MouseeventfXUp, NativeMethods.XButton2MouseData);
    }

    private static string NormalizeButtonName(string? buttonName)
    {
        return buttonName switch
        {
            "Right" or "RButton" => "Right",
            "Left" or "LButton" => "Left",
            "Middle" or "MButton" => "Middle",
            "XButton1" => "XButton1",
            "XButton2" => "XButton2",
            _ => string.Empty
        };
    }

    private static void SendMouseButtonUp(string normalizedButton)
    {
        switch (normalizedButton)
        {
            case "Right":
                SendMouse(NativeMethods.MouseeventfRightUp);
                break;
            case "Middle":
                SendMouse(NativeMethods.MouseeventfMiddleUp);
                break;
            case "XButton1":
                SendMouse(NativeMethods.MouseeventfXUp, NativeMethods.XButton1MouseData);
                break;
            case "XButton2":
                SendMouse(NativeMethods.MouseeventfXUp, NativeMethods.XButton2MouseData);
                break;
            default:
                SendMouse(NativeMethods.MouseeventfLeftUp);
                break;
        }
    }

    private static void SendMouse(uint flags, uint mouseData = 0)
    {
        var input = new NativeMethods.Input
        {
            Type = 0,
            U = new NativeMethods.InputUnion
            {
                Mi = new NativeMethods.MouseInput
                {
                    MouseData = mouseData,
                    DwFlags = flags,
                    DwExtraInfo = NativeMethods.KofgeClickerExtraInfo
                }
            }
        };

        NativeMethods.SendInput(1, [input], Marshal.SizeOf<NativeMethods.Input>());
    }
}
