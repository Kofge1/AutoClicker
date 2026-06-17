using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AutoClickerCs;

public sealed partial class MainForm
{    private void StartRecordHotkeyFor(string targetName)
    {
        if (_recordingTargetName is not null)
        {
            return;
        }

        _recordingTargetName = targetName;
        _recordStartTick = Environment.TickCount64;
        SetRecordingDisplay(targetName, "Press a key or mouse button...");
        RefreshHotkeyDisplay(targetName);
        _recordTimeoutTimer.Start();
    }

    private void StopRecordingHotkey()
    {
        _recordTimeoutTimer.Stop();
        if (_recordingTargetName is null)
        {
            return;
        }

        var previousTarget = _recordingTargetName;
        _recordingTargetName = null;
        _recordStartTick = 0;
        SetRecordingDisplay(previousTarget, FormatHotkeyDisplay(GetEffectiveHotkeyForTarget(previousTarget)));
    }

    private void FinishRecordedHotkey(string finalKey)
    {
        if (_recordingTargetName is null)
        {
            return;
        }

        var target = _recordingTargetName;
        _recordingTargetName = null;
        _recordStartTick = 0;
        _recordTimeoutTimer.Stop();
        SetHotkeyTargetValue(target, finalKey);
        ApplySettings();
        SetRecordingDisplay(target, FormatHotkeyDisplay(GetEffectiveHotkeyForTarget(target)));
        RefreshHotkeyDisplay(target);
    }

    private void SetRecordingDisplay(string targetName, string value)
    {
        switch (targetName)
        {
            case "panicHotkey":
                _txtPanicHotkey.Text = value;
                break;
            case "showWindowHotkey":
                _txtShowWindowHotkey.Text = value;
                break;
            case "togglePowerHotkey":
                _txtTogglePowerHotkey.Text = value;
                break;
            case "profileHotkey":
                _txtProfileHotkey.Text = value;
                break;
            default:
                _txtTriggerHotkey.Text = value;
                break;
        }
    }

    private void RefreshHotkeyDisplay(string targetName)
    {
        switch (targetName)
        {
            case "panicHotkey":
                _txtPanicHotkey.Invalidate();
                _txtPanicHotkey.Update();
                break;
            case "showWindowHotkey":
                _txtShowWindowHotkey.Invalidate();
                _txtShowWindowHotkey.Update();
                break;
            case "togglePowerHotkey":
                _txtTogglePowerHotkey.Invalidate();
                _txtTogglePowerHotkey.Update();
                break;
            case "profileHotkey":
                _txtProfileHotkey.Invalidate();
                _txtProfileHotkey.Update();
                break;
            default:
                _txtTriggerHotkey.Invalidate();
                _txtTriggerHotkey.Update();
                break;
        }
    }

    private void RefreshAllHotkeyDisplays()
    {
        _txtTriggerHotkey.Invalidate();
        _txtTriggerHotkey.Update();
        _txtPanicHotkey.Invalidate();
        _txtPanicHotkey.Update();
        _txtShowWindowHotkey.Invalidate();
        _txtShowWindowHotkey.Update();
        _txtTogglePowerHotkey.Invalidate();
        _txtTogglePowerHotkey.Update();
        _txtProfileHotkey.Invalidate();
        _txtProfileHotkey.Update();
    }

    private void OnGlobalInputChanged(object? sender, GlobalInputEventArgs e)
    {
        if (e.IsSelfGenerated || IsDisposed || !IsHandleCreated)
        {
            return;
        }

        BeginInvoke(new Action(() => HandleGlobalInput(e)));
    }

    private void HandleGlobalInput(GlobalInputEventArgs e)
    {
        if (_recordingTargetName is not null)
        {
            HandleRecordingInput(e);
            return;
        }

        if (MatchesChordPress(GetEffectiveChord(GetEffectivePanicHotkey(_settings.PanicHotkey)), e))
        {
            InputDiagnostics.Write($"ServiceHotkey Panic token={e.Token}");
            PrepareForServiceHotkey(GetEffectivePanicHotkey(_settings.PanicHotkey));
            PanicStop();
            return;
        }

        if (MatchesChordPress(GetEffectiveChord(GetEffectiveShowWindowHotkey(_settings.ShowWindowHotkey)), e))
        {
            InputDiagnostics.Write($"ServiceHotkey ShowWindow token={e.Token}");
            PrepareForServiceHotkey(GetEffectiveShowWindowHotkey(_settings.ShowWindowHotkey));
            ShowFromTrayHotkey();
            return;
        }

        if (MatchesChordPress(GetEffectiveChord(GetEffectiveTogglePowerHotkey(_settings.TogglePowerHotkey)), e))
        {
            InputDiagnostics.Write($"ServiceHotkey TogglePower token={e.Token}");
            PrepareForServiceHotkey(GetEffectiveTogglePowerHotkey(_settings.TogglePowerHotkey));
            ToggleEnabledState();
            return;
        }

        if (MatchesChordPress(GetEffectiveChord(GetEffectiveProfileHotkey(_settings.ProfileHotkey)), e))
        {
            InputDiagnostics.Write($"ServiceHotkey NextProfile token={e.Token}");
            PrepareForServiceHotkey(GetEffectiveProfileHotkey(_settings.ProfileHotkey));
            SwitchToNextProfile();
            return;
        }

        var trigger = GetEffectiveChord(GetEffectiveTriggerKey(_settings.TriggerKey));
        if (_settings.CurrentMode == "hold")
        {
            if (_isActive && ShouldStopHoldFromEvent(trigger, e))
            {
                StopClicking(ClickStopReason.HotkeyReleased);
                return;
            }

            if (MatchesChordPress(trigger, e) && _settings.AutoEnabled && !_isActive)
            {
                if (!CanClickInCurrentContext())
                {
                    return;
                }

                StartHoldClicking();
                return;
            }

            if (_isActive && !IsHotkeyStillPressed(_settings.TriggerKey))
            {
                StopClicking(ClickStopReason.HotkeyReleased);
            }

            return;
        }

        if (MatchesChordPress(trigger, e) && _settings.AutoEnabled)
        {
            if (!_isActive && !CanClickInCurrentContext())
            {
                return;
            }

            ToggleClicking();
        }
    }

    private void HandleRecordingInput(GlobalInputEventArgs e)
    {
        if (!e.IsDown || e.WasAlreadyDown || HotkeyHelper.IsModifierToken(e.Token))
        {
            return;
        }

        // Mirror the AHK WatchMouse delay so the click used on the Bind button
        // is not accidentally captured as the new mouse hotkey.
        if (HotkeyHelper.IsMouseToken(e.Token) && Environment.TickCount64 - _recordStartTick < 150)
        {
            return;
        }

        var chord = new HotkeyChord(e.Ctrl, e.Shift, e.Alt, e.Token).ToStoredString();
        if (Regex.IsMatch(chord, @"^[\^\+\!]+$"))
        {
            return;
        }

        FinishRecordedHotkey(chord);
    }

    private void StartHoldClicking()
    {
        if (!_settings.AutoEnabled || _isActive || !CanClickInCurrentContext())
        {
            return;
        }

        _isActive = true;
        ResetHumanizedEngine();
        StartClickLoop();
        ShowTransientBalloon("THE CLICKER IS WORKING");
        UpdateStatus();
    }

    private void ToggleClicking()
    {
        if (!_settings.AutoEnabled)
        {
            return;
        }

        if (_isActive)
        {
            StopClicking(ClickStopReason.Manual);
            ShowTransientBalloon("CLICKER OFF");
            return;
        }

        if (!CanClickInCurrentContext())
        {
            return;
        }

        _isActive = true;
        ResetHumanizedEngine();
        StartClickLoop();
        ShowTransientBalloon("CLICKER ON");
        UpdateStatus();
    }

    private void StartClickingFromTray()
    {
        if (!_settings.AutoEnabled || _settings.CurrentMode != "toggle" || _isActive)
        {
            return;
        }

        _isActive = true;
        ResetHumanizedEngine();
        StartClickLoop();
        UpdateStatus();
    }

    private void StopClickingFromTray()
    {
        if (_isActive)
        {
            StopClicking(ClickStopReason.Manual);
        }
    }

    private void PanicStop()
    {
        if (_isActive || _mouseButtonHeldByClicker.Length > 0 || MouseButtonSafety.HasPressedButtons)
        {
            StopClicking(ClickStopReason.Panic, updateStatus: false);
        }
        else
        {
            _clickCts?.Cancel();
            Interlocked.Increment(ref _clickSessionVersion);
            InputDiagnostics.Write("PanicStop quiet close no active clicker state");
        }

        SaveSettings();
        _allowClose = true;
        Close();
    }

    private void ToggleEnabledState()
    {
        ApplyAutoEnabledState(!_settings.AutoEnabled, updateCheckbox: true);
    }

    private void ApplyAutoEnabledState(bool newValue, bool updateCheckbox)
    {
        var stateChanged = _settings.AutoEnabled != newValue;
        if (updateCheckbox && _chkEnabled.Checked != newValue)
        {
            _suppressUiEvents = true;
            try
            {
                _chkEnabled.Checked = newValue;
            }
            finally
            {
                _suppressUiEvents = false;
            }
        }

        if (!stateChanged)
        {
            return;
        }

        _settings.AutoEnabled = newValue;
        InputDiagnostics.Write($"AutoEnabledChanged value={newValue} active={_isActive}");
        if (!newValue)
        {
            StopClicking(ClickStopReason.Disabled, updateStatus: false);
        }

        QueueAutoEnabledSettingSave();
        UpdateStatus(refreshTrayMenu: false);
        QueueTrayMenuRefresh();
    }

    private void ShowFromTrayHotkey()
    {
        if (Visible && WindowState != FormWindowState.Minimized)
        {
            HideToTray();
            return;
        }

        ShowFromTray();
    }

    private void ShowFromTray()
    {
        var previousOpacity = Opacity > 0 ? Opacity : 1.0;
        Opacity = 0;
        PrepareWindowForTaskbar();
        WindowState = FormWindowState.Normal;
        Show();
        PrepareWindowForTaskbar();
        EnsureWindowOnScreen();
        Refresh();
        Opacity = previousOpacity;
        BringWindowToFront();
        RefreshTrayMenu();
    }

    private void BringWindowToFront()
    {
        PrepareWindowForTaskbar();
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        if (!Visible)
        {
            Show();
        }

        EnsureWindowOnScreen();
        PrepareWindowForTaskbar();
        NativeMethods.ShowWindow(Handle, NativeMethods.SwRestore);
        NativeMethods.ShowWindow(Handle, NativeMethods.SwShow);
        NativeMethods.SetWindowPos(
            Handle,
            NativeMethods.HwndTopmost,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNomove | NativeMethods.SwpNosize | NativeMethods.SwpNoactivate);
        NativeMethods.SetWindowPos(
            Handle,
            NativeMethods.HwndNotopmost,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNomove | NativeMethods.SwpNosize);
        NativeMethods.SetForegroundWindow(Handle);
        BringToFront();
        Activate();
        RefreshTrayMenu();
    }

    private void PrepareWindowForTaskbar()
    {
        ShowInTaskbar = true;
        SetTrayWindowMode(false);
    }

    private void EnsureWindowOnScreen()
    {
        var bounds = Bounds;
        foreach (var screen in Screen.AllScreens)
        {
            if (screen.WorkingArea.IntersectsWith(bounds))
            {
                return;
            }
        }

        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1200, 800);
        StartPosition = FormStartPosition.Manual;
        Location = new Point(
            area.Left + Math.Max(0, (area.Width - Width) / 2),
            area.Top + Math.Max(0, (area.Height - Height) / 2));
    }

    private void HideToTray(bool silent = false)
    {
        Hide();
        ShowInTaskbar = false;
        SetTrayWindowMode(true);
        if (!silent)
        {
            ShowTransientBalloon("Running in tray");
        }

        RefreshTrayMenu();
    }

    private void RefreshTrayMenu()
    {
        var visible = Visible && WindowState != FormWindowState.Minimized;
        _trayMenu.Items.Clear();
        _trayMenu.Items.Add(visible ? "Hide AutoClicker" : "Open AutoClicker", null, (_, _) =>
        {
            if (visible)
            {
                HideToTray();
            }
            else
            {
                ShowFromTray();
            }
        });
        _trayMenu.Items.Add(_settings.AutoEnabled ? "Disable Clicker" : "Enable Clicker", null, (_, _) => ToggleEnabledState());

        if (_settings.CurrentMode == "toggle")
        {
            _trayMenu.Items.Add(_isActive ? "Stop Clicking" : "Start Clicking", null, (_, _) =>
            {
                if (_isActive)
                {
                    StopClickingFromTray();
                }
                else
                {
                    StartClickingFromTray();
                }
            });
        }
        else
        {
            var item = _trayMenu.Items.Add("Hold Mode Active");
            item.Enabled = false;
        }

        var profilesMenu = new ToolStripMenuItem("Profiles");
        foreach (var profile in _profiles)
        {
            var item = new ToolStripMenuItem(profile.Name)
            {
                Checked = profile.Id == _activeProfileId
            };
            item.Click += (_, _) => SwitchToProfileById(profile.Id);
            profilesMenu.DropDownItems.Add(item);
        }

        _trayMenu.Items.Add(profilesMenu);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("Exit", null, (_, _) => ExitHandler());
    }

    private void ExitHandler()
    {
        SaveSettings();
        _allowClose = true;
        Close();
    }

    private void RequestCloseWindow()
    {
        if (_settings.CloseToTrayOnClose)
        {
            HideToTray();
            return;
        }

        ExitHandler();
    }


}
