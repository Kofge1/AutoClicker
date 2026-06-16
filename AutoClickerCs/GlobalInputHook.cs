using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace AutoClickerCs;

public sealed class GlobalInputEventArgs : EventArgs
{
    public required string Token { get; init; }
    public required bool IsDown { get; init; }
    public required bool IsInjected { get; init; }
    public required bool IsSelfGenerated { get; init; }
    public required bool WasAlreadyDown { get; init; }
    public required bool Ctrl { get; init; }
    public required bool Shift { get; init; }
    public required bool Alt { get; init; }
}

public sealed class GlobalInputHook : IDisposable
{
    private readonly NativeMethods.HookProc _keyboardProc;
    private readonly NativeMethods.HookProc _mouseProc;
    private readonly ConcurrentDictionary<string, byte> _downTokens = new(StringComparer.OrdinalIgnoreCase);
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private bool _disposed;

    public event EventHandler<GlobalInputEventArgs>? InputChanged;
    public Func<string, bool, bool, bool, bool, bool>? ShouldSuppressMouseInput { get; set; }

    public GlobalInputHook()
    {
        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;
    }

    public void Start()
    {
        if (_keyboardHook != IntPtr.Zero || _mouseHook != IntPtr.Zero)
        {
            return;
        }

        var module = NativeMethods.GetModuleHandle(null);
        _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, _keyboardProc, module, 0);
        _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLl, _mouseProc, module, 0);
    }

    public bool IsChordPressed(HotkeyChord chord)
    {
        var ctrlPressed = NativeMethods.IsPressed(NativeMethods.VkControl);
        var shiftPressed = NativeMethods.IsPressed(NativeMethods.VkShift);
        var altPressed = NativeMethods.IsPressed(NativeMethods.VkMenu);
        if (!chord.RequiredModifiersPresent(ctrlPressed, shiftPressed, altPressed))
        {
            return false;
        }

        if (HotkeyHelper.IsMouseToken(chord.PrimaryToken))
        {
            return IsTokenDown(chord.PrimaryToken);
        }

        var key = HotkeyHelper.ToVirtualKey(chord.PrimaryToken);
        return key.HasValue && NativeMethods.IsPressed(key.Value);
    }

    public bool IsTokenDown(string token)
    {
        var normalizedToken = HotkeyHelper.NormalizePrimaryToken(token);
        return _downTokens.ContainsKey(normalizedToken);
    }

    public void ClearTokenDownState(string token)
    {
        var normalizedToken = HotkeyHelper.NormalizePrimaryToken(token);
        _downTokens.TryRemove(normalizedToken, out _);
    }

    public static bool AreModifiersPressed(bool ctrl, bool shift, bool alt)
    {
        return ctrl == NativeMethods.IsPressed(NativeMethods.VkControl)
            && shift == NativeMethods.IsPressed(NativeMethods.VkShift)
            && alt == NativeMethods.IsPressed(NativeMethods.VkMenu);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<NativeMethods.Kbdllhookstruct>(lParam);
            var message = unchecked((int)wParam);
            var isDown = message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;
            var isUp = message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp;
            if (isDown || isUp)
            {
                var token = HotkeyHelper.FromVirtualKey((int)hookStruct.VkCode);
                if (HotkeyHelper.IsModifierToken(token))
                {
                    token = token switch
                    {
                        "ControlKey" => "Ctrl",
                        "ShiftKey" => "Shift",
                        "Menu" => "Alt",
                        _ => token
                    };
                }

                Publish(
                    token,
                    isDown,
                    (hookStruct.Flags & NativeMethods.LlkhfInjected) != 0,
                    hookStruct.DwExtraInfo == NativeMethods.AutoClickerExtraInfo);
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<NativeMethods.Msllhookstruct>(lParam);
            var message = unchecked((int)wParam);
            string? token = message switch
            {
                NativeMethods.WmLButtonDown or NativeMethods.WmLButtonUp => "LButton",
                NativeMethods.WmRButtonDown or NativeMethods.WmRButtonUp => "RButton",
                NativeMethods.WmMButtonDown or NativeMethods.WmMButtonUp => "MButton",
                NativeMethods.WmXButtonDown or NativeMethods.WmXButtonUp => ((hookStruct.MouseData >> 16) & 0xFFFF) == 1 ? "XButton1" : "XButton2",
                _ => null
            };

            if (token is not null)
            {
                var isDown = message is NativeMethods.WmLButtonDown or NativeMethods.WmRButtonDown or NativeMethods.WmMButtonDown or NativeMethods.WmXButtonDown;
                var ctrl = NativeMethods.IsPressed(NativeMethods.VkControl);
                var shift = NativeMethods.IsPressed(NativeMethods.VkShift);
                var alt = NativeMethods.IsPressed(NativeMethods.VkMenu);
                var isInjected = (hookStruct.Flags & NativeMethods.LlmhfInjected) != 0;
                var isSelfGenerated = hookStruct.DwExtraInfo == NativeMethods.AutoClickerExtraInfo;
                Publish(
                    token,
                    isDown,
                    isInjected,
                    isSelfGenerated);

                if (!isInjected &&
                    !isSelfGenerated &&
                    ShouldSuppressMouseInput?.Invoke(token, isDown, ctrl, shift, alt) == true)
                {
                    return (IntPtr)1;
                }
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private void Publish(string token, bool isDown, bool injected, bool selfGenerated)
    {
        var normalizedToken = token switch
        {
            "ControlKey" or "LControlKey" or "RControlKey" => "Ctrl",
            "ShiftKey" or "LShiftKey" or "RShiftKey" => "Shift",
            "Menu" or "LMenu" or "RMenu" => "Alt",
            _ => HotkeyHelper.NormalizePrimaryToken(token)
        };

        var wasAlreadyDown = _downTokens.ContainsKey(normalizedToken);
        if (!selfGenerated)
        {
            if (isDown)
            {
                _downTokens.TryAdd(normalizedToken, 0);
            }
            else
            {
                _downTokens.TryRemove(normalizedToken, out _);
            }
        }

        InputChanged?.Invoke(this, new GlobalInputEventArgs
        {
            Token = normalizedToken,
            IsDown = isDown,
            IsInjected = injected,
            IsSelfGenerated = selfGenerated,
            WasAlreadyDown = wasAlreadyDown,
            Ctrl = NativeMethods.IsPressed(NativeMethods.VkControl),
            Shift = NativeMethods.IsPressed(NativeMethods.VkShift),
            Alt = NativeMethods.IsPressed(NativeMethods.VkMenu)
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            _ = NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        if (_mouseHook != IntPtr.Zero)
        {
            _ = NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        _disposed = true;
    }
}
