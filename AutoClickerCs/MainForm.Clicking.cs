using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AutoClickerCs;

public sealed partial class MainForm
{
    private enum ClickStopReason
    {
        Manual,
        HotkeyReleased,
        Disabled,
        ContextLost,
        SessionSuperseded,
        ServiceHotkey,
        ProfileChange,
        Shutdown,
        Panic,
        ConfigurationChanged
    }

    private enum ClickReleaseMode
    {
        Soft,
        PreserveTriggerMouseButton,
        ForceCurrentClickButton
    }

    private void StartClickLoop()
    {
        PrepareForClickLoopStart();
        var sessionVersion = Interlocked.Increment(ref _clickSessionVersion);
        InputDiagnostics.Write($"StartClickLoop session={sessionVersion} active={_isActive} enabled={_settings.AutoEnabled} mode={_settings.CurrentMode} click={_settings.ClickButton} pattern={_settings.ClickPattern} cps={_settings.Cps} humanized={_settings.HumanizedCpsEnabled}");
        _clickCts?.Cancel();
        _clickCts = new CancellationTokenSource();
        var token = _clickCts.Token;
        Task.Factory.StartNew(() => ClickLoop(token, sessionVersion), token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private void ClickLoop(CancellationToken token, int sessionVersion)
    {
        var nextClick = HighResNowMs();
        try
        {
            while (!token.IsCancellationRequested
                && sessionVersion == Volatile.Read(ref _clickSessionVersion)
                && _isActive
                && _settings.AutoEnabled)
            {
                if (!CanClickInCurrentContext())
                {
                    ReleaseClickerMouseState(ClickStopReason.ContextLost);
                    nextClick = HighResNowMs() + 10;
                    Thread.Sleep(10);
                    continue;
                }

                while (!token.IsCancellationRequested && HighResNowMs() < nextClick)
                {
                    Thread.SpinWait(50);
                }

                if (token.IsCancellationRequested
                    || sessionVersion != Volatile.Read(ref _clickSessionVersion)
                    || !_isActive
                    || !_settings.AutoEnabled)
                {
                    break;
                }

                var sentClicks = SendLowLevelClick(token, sessionVersion);
                if (sentClicks <= 0)
                {
                    break;
                }
                nextClick += GetScheduledClickIntervalMs(sentClicks);
            }
        }
        finally
        {
            InputDiagnostics.Write($"ClickLoopExit session={sessionVersion} current={Volatile.Read(ref _clickSessionVersion)} active={_isActive} enabled={_settings.AutoEnabled}");
            ReleaseClickerMouseState(ClickStopReason.Manual);
        }
    }

    private void StopClicking(ClickStopReason reason, bool updateStatus = true)
    {
        _isActive = false;
        var sessionVersion = Interlocked.Increment(ref _clickSessionVersion);
        InputDiagnostics.Write($"StopClicking reason={reason} session={sessionVersion} click={_settings.ClickButton} held={_mouseButtonHeldByClicker}");
        _clickCts?.Cancel();
        ReleaseClickerMouseState(reason);
        if (updateStatus)
        {
            UpdateStatus();
        }
    }

    private void StopClicking(bool updateStatus = true)
    {
        StopClicking(ClickStopReason.Manual, updateStatus);
    }

    private int SendLowLevelClick(CancellationToken token, int sessionVersion)
    {
        return _settings.ClickPattern switch
        {
            "Burst" => SendBurstSeries(3, _settings.BurstGapMs, token, sessionVersion),
            "Double Click" => SendBurstSeries(2, _settings.BurstGapMs, token, sessionVersion),
            "Hold then Burst" => SendHoldThenBurst(token, sessionVersion),
            _ => SendSingleTap(token, sessionVersion)
        };
    }

    private int SendSingleTap(CancellationToken token, int sessionVersion)
    {
        return SendButtonTap(_settings.ClickButton, token, sessionVersion) ? 1 : 0;
    }

    private int SendHoldThenBurst(CancellationToken token, int sessionVersion)
    {
        var mouseDownSent = false;
        try
        {
            SendMouseDown(_settings.ClickButton);
            mouseDownSent = true;
            if (!DelayRespectingCancellation(_settings.HoldThenBurstHoldMs, token, sessionVersion))
            {
                return 0;
            }

            SendMouseUp(_settings.ClickButton);
            EnsureToggleClickButtonReleased(_settings.ClickButton);
            mouseDownSent = false;
            if (!DelayRespectingCancellation(_settings.ReleaseDelayMs, token, sessionVersion))
            {
                return 1;
            }

            return 1 + SendBurstSeries(Math.Max(1, _settings.BurstClickCount) - 1, _settings.BurstGapMs, token, sessionVersion);
        }
        finally
        {
            if (mouseDownSent)
            {
                MouseButtonSafety.ReleaseButton(_settings.ClickButton);
                if (string.Equals(_mouseButtonHeldByClicker, NormalizeClickButton(_settings.ClickButton), StringComparison.Ordinal))
                {
                    _mouseButtonHeldByClicker = string.Empty;
                }
            }
        }
    }

    private int SendBurstSeries(int clickCount, int gapMs, CancellationToken token, int sessionVersion)
    {
        if (clickCount <= 0)
        {
            return 0;
        }

        var sentClicks = 0;
        for (var i = 0; i < clickCount; i++)
        {
            if (!SendButtonTap(_settings.ClickButton, token, sessionVersion))
            {
                return sentClicks;
            }

            sentClicks++;
            if (i < clickCount - 1 && !DelayRespectingCancellation(gapMs, token, sessionVersion))
            {
                return sentClicks;
            }
        }

        return sentClicks;
    }

    private bool SendButtonTap(string buttonName, CancellationToken token, int sessionVersion)
    {
        var mouseDownSent = false;
        try
        {
            SendMouseDown(buttonName);
            mouseDownSent = true;
            if (!DelayRespectingCancellation(_settings.PressDelayMs, token, sessionVersion))
            {
                return false;
            }

            SendMouseUp(buttonName);
            EnsureToggleClickButtonReleased(buttonName);
            mouseDownSent = false;
            if (!DelayRespectingCancellation(_settings.ReleaseDelayMs, token, sessionVersion))
            {
                return false;
            }

            return true;
        }
        finally
        {
            if (mouseDownSent)
            {
                MouseButtonSafety.ReleaseButton(buttonName);
                var normalizedButton = NormalizeClickButton(buttonName);
                if (string.Equals(_mouseButtonHeldByClicker, normalizedButton, StringComparison.Ordinal))
                {
                    _mouseButtonHeldByClicker = string.Empty;
                }
            }
        }
    }

    private void EnsureToggleClickButtonReleased(string buttonName)
    {
        if (!string.Equals(_settings.CurrentMode, "toggle", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var virtualKey = NormalizeClickButton(buttonName) switch
        {
            "Right" => NativeMethods.VkRButton,
            "Left" => NativeMethods.VkLButton,
            _ => 0
        };

        if (virtualKey == 0 || !NativeMethods.IsPressed(virtualKey))
        {
            return;
        }

        InputDiagnostics.Write($"EnsureToggleClickButtonReleased force button={buttonName} mode={_settings.CurrentMode}");
        MouseButtonSafety.ForceReleaseButton(buttonName);
        if (string.Equals(_mouseButtonHeldByClicker, NormalizeClickButton(buttonName), StringComparison.Ordinal))
        {
            _mouseButtonHeldByClicker = string.Empty;
        }
    }

    private void SendMouseDown(string buttonName)
    {
        var normalizedButton = NormalizeClickButton(buttonName);
        var flags = normalizedButton == "Right"
            ? NativeMethods.MouseeventfRightDown
            : NativeMethods.MouseeventfLeftDown;
        _mouseButtonHeldByClicker = normalizedButton;
        MouseButtonSafety.MarkButtonDown(normalizedButton);
        SendMouse(flags);
    }

    private void SendMouseUp(string buttonName)
    {
        var normalizedButton = NormalizeClickButton(buttonName);
        var flags = normalizedButton == "Right"
            ? NativeMethods.MouseeventfRightUp
            : NativeMethods.MouseeventfLeftUp;
        SendMouse(flags);
        MouseButtonSafety.MarkButtonUp(normalizedButton);
        if (string.Equals(_mouseButtonHeldByClicker, normalizedButton, StringComparison.Ordinal))
        {
            _mouseButtonHeldByClicker = string.Empty;
        }
    }

    private static void SendMouse(uint flags)
    {
        var input = new NativeMethods.Input
        {
            Type = 0,
            U = new NativeMethods.InputUnion
            {
                Mi = new NativeMethods.MouseInput
                {
                    DwFlags = flags,
                    DwExtraInfo = NativeMethods.AutoClickerExtraInfo
                }
            }
        };

        NativeMethods.SendInput(1, [input], Marshal.SizeOf<NativeMethods.Input>());
    }

    private bool DelayRespectingCancellation(int delayMs, CancellationToken token, int sessionVersion)
    {
        if (delayMs <= 0)
        {
            if (TryGetClickStopReason(token, sessionVersion, out var stopReason))
            {
                ReleaseClickerMouseState(stopReason);
                return false;
            }

            return true;
        }

        var end = HighResNowMs() + delayMs;
        while (HighResNowMs() < end)
        {
            if (TryGetClickStopReason(token, sessionVersion, out var stopReason))
            {
                ReleaseClickerMouseState(stopReason);
                return false;
            }

            var remaining = end - HighResNowMs();
            if (remaining > 2.0)
            {
                Thread.Sleep(1);
            }
            else
            {
                Thread.SpinWait(50);
            }
        }

        if (TryGetClickStopReason(token, sessionVersion, out var finalStopReason))
        {
            ReleaseClickerMouseState(finalStopReason);
            return false;
        }

        return true;
    }

    private bool TryGetClickStopReason(CancellationToken token, int sessionVersion, out ClickStopReason stopReason)
    {
        if (sessionVersion != Volatile.Read(ref _clickSessionVersion))
        {
            stopReason = ClickStopReason.SessionSuperseded;
            return true;
        }

        if (!CanClickInCurrentContext())
        {
            stopReason = ClickStopReason.ContextLost;
            return true;
        }

        if (!_settings.AutoEnabled)
        {
            stopReason = ClickStopReason.Disabled;
            return true;
        }

        if (!_isActive)
        {
            stopReason = ClickStopReason.Manual;
            return true;
        }

        if (token.IsCancellationRequested)
        {
            stopReason = ClickStopReason.Manual;
            return true;
        }

        stopReason = ClickStopReason.Manual;
        return false;
    }

    private void PrepareForClickLoopStart()
    {
        ReleaseClickerMouseState(ClickReleaseMode.PreserveTriggerMouseButton);
    }

    private void ReleaseHeldMouseButton()
    {
        if (_mouseButtonHeldByClicker.Length == 0)
        {
            return;
        }

        var heldButton = _mouseButtonHeldByClicker;
        _mouseButtonHeldByClicker = string.Empty;
        MouseButtonSafety.ReleaseButton(heldButton);
    }

    private void ReleaseClickerMouseState(ClickStopReason reason)
    {
        ReleaseClickerMouseState(GetReleaseModeForStopReason(reason));
    }

    private void ReleaseClickerMouseState(ClickReleaseMode mode)
    {
        InputDiagnostics.Write($"ReleaseClickerMouseState mode={mode} click={_settings.ClickButton} held={_mouseButtonHeldByClicker}");
        ReleaseHeldMouseButton();
        if (mode == ClickReleaseMode.PreserveTriggerMouseButton)
        {
            MouseButtonSafety.ReleaseAllPressedButtonsExcept(GetPreservedMouseTriggerToken());
        }
        else
        {
            MouseButtonSafety.ReleaseAllPressedButtons();
        }

        if (mode == ClickReleaseMode.ForceCurrentClickButton)
        {
            MouseButtonSafety.ForceReleaseButton(_settings.ClickButton);
        }
    }

    private static ClickReleaseMode GetReleaseModeForStopReason(ClickStopReason reason)
    {
        return reason switch
        {
            ClickStopReason.ContextLost or ClickStopReason.SessionSuperseded or ClickStopReason.ServiceHotkey or ClickStopReason.Panic => ClickReleaseMode.ForceCurrentClickButton,
            _ => ClickReleaseMode.Soft
        };
    }

    private void PrepareForServiceHotkey(string storedHotkey)
    {
        if (!HotkeyChord.TryParse(storedHotkey, out var chord) || !HotkeyHelper.IsMouseToken(chord.PrimaryToken))
        {
            return;
        }

        InputDiagnostics.Write($"PrepareForServiceHotkey token={chord.PrimaryToken} active={_isActive} held={_mouseButtonHeldByClicker}");
        if (_isActive || _mouseButtonHeldByClicker.Length > 0)
        {
            ReleaseClickerMouseState(ClickStopReason.ServiceHotkey);
        }
        else
        {
            ReleaseClickerMouseState(ClickReleaseMode.Soft);
        }

        ReleaseSuppressedServiceHotkeyButton(chord.PrimaryToken);
    }

    private void ReleaseSuppressedServiceHotkeyButton(string token)
    {
        if (!string.Equals(token, "XButton1", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(token, "XButton2", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _inputHook.ClearTokenDownState(token);
        MouseButtonSafety.ForceReleaseButton(token);
        InputDiagnostics.Write($"ReleaseSuppressedServiceHotkeyButton token={token}");
    }

    private void UpdateStatus(bool refreshTrayMenu = true)
    {
        var profileName = GetActiveProfileName();
        var modeText = _settings.CurrentMode == "hold" ? "Hold" : "Toggle";
        var status = _settings.AutoEnabled ? "ON" : "OFF";
        var targetText = GetTargetCpsDisplay();
        _lblStatus.Text =
            $"Profile: {profileName} | Status: {status} | Click: {FormatClickButtonDisplay(_settings.ClickButton)} / {FormatClickPatternDisplay(_settings.ClickPattern)}{Environment.NewLine}" +
            $"Hotkey: {FormatHotkeyDisplay(_settings.TriggerKey)} | Mode: {modeText}{Environment.NewLine}" +
            $"Target: {targetText} / {FormatClickRateModeDisplay(_settings.ClickRateMode)}";
        UpdateStatusIcon();
        if (refreshTrayMenu)
        {
            RefreshTrayMenu();
        }
    }

    private void ResetHumanizedEngine()
    {
        _humanizedWavePhase = 0;
        _humanizedClickCounter = 0;
        _humanizedRecoveryBudgetMs = 0;
    }

    private void UpdateInterval()
    {
        _intervalMs = 1000.0 / _settings.Cps;
    }

    private double GetNextClickIntervalMs()
    {
        if (!_settings.HumanizedCpsEnabled)
        {
            return _intervalMs;
        }

        var config = GetHumanizedPresetConfig();
        _humanizedClickCounter++;
        _humanizedWavePhase += config.phaseStep + Random.Shared.NextDouble() * 0.07 - 0.035;
        var waveComponent = Math.Sin(_humanizedWavePhase) * config.waveAmp;
        var driftComponent = Math.Sin(_humanizedWavePhase * 0.37) * (config.waveAmp * 0.45);
        var noiseComponent = RandomRange(-config.noiseAmp, config.noiseAmp);
        var cpsBias = _settings.Cps >= 90 ? 1.12 : _settings.Cps >= 55 ? 1.08 : 1.035;
        var effectiveCps = ClampHumanizedCps(_settings.Cps * cpsBias * (1.0 + waveComponent + driftComponent + noiseComponent));

        var pauseEvery = (int)Math.Round(config.pauseEveryMin + ((Math.Sin(_humanizedWavePhase * 0.41) + 1.0) / 2.0) * (config.pauseEveryMax - config.pauseEveryMin));
        if (pauseEvery < 1)
        {
            pauseEvery = 1;
        }

        var microPauseMs = 0.0;
        if (_humanizedClickCounter % pauseEvery == 0 && Random.Shared.NextDouble() <= config.pauseChance)
        {
            microPauseMs = RandomRange(config.pauseMinMs, config.pauseMaxMs);
            _humanizedRecoveryBudgetMs += microPauseMs;
        }

        var intervalMs = 1000.0 / effectiveCps;
        if (_humanizedRecoveryBudgetMs > 0)
        {
            var maxRecovery = Math.Max(0.75, intervalMs * 0.42);
            var recovery = Math.Min(_humanizedRecoveryBudgetMs, maxRecovery);
            intervalMs = Math.Max(2.0, intervalMs - recovery);
            _humanizedRecoveryBudgetMs -= recovery;
        }

        return intervalMs + microPauseMs;
    }

    private double GetScheduledClickIntervalMs(int sentClicks)
    {
        var baseInterval = GetNextClickIntervalMs();
        if (_settings.ClickRateMode == "Ordinary" && _settings.ClickPattern != "Standard" && sentClicks > 1)
        {
            return baseInterval * sentClicks;
        }

        return baseInterval;
    }

    private (double waveAmp, double noiseAmp, double phaseStep, int pauseEveryMin, int pauseEveryMax, double pauseChance, double pauseMinMs, double pauseMaxMs) GetHumanizedPresetConfig()
    {
        return _settings.HumanizedPreset switch
        {
            "Stable" => (0.025, 0.012, 0.19, 24, 36, 0.18, 10.0, 16.0),
            "Aggressive" => (0.11, 0.048, 0.33, 9, 16, 0.38, 18.0, 38.0),
            _ => (0.06, 0.026, 0.25, 14, 24, 0.28, 12.0, 24.0)
        };
    }

    private double ClampHumanizedCps(double value)
    {
        var upperBound = _settings.Cps >= 90
            ? 140.0
            : _settings.Cps >= 55
                ? 125.0
                : 110.0;
        return Math.Clamp(value, 1.0, upperBound);
    }

    private bool CanClickInCurrentContext()
    {
        return !_settings.RestrictToFocusedWindow || IsTargetWindowFocused();
    }

    private bool IsTargetWindowFocused()
    {
        if (!HasCapturedTargetWindow())
        {
            return true;
        }

        var active = NativeMethods.GetForegroundWindow();
        if (active == IntPtr.Zero)
        {
            return false;
        }

        var activeExe = NativeMethods.GetWindowProcessName(active);
        var activeClass = NativeMethods.GetWindowClass(active);
        var activeTitle = NativeMethods.GetWindowTitle(active);
        if (_settings.TargetWindowExe.Length > 0)
        {
            return string.Equals(activeExe, _settings.TargetWindowExe, StringComparison.OrdinalIgnoreCase);
        }

        if (_settings.TargetWindowClass.Length > 0)
        {
            return string.Equals(activeClass, _settings.TargetWindowClass, StringComparison.Ordinal);
        }

        if (_settings.TargetWindowTitle.Length > 0)
        {
            return string.Equals(activeTitle, _settings.TargetWindowTitle, StringComparison.Ordinal);
        }

        return true;
    }

    private double HighResNowMs()
    {
        NativeMethods.QueryPerformanceCounter(out var counter);
        return counter * 1000.0 / _qpcFrequency;
    }

    private bool MatchesChordPress(HotkeyChord chord, GlobalInputEventArgs e)
    {
        return e.IsDown
            && !e.WasAlreadyDown
            && e.Token.Equals(chord.PrimaryToken, StringComparison.OrdinalIgnoreCase)
            && chord.RequiredModifiersPresent(e.Ctrl, e.Shift, e.Alt);
    }

    private bool ShouldSuppressPhysicalMouseInput(string token, bool isDown, bool ctrl, bool shift, bool alt)
    {
        if (!isDown || _recordingTargetName is not null || !HotkeyHelper.IsMouseToken(token) || IsOwnWindowForeground())
        {
            return false;
        }

        return (_settings.AutoEnabled && ShouldSuppressConfiguredMouseChord(GetEffectiveTriggerKey(_settings.TriggerKey), token, ctrl, shift, alt))
            || ShouldSuppressConfiguredMouseChord(GetEffectivePanicHotkey(_settings.PanicHotkey), token, ctrl, shift, alt)
            || ShouldSuppressConfiguredMouseChord(GetEffectiveShowWindowHotkey(_settings.ShowWindowHotkey), token, ctrl, shift, alt)
            || ShouldSuppressConfiguredMouseChord(GetEffectiveTogglePowerHotkey(_settings.TogglePowerHotkey), token, ctrl, shift, alt);
    }

    private bool IsOwnWindowForeground()
    {
        if (!IsHandleCreated)
        {
            return false;
        }

        return NativeMethods.GetForegroundWindow() == Handle;
    }

    private static bool ShouldSuppressConfiguredMouseChord(string storedHotkey, string token, bool ctrl, bool shift, bool alt)
    {
        if (!HotkeyChord.TryParse(storedHotkey, out var chord) || !HotkeyHelper.IsMouseToken(chord.PrimaryToken))
        {
            return false;
        }

        return string.Equals(token, chord.PrimaryToken, StringComparison.OrdinalIgnoreCase)
            && chord.RequiredModifiersPresent(ctrl, shift, alt);
    }

    private bool IsChordStillPressed(HotkeyChord chord)
    {
        return _inputHook.IsChordPressed(chord);
    }

    private bool ShouldStopHoldFromEvent(HotkeyChord trigger, GlobalInputEventArgs e)
    {
        if (e.IsDown)
        {
            return false;
        }

        if (string.Equals(e.Token, trigger.PrimaryToken, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trigger.Ctrl && string.Equals(e.Token, "Ctrl", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trigger.Shift && string.Equals(e.Token, "Shift", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trigger.Alt && string.Equals(e.Token, "Alt", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private void SetTrayWindowMode(bool toTray)
    {
        if (!IsHandleCreated)
        {
            return;
        }

        var exStyle = (uint)NativeMethods.GetWindowLongPtr(Handle, NativeMethods.GwlExstyle).ToInt64();
        exStyle = toTray
            ? (exStyle | NativeMethods.WsExToolwindow) & ~NativeMethods.WsExAppwindow
            : (exStyle | NativeMethods.WsExAppwindow) & ~NativeMethods.WsExToolwindow;

        NativeMethods.SetWindowLongPtr(Handle, NativeMethods.GwlExstyle, (nint)exStyle);
        NativeMethods.SetWindowPos(
            Handle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNomove | NativeMethods.SwpNosize | NativeMethods.SwpNozorder | NativeMethods.SwpNoactivate | NativeMethods.SwpFramechanged);
    }

    private static string GetTriggerMainKey(string hotkey)
    {
        return HotkeyChord.TryParse(GetEffectiveTriggerKey(hotkey), out var chord)
            ? chord.PrimaryToken
            : string.Empty;
    }

    private bool IsHotkeyStillPressed(string hotkey)
    {
        if (!HotkeyChord.TryParse(GetEffectiveTriggerKey(hotkey), out var chord))
        {
            return false;
        }

        var primaryPressed = HotkeyHelper.IsMouseToken(chord.PrimaryToken)
            ? _inputHook.IsTokenDown(chord.PrimaryToken)
            : HotkeyHelper.ToVirtualKey(chord.PrimaryToken) is { } key && NativeMethods.IsPressed(key);

        if (!primaryPressed)
        {
            return false;
        }

        if (chord.Ctrl && !NativeMethods.IsPressed(NativeMethods.VkControl))
        {
            return false;
        }

        if (chord.Shift && !NativeMethods.IsPressed(NativeMethods.VkShift))
        {
            return false;
        }

        if (chord.Alt && !NativeMethods.IsPressed(NativeMethods.VkMenu))
        {
            return false;
        }

        return true;
    }

    private string? GetPreservedMouseTriggerToken()
    {
        if (_settings.CurrentMode != "hold")
        {
            return null;
        }

        if (!HotkeyChord.TryParse(GetEffectiveTriggerKey(_settings.TriggerKey), out var chord))
        {
            return null;
        }

        if (!HotkeyHelper.IsMouseToken(chord.PrimaryToken))
        {
            return null;
        }

        var normalizedTriggerButton = NormalizeTriggerMouseToken(chord.PrimaryToken);
        // When hold mode uses the same mouse button as both the trigger and the click target,
        // keeping the physical button logically held can interfere with games that expect clean
        // down/up pairs for camera control and raw input.
        if (string.Equals(normalizedTriggerButton, NormalizeClickButton(_settings.ClickButton), StringComparison.Ordinal))
        {
            return null;
        }

        return normalizedTriggerButton;
    }

    private static string? NormalizeTriggerMouseToken(string token)
    {
        return token switch
        {
            "LButton" => "Left",
            "RButton" => "Right",
            "MButton" => "Middle",
            _ => null
        };
    }

    private static HotkeyChord GetEffectiveChord(string stored)
    {
        return HotkeyChord.TryParse(stored, out var chord)
            ? chord
            : HotkeyChord.TryParse("F2", out chord)
                ? chord
                : default;
    }

    private static bool ValidateDistinctHotkeys(params string[] hotkeys)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hotkey in hotkeys)
        {
            var normalized = NormalizeHotkey(hotkey);
            if (normalized.Length == 0)
            {
                continue;
            }

            if (!set.Add(normalized))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsBareMouseHotkey(string hotkey)
    {
        return HotkeyChord.TryParse(hotkey, out var chord) && chord.IsBareMouseButton();
    }

    private static bool IsRestrictedBareServiceMouseHotkey(string hotkey)
    {
        if (!HotkeyChord.TryParse(hotkey, out var chord) || !chord.IsBareMouseButton())
        {
            return false;
        }

        return chord.PrimaryToken.Equals("LButton", StringComparison.OrdinalIgnoreCase)
            || chord.PrimaryToken.Equals("RButton", StringComparison.OrdinalIgnoreCase)
            || chord.PrimaryToken.Equals("MButton", StringComparison.OrdinalIgnoreCase);
    }

    private static (string main, string panic, string show, string toggle) RepairUnsafeServiceHotkeys(string mainKey, string panicKey, string showWindowKey, string togglePowerKey)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var main = GetSafeUniqueHotkey(mainKey, ["F2", "F3", "F4"], used);
        var panic = GetSafeUniqueHotkey(IsRestrictedBareServiceMouseHotkey(panicKey) ? "" : panicKey, ["F12", "F11", "F10"], used);
        var show = GetSafeUniqueHotkey(IsRestrictedBareServiceMouseHotkey(showWindowKey) ? "" : showWindowKey, ["F10", "F9", "F8"], used);
        var toggle = GetSafeUniqueHotkey(IsRestrictedBareServiceMouseHotkey(togglePowerKey) ? "" : togglePowerKey, ["F7", "F6", "F5"], used);
        return (main, panic, show, toggle);
    }

    private static string GetSafeUniqueHotkey(string preferredKey, string[] fallbackKeys, ISet<string> usedHotkeys)
    {
        preferredKey = NormalizeStoredHotkey(preferredKey, fallbackKeys[0]);
        var preferredNorm = NormalizeHotkey(preferredKey);
        if (!usedHotkeys.Contains(preferredNorm))
        {
            usedHotkeys.Add(preferredNorm);
            return preferredKey;
        }

        foreach (var fallback in fallbackKeys)
        {
            var fallbackNorm = NormalizeHotkey(fallback);
            if (!usedHotkeys.Contains(fallbackNorm))
            {
                usedHotkeys.Add(fallbackNorm);
                return fallback;
            }
        }

        var index = 1;
        while (usedHotkeys.Contains($"F{index}"))
        {
            index++;
        }

        usedHotkeys.Add($"F{index}");
        return $"F{index}";
    }

    private void SanitizeLoadedSettings()
    {
        _settings.CurrentMode = NormalizeMode(_settings.CurrentMode);
        _settings.ClickButton = NormalizeClickButton(_settings.ClickButton);
        _settings.ClickPattern = NormalizeClickPattern(_settings.ClickPattern);
        _settings.ClickRateMode = NormalizeClickRateMode(_settings.ClickRateMode);
        _settings.BurstClickCount = ClampBurstClickCount(_settings.BurstClickCount);
        _settings.BurstGapMs = ClampPatternDelay(_settings.BurstGapMs);
        _settings.HoldThenBurstHoldMs = ClampPatternDelay(_settings.HoldThenBurstHoldMs);
        _settings.PressDelayMs = ClampPatternDelay(_settings.PressDelayMs);
        _settings.ReleaseDelayMs = ClampPatternDelay(_settings.ReleaseDelayMs);
        _settings.TargetWindowTitle = _settings.TargetWindowTitle.Trim();
        _settings.TargetWindowClass = _settings.TargetWindowClass.Trim();
        _settings.TargetWindowExe = _settings.TargetWindowExe.Trim();
        _settings.HumanizedPreset = NormalizeHumanizedPreset(_settings.HumanizedPreset);
        if (!HasCapturedTargetWindow())
        {
            _settings.RestrictToFocusedWindow = false;
        }

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _settings.TriggerKey = GetSafeUniqueHotkey(_settings.TriggerKey, ["F2", "F3", "F4"], used);
        _settings.PanicHotkey = GetSafeUniqueHotkey(IsRestrictedBareServiceMouseHotkey(_settings.PanicHotkey) ? "" : _settings.PanicHotkey, ["F12", "F11", "F10"], used);
        _settings.ShowWindowHotkey = GetSafeUniqueHotkey(IsRestrictedBareServiceMouseHotkey(_settings.ShowWindowHotkey) ? "" : _settings.ShowWindowHotkey, ["F10", "F9", "F8"], used);
        _settings.TogglePowerHotkey = GetSafeUniqueHotkey(IsRestrictedBareServiceMouseHotkey(_settings.TogglePowerHotkey) ? "" : _settings.TogglePowerHotkey, ["F7", "F6", "F5"], used);
    }

    private void SyncStartupShortcut()
    {
        const string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string runValueName = "AutoClicker";
        var shortcutPath = GetStartupShortcutPath();
        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(runKeyPath);
            if (runKey is null)
            {
                return;
            }

            if (_settings.RunOnWindowsStartup)
            {
                runKey.SetValue(runValueName, $"\"{Application.ExecutablePath}\"", RegistryValueKind.String);
            }
            else
            {
                runKey.DeleteValue(runValueName, false);
            }

            // Clean up the legacy .lnk startup entry if it exists from older builds.
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }
        }
        catch
        {
        }
    }

    private static string GetStartupShortcutPath()
    {
        var startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        return Path.Combine(startupDir, "AutoClicker.lnk");
    }

    private static string GetProfileSectionName(string profileId) => $"Profile_{profileId}";

    private string GenerateProfileId(string profileName)
    {
        var baseId = Regex.Replace(profileName.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
        if (baseId.Length == 0)
        {
            baseId = "profile";
        }

        var profileId = baseId;
        var suffix = 2;
        while (ProfileExists(profileId))
        {
            profileId = $"{baseId}_{suffix}";
            suffix++;
        }

        return profileId;
    }

    private static string NormalizeProfileName(string profileName)
    {
        return profileName.Trim().Replace("[", "(").Replace("]", ")");
    }

    private string BuildNewProfileName()
    {
        var index = 2;
        while (FindProfileIndexByName($"Profile {index}") > 0)
        {
            index++;
        }

        return $"Profile {index}";
    }

    private string BuildDuplicateProfileName(string sourceName) => MakeUniqueProfileName($"{sourceName} Copy");

    private string MakeUniqueProfileName(string baseName)
    {
        var candidate = NormalizeProfileName(baseName);
        if (candidate.Length == 0)
        {
            candidate = "Imported Profile";
        }

        if (FindProfileIndexByName(candidate) == 0)
        {
            return candidate;
        }

        var suffix = 2;
        while (FindProfileIndexByName($"{candidate} {suffix}") > 0)
        {
            suffix++;
        }

        return $"{candidate} {suffix}";
    }

    private static string BuildProfileExportFileName(string profileName)
    {
        var safeName = Regex.Replace(profileName, "[\\\\/:*?\"<>|]", "_").Trim();
        if (safeName.Length == 0)
        {
            safeName = "profile";
        }

        return $"{safeName}.ini";
    }

    private void CopyProfileSectionData(string sourceFile, string sourceSection, string targetFile, string targetSection, string targetProfileName)
    {
        var sourceIni = new IniFile(sourceFile);
        var targetIni = new IniFile(targetFile);
        var keys = new[]
        {
            "AutoEnabled", "Mode", "Hotkey", "PanicHotkey", "ShowWindowHotkey", "TogglePowerHotkey",
            "CloseToTrayOnClose", "RestrictToFocusedWindow", "TargetWindowTitle", "TargetWindowClass", "TargetWindowExe",
            "ClickButton", "ClickPattern", "ClickRateMode", "BurstClickCount", "BurstGapMs", "HoldThenBurstHoldMs",
            "PressDelayMs", "ReleaseDelayMs", "CPS", "HumanizedCpsEnabled", "HumanizedPreset"
        };

        targetIni.WriteString(targetSection, "Name", targetProfileName);
        foreach (var key in keys)
        {
            targetIni.WriteString(targetSection, key, ReadProfileDataValue(sourceIni, sourceSection, key));
        }
    }

    private string ReadProfileDataValue(IniFile sourceIni, string sourceSection, string key)
    {
        return key switch
        {
            "AutoEnabled" => sourceIni.ReadString(sourceSection, key, "1"),
            "Mode" => sourceIni.ReadString(sourceSection, key, "hold"),
            "Hotkey" => sourceIni.ReadString(sourceSection, key, "F2"),
            "PanicHotkey" => sourceIni.ReadString(sourceSection, key, "F12"),
            "ShowWindowHotkey" => sourceIni.ReadString(sourceSection, key, "F10"),
            "TogglePowerHotkey" => sourceIni.ReadString(sourceSection, key, "F7"),
            "CloseToTrayOnClose" => sourceIni.ReadString(sourceSection, key, "1"),
            "RestrictToFocusedWindow" => sourceIni.ReadString(sourceSection, key, "0"),
            "TargetWindowTitle" => sourceIni.ReadString(sourceSection, key, ""),
            "TargetWindowClass" => sourceIni.ReadString(sourceSection, key, ""),
            "TargetWindowExe" => sourceIni.ReadString(sourceSection, key, ""),
            "ClickButton" => sourceIni.ReadString(sourceSection, key, "Left"),
            "ClickPattern" => sourceIni.ReadString(sourceSection, key, "Standard"),
            "ClickRateMode" => sourceIni.ReadString(sourceSection, key, "Ordinary"),
            "BurstClickCount" => sourceIni.ReadString(sourceSection, key, "3"),
            "BurstGapMs" => sourceIni.ReadString(sourceSection, key, "14"),
            "HoldThenBurstHoldMs" => sourceIni.ReadString(sourceSection, key, "70"),
            "PressDelayMs" => sourceIni.ReadString(sourceSection, key, "0"),
            "ReleaseDelayMs" => sourceIni.ReadString(sourceSection, key, "0"),
            "CPS" => sourceIni.ReadString(sourceSection, key, "15"),
            "HumanizedCpsEnabled" => sourceIni.ReadString(sourceSection, key, "0"),
            "HumanizedPreset" => sourceIni.ReadString(sourceSection, key, "Natural"),
            _ => ""
        };
    }

    private void ExportProfileToFile(string filePath, string profileName)
    {
        var exportIni = new IniFile(filePath);
        exportIni.WriteInt("ProfileExport", "FormatVersion", 1);
        exportIni.WriteString("ProfileExport", "Name", profileName);
        CopyProfileSectionData(_settingsPath, GetProfileSectionName(_activeProfileId), filePath, "ProfileExport", profileName);
    }

    private void SwitchToProfileByName(string profileName)
    {
        var profile = GetProfileByName(profileName);
        if (profile is not null)
        {
            SwitchToProfileById(profile.Id);
        }
    }

    private void SwitchToProfileById(string profileId)
    {
        if (!ProfileExists(profileId) || profileId == _activeProfileId)
        {
            return;
        }

        SaveSettings();
        StopClicking(ClickStopReason.ProfileChange);
        _activeProfileId = profileId;
        LoadProfileSettings(_activeProfileId);
        SaveSettings();
        ApplySettingsToUi();
        UpdateStatus();
    }

    private int GetProfileIndexById(string profileId) => FindProfileIndexById(profileId, _profiles);

    private static int FindProfileIndexById(string profileId, IReadOnlyList<ProfileInfo> profiles)
    {
        for (var i = 0; i < profiles.Count; i++)
        {
            if (profiles[i].Id == profileId)
            {
                return i + 1;
            }
        }

        return 0;
    }

    private int FindProfileIndexByName(string profileName)
    {
        for (var i = 0; i < _profiles.Count; i++)
        {
            if (string.Equals(_profiles[i].Name, profileName, StringComparison.OrdinalIgnoreCase))
            {
                return i + 1;
            }
        }

        return 0;
    }

    private bool ProfileExists(string profileId) => GetProfileIndexById(profileId) > 0;

    private string GetActiveProfileName() => GetProfileNameById(_activeProfileId);

    private string GetProfileNameById(string profileId) => GetProfileById(profileId)?.Name ?? "Default";

    private ProfileInfo? GetProfileById(string profileId) => _profiles.FirstOrDefault(p => p.Id == profileId);

    private ProfileInfo? GetProfileByName(string profileName) => _profiles.FirstOrDefault(p => p.Name == profileName);

    private void ShowProfileMessage(string text)
    {
        MessageBox.Show(this, text, "Profiles", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private bool HasTargetWindowEntry(IEnumerable<TargetWindowInfo> windows, string title, string @class, string exe)
    {
        return windows.Any(entry => IsSameTargetWindow(entry.Title, entry.Class, entry.Exe, title, @class, exe));
    }

    private static bool IsSameTargetWindow(string leftTitle, string leftClass, string leftExe, string rightTitle, string rightClass, string rightExe)
    {
        return string.Equals(leftTitle.Trim(), rightTitle.Trim(), StringComparison.Ordinal)
            && string.Equals(leftClass.Trim(), rightClass.Trim(), StringComparison.Ordinal)
            && string.Equals(leftExe.Trim(), rightExe.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildTargetWindowChoiceLabel(string windowTitle, string windowClass, string windowExe, string matchTitle, string matchClass, string matchExe)
    {
        if (matchExe.Length > 0)
        {
            return StripLegacyAllWindowsSuffix(matchExe);
        }

        if (matchClass.Length > 0)
        {
            return StripLegacyAllWindowsSuffix(windowClass);
        }

        if (matchTitle.Length > 0)
        {
            return StripLegacyAllWindowsSuffix(matchTitle);
        }

        if (windowExe.Length > 0)
        {
            return StripLegacyAllWindowsSuffix(windowExe);
        }

        if (windowTitle.Length > 0)
        {
            return StripLegacyAllWindowsSuffix(windowTitle);
        }

        return windowClass.Length > 0 ? StripLegacyAllWindowsSuffix(windowClass) : "Unknown window";
    }

    private bool HasCapturedTargetWindow()
    {
        return _settings.TargetWindowExe.Trim().Length > 0
            || _settings.TargetWindowClass.Trim().Length > 0
            || _settings.TargetWindowTitle.Trim().Length > 0;
    }

    private string FormatTargetWindowDisplay()
    {
        if (_settings.TargetWindowExe.Trim().Length > 0)
        {
            return StripLegacyAllWindowsSuffix(_settings.TargetWindowExe);
        }

        if (_settings.TargetWindowTitle.Trim().Length > 0)
        {
            return StripLegacyAllWindowsSuffix(_settings.TargetWindowTitle);
        }

        return "Not selected";
    }

    private static string StripLegacyAllWindowsSuffix(string value)
    {
        const string suffix = " (all windows)";
        return value.TrimEnd().EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? value[..^suffix.Length]
            : value;
    }

    private static string NormalizeMode(string mode) => mode.Trim().Equals("toggle", StringComparison.OrdinalIgnoreCase) ? "toggle" : "hold";

    private static string NormalizeStoredHotkey(string hotkey, string fallback)
    {
        if (string.IsNullOrWhiteSpace(hotkey) || hotkey.Trim().Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return fallback;
        }

        return HotkeyChord.TryParse(hotkey, out var chord) ? chord.ToStoredString() : fallback;
    }

    private static string NormalizeHotkey(string hotkey) => HotkeyHelper.NormalizeStoredString(hotkey);

    private static string GetEffectiveTriggerKey(string hotkey) => string.IsNullOrWhiteSpace(hotkey) ? "F2" : hotkey;

    private static string GetEffectivePanicHotkey(string hotkey) => string.IsNullOrWhiteSpace(hotkey) ? "F12" : hotkey;

    private static string GetEffectiveShowWindowHotkey(string hotkey) => string.IsNullOrWhiteSpace(hotkey) ? "F10" : hotkey;

    private static string GetEffectiveTogglePowerHotkey(string hotkey) => string.IsNullOrWhiteSpace(hotkey) ? "F7" : hotkey;

    private string GetEffectiveHotkeyForTarget(string targetName)
    {
        return targetName switch
        {
            "panicHotkey" => GetEffectivePanicHotkey(_settings.PanicHotkey),
            "showWindowHotkey" => GetEffectiveShowWindowHotkey(_settings.ShowWindowHotkey),
            "togglePowerHotkey" => GetEffectiveTogglePowerHotkey(_settings.TogglePowerHotkey),
            _ => GetEffectiveTriggerKey(_settings.TriggerKey)
        };
    }

    private void SetHotkeyTargetValue(string targetName, string value)
    {
        switch (targetName)
        {
            case "panicHotkey":
                _settings.PanicHotkey = value;
                break;
            case "showWindowHotkey":
                _settings.ShowWindowHotkey = value;
                break;
            case "togglePowerHotkey":
                _settings.TogglePowerHotkey = value;
                break;
            default:
                _settings.TriggerKey = value;
                break;
        }
    }

    private static string FormatHotkeyDisplay(string hotkey)
    {
        return HotkeyChord.TryParse(hotkey, out var chord) ? chord.ToDisplayString() : "None";
    }

    private static string NormalizeClickButton(string buttonName) => buttonName == "Right" ? "Right" : "Left";

    private static string FormatClickButtonDisplay(string buttonName) => NormalizeClickButton(buttonName) == "Right" ? "RMB" : "LMB";

    private static string NormalizeClickRateMode(string modeName) => modeName.Trim().Equals("Amplified", StringComparison.OrdinalIgnoreCase) ? "Amplified" : "Ordinary";

    private static string FormatClickRateModeDisplay(string modeName) => NormalizeClickRateMode(modeName) == "Amplified" ? "Rate Amplified" : "Rate Locked";

    private static string NormalizeClickPattern(string patternName)
    {
        return patternName.Trim().ToLowerInvariant() switch
        {
            "burst" => "Burst",
            "double click" => "Double Click",
            "hold then burst" => "Hold then Burst",
            _ => "Standard"
        };
    }

    private static string FormatClickPatternDisplay(string patternName)
    {
        return NormalizeClickPattern(patternName) switch
        {
            "Standard" => "Std",
            "Double Click" => "Double",
            "Hold then Burst" => "Hold+Burst",
            var normalized => normalized
        };
    }

    private static int ClampBurstClickCount(int value) => Math.Clamp(value, 2, 12);

    private static int ClampPatternDelay(int value) => Math.Clamp(value, 0, 250);

    private static string NormalizeHumanizedPreset(string presetName)
    {
        return presetName.Trim().ToLowerInvariant() switch
        {
            "stable" => "Stable",
            "aggressive" => "Aggressive",
            _ => "Natural"
        };
    }

    private string InferPresetFromLegacyRange(string section)
    {
        var legacyBase = ClampCps(_ini.ReadInt(section, "CPS", _ini.ReadInt("Main", "CPS", _settings.Cps)));
        var legacyMin = ClampCps(_ini.ReadInt(section, "HumanizedMinCps", Math.Max(1, legacyBase - 3)));
        var legacyMax = ClampCps(_ini.ReadInt(section, "HumanizedMaxCps", Math.Min(100, legacyBase + 3)));
        var spread = legacyMax - legacyMin;
        if (spread <= 2)
        {
            return "Stable";
        }

        if (spread <= 6)
        {
            return "Natural";
        }

        return "Aggressive";
    }

    private static int ClampCps(int value) => Math.Clamp(value, 1, 100);

    private string CustomPatternHelpText()
    {
        var modeHelp = NormalizeClickRateMode(_settings.ClickRateMode) switch
        {
            "Amplified" => "Amplified lets the pattern add extra taps above the target CPS.",
            _ => "Locked keeps the output tied to your target CPS."
        };

        var patternHelp = _settings.ClickPattern switch
        {
            "Burst" => "Burst sends grouped taps.",
            "Double Click" => "Double Click sends paired taps.",
            "Hold then Burst" => "Hold then Burst starts with a hold, then finishes with a burst pattern.",
            _ => "Standard sends one tap per CPS tick."
        };

        return $"{modeHelp} {patternHelp}";
    }

    private string GetTargetCpsDisplay()
    {
        return !_settings.HumanizedCpsEnabled
            ? $"{_settings.Cps} CPS"
            : $"{_settings.Cps} CPS (Humanized)";
    }

    private static double RandomRange(double min, double max)
    {
        return min + ((max - min) * Random.Shared.NextDouble());
    }

    private void ResetHotkeysToDefaults()
    {
        _settings.TriggerKey = "F2";
        _settings.PanicHotkey = "F12";
        _settings.ShowWindowHotkey = "F10";
        _settings.TogglePowerHotkey = "F7";
        ApplySettings();
    }

    private void ShowTransientBalloon(string text)
    {
        try
        {
            _trayIcon.BalloonTipTitle = "AutoClicker";
            _trayIcon.BalloonTipText = text;
            _trayIcon.ShowBalloonTip(800);
        }
        catch
        {
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WmNclbuttonUp
            && m.WParam.ToInt64() == NativeMethods.HtMinButton
            && _startupCompleted
            && _settings.MinimizeToTrayOnMinimize)
        {
            BeginInvoke(new Action(() => HideToTray(true)));
            return;
        }

        base.WndProc(ref m);
    }

}
