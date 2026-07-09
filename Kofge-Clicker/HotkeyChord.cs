using System.Windows.Forms;

namespace KofgeClicker;

public readonly record struct HotkeyChord(bool Ctrl, bool Shift, bool Alt, string PrimaryToken)
{
    public string ToStoredString()
    {
        return $"{(Ctrl ? "^" : "")}{(Shift ? "+" : "")}{(Alt ? "!" : "")}{PrimaryToken}";
    }

    public string ToDisplayString()
    {
        if (string.IsNullOrWhiteSpace(PrimaryToken))
        {
            return "None";
        }

        var parts = new List<string>(4);
        if (Ctrl)
        {
            parts.Add("Ctrl");
        }

        if (Shift)
        {
            parts.Add("Shift");
        }

        if (Alt)
        {
            parts.Add("Alt");
        }

        parts.Add(PrimaryToken);
        return string.Join(" + ", parts);
    }

    public bool ModifiersMatch(bool ctrl, bool shift, bool alt)
    {
        return Ctrl == ctrl && Shift == shift && Alt == alt;
    }

    public bool RequiredModifiersPresent(bool ctrl, bool shift, bool alt)
    {
        return (!Ctrl || ctrl) && (!Shift || shift) && (!Alt || alt);
    }

    public bool IsBareMouseButton()
    {
        return !Ctrl && !Shift && !Alt && HotkeyHelper.IsMouseToken(PrimaryToken);
    }

    public static bool TryParse(string? text, out HotkeyChord chord)
    {
        chord = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = HotkeyHelper.NormalizeStoredString(text);
        if (normalized.Length == 0 || normalized.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var ctrl = normalized.Contains('^');
        var shift = normalized.Contains('+');
        var alt = normalized.Contains('!');
        var token = normalized.Replace("^", string.Empty).Replace("+", string.Empty).Replace("!", string.Empty);
        token = HotkeyHelper.NormalizePrimaryToken(token);
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        chord = new HotkeyChord(ctrl, shift, alt, token);
        return true;
    }
}

public static class HotkeyHelper
{
    private static readonly Dictionary<string, int> TokenToVk = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LButton"] = NativeMethods.VkLButton,
        ["RButton"] = NativeMethods.VkRButton,
        ["MButton"] = NativeMethods.VkMButton,
        ["XButton1"] = NativeMethods.VkXButton1,
        ["XButton2"] = NativeMethods.VkXButton2,
        ["Backspace"] = NativeMethods.VkBack,
        ["Tab"] = NativeMethods.VkTab,
        ["Enter"] = NativeMethods.VkReturn,
        ["Pause"] = NativeMethods.VkPause,
        ["CapsLock"] = NativeMethods.VkCapsLock,
        ["Esc"] = NativeMethods.VkEscape,
        ["Space"] = NativeMethods.VkSpace,
        ["PgUp"] = NativeMethods.VkPageUp,
        ["PgDn"] = NativeMethods.VkPageDown,
        ["End"] = NativeMethods.VkEnd,
        ["Home"] = NativeMethods.VkHome,
        ["Left"] = NativeMethods.VkLeft,
        ["Up"] = NativeMethods.VkUp,
        ["Right"] = NativeMethods.VkRight,
        ["Down"] = NativeMethods.VkDown,
        ["PrintScreen"] = NativeMethods.VkPrintScreen,
        ["Insert"] = NativeMethods.VkInsert,
        ["Delete"] = NativeMethods.VkDelete,
        ["Apps"] = NativeMethods.VkApps,
        ["NumLock"] = NativeMethods.VkNumLock,
        ["ScrollLock"] = NativeMethods.VkScroll
    };

    public static string NormalizeStoredString(string value)
    {
        return value.Replace(" ", string.Empty).Trim();
    }

    public static string NormalizePrimaryToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        token = token.Trim();

        if (TokenToVk.ContainsKey(token))
        {
            return token switch
            {
                "Escape" => "Esc",
                "PageUp" => "PgUp",
                "PageDown" => "PgDn",
                _ => token
            };
        }

        if (token.Length == 1 && char.IsLetterOrDigit(token[0]))
        {
            return token.ToUpperInvariant();
        }

        if (token.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(token[1..], out var fIndex) &&
            fIndex is >= 1 and <= 24)
        {
            return $"F{fIndex}";
        }

        if (token.StartsWith("Numpad", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(token[7..], out var numpadIndex) &&
            numpadIndex is >= 0 and <= 9)
        {
            return $"Numpad{numpadIndex}";
        }

        return token switch
        {
            "Escape" => "Esc",
            "PageUp" => "PgUp",
            "PageDown" => "PgDn",
            _ => token
        };
    }

    public static bool IsModifierToken(string token)
    {
        return token.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)
            || token.Equals("Shift", StringComparison.OrdinalIgnoreCase)
            || token.Equals("Alt", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsMouseToken(string token)
    {
        return token.Equals("LButton", StringComparison.OrdinalIgnoreCase)
            || token.Equals("RButton", StringComparison.OrdinalIgnoreCase)
            || token.Equals("MButton", StringComparison.OrdinalIgnoreCase)
            || token.Equals("XButton1", StringComparison.OrdinalIgnoreCase)
            || token.Equals("XButton2", StringComparison.OrdinalIgnoreCase);
    }

    public static string FromVirtualKey(int virtualKey)
    {
        if (virtualKey is >= NativeMethods.VkA and <= NativeMethods.VkZ)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= NativeMethods.Vk0 and <= NativeMethods.Vk9)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= NativeMethods.VkF1 and <= NativeMethods.VkF24)
        {
            return $"F{virtualKey - NativeMethods.VkF1 + 1}";
        }

        if (virtualKey is >= NativeMethods.VkNumpad0 and <= NativeMethods.VkNumpad9)
        {
            return $"Numpad{virtualKey - NativeMethods.VkNumpad0}";
        }

        return virtualKey switch
        {
            NativeMethods.VkBack => "Backspace",
            NativeMethods.VkTab => "Tab",
            NativeMethods.VkReturn => "Enter",
            NativeMethods.VkPause => "Pause",
            NativeMethods.VkCapsLock => "CapsLock",
            NativeMethods.VkEscape => "Esc",
            NativeMethods.VkSpace => "Space",
            NativeMethods.VkPageUp => "PgUp",
            NativeMethods.VkPageDown => "PgDn",
            NativeMethods.VkEnd => "End",
            NativeMethods.VkHome => "Home",
            NativeMethods.VkLeft => "Left",
            NativeMethods.VkUp => "Up",
            NativeMethods.VkRight => "Right",
            NativeMethods.VkDown => "Down",
            NativeMethods.VkPrintScreen => "PrintScreen",
            NativeMethods.VkInsert => "Insert",
            NativeMethods.VkDelete => "Delete",
            NativeMethods.VkApps => "Apps",
            NativeMethods.VkNumLock => "NumLock",
            NativeMethods.VkScroll => "ScrollLock",
            _ => ((Keys)virtualKey).ToString()
        };
    }

    public static int? ToVirtualKey(string token)
    {
        token = NormalizePrimaryToken(token);

        if (TokenToVk.TryGetValue(token, out var direct))
        {
            return direct;
        }

        if (token.Length == 1 && char.IsLetter(token[0]))
        {
            return char.ToUpperInvariant(token[0]);
        }

        if (token.Length == 1 && char.IsDigit(token[0]))
        {
            return token[0];
        }

        if (token.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(token[1..], out var fIndex) &&
            fIndex is >= 1 and <= 24)
        {
            return NativeMethods.VkF1 + fIndex - 1;
        }

        if (token.StartsWith("Numpad", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(token[7..], out var npIndex) &&
            npIndex is >= 0 and <= 9)
        {
            return NativeMethods.VkNumpad0 + npIndex;
        }

        return null;
    }
}
