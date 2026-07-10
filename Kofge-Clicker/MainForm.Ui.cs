using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace KofgeClicker;

public sealed partial class MainForm
{
    private static string ResolveSettingsPath(string localSettingsPath, string executableSettingsPath, string legacySettingsPath)
    {
        if (File.Exists(localSettingsPath))
        {
            return localSettingsPath;
        }

        if (File.Exists(executableSettingsPath))
        {
            return CopyInitialSettingsFile(executableSettingsPath, localSettingsPath);
        }

        if (File.Exists(legacySettingsPath))
        {
            return CopyInitialSettingsFile(legacySettingsPath, localSettingsPath);
        }

        return localSettingsPath;
    }

    private static string CopyInitialSettingsFile(string sourceSettingsPath, string localSettingsPath)
    {
        try
        {
            var directory = Path.GetDirectoryName(localSettingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(sourceSettingsPath, localSettingsPath, false);
            EnsureSafeInitialSettings(localSettingsPath);
        }
        catch
        {
        }

        return localSettingsPath;
    }

    private static void EnsureSafeInitialSettings(string settingsPath)
    {
        var ini = new IniFile(settingsPath);
        ini.WriteBool("Main", "AutoEnabled", false);
        ini.WriteBool("Main", "StartMinimized", false);
        ini.WriteBool("Main", "MinimizeToTrayOnMinimize", false);
        ini.WriteBool("Main", "RunOnWindowsStartup", false);
        ini.WriteBool("Main", "CloseToTrayOnClose", false);
        ini.WriteBool("Main", "RestrictToFocusedWindow", false);
        ini.WriteString("Main", "TargetWindowTitle", string.Empty);
        ini.WriteString("Main", "TargetWindowClass", string.Empty);
        ini.WriteString("Main", "TargetWindowExe", string.Empty);

        var count = ini.ReadInt("Profiles", "Count", 0);
        for (var i = 1; i <= count; i++)
        {
            var profileId = ini.ReadString("Profiles", $"Profile{i}", string.Empty);
            if (!string.IsNullOrWhiteSpace(profileId))
            {
                var section = GetProfileSectionName(profileId);
                ini.WriteBool(section, "AutoEnabled", false);
                ini.WriteBool(section, "CloseToTrayOnClose", false);
                ini.WriteBool(section, "RestrictToFocusedWindow", false);
                ini.WriteString(section, "TargetWindowTitle", string.Empty);
                ini.WriteString(section, "TargetWindowClass", string.Empty);
                ini.WriteString(section, "TargetWindowExe", string.Empty);
            }
        }
    }

    private void BuildUi()
    {
        SuspendLayout();
        Text = "Kofge-Clicker";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimumSize = new Size(1116, 675);
        MaximumSize = new Size(1116, 675);
        ClientSize = new Size(1100, 636);
        BackColor = UiTheme.AppBackground;
        ForeColor = UiTheme.TextPrimary;
        Icon = _baseAppIcon;

        _tabBodyShell = new RoundedPanel
        {
            Left = 10,
            Top = 10,
            Width = 1080,
            Height = 514,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            FillColor = UiTheme.CardOuter,
            BorderColor = Color.FromArgb(96, 102, 120),
            Radius = 24,
            DrawShadow = false
        };

        _tabs = new ThemedTabControl
        {
            Left = 10,
            Top = 50,
            Width = 1060,
            Height = 390,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };
        _tabs.Multiline = true;
        _tabs.SizeMode = TabSizeMode.Fixed;
        _tabs.ItemSize = new Size(0, 1);
        _tabs.Appearance = TabAppearance.FlatButtons;
        _tabs.Padding = new Point(0, 0);
        _tabs.SelectedIndexChanged += (_, _) => UpdateTabHeaderVisuals();

        BuildClickerTab();
        BuildPatternTab();
        BuildMouseTab();
        BuildHotkeyTab();
        BuildProfilesTab();
        BuildOptionsTab();
        BuildTabHeader();

        _btnApply = CreateButton("Apply", 250, 552, 220, this, (_, _) => ApplySettings(), primary: true);
        _btnApply.Height = 42;
        _btnApply.Anchor = AnchorStyles.Bottom;

        _btnClose = CreateButton("Close", 520, 552, 220, this, (_, _) => RequestCloseWindow());
        _btnClose.Height = 42;
        _btnClose.Anchor = AnchorStyles.Bottom;

        _lblStatus = new Label
        {
            Left = 24,
            Top = 8,
            Width = 781,
            Height = 64,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
            ForeColor = UiTheme.TextPrimary,
            Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold)
        };

        _statusCard = new RoundedPanel
        {
            Left = 136,
            Top = 534,
            Width = 829,
            Height = 92,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            FillColor = Color.FromArgb(36, 43, 59),
            BorderColor = Color.FromArgb(60, 80, 108)
        };
        _statusCard.Controls.Add(_lblStatus);

        _lblVersion = new Label
        {
            Left = 985,
            Top = 552,
            Width = 88,
            Height = 44,
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            Text = AppVersion.Display,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
            ForeColor = UiTheme.TextMuted,
            Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold)
        };

        _tabBodyShell.Controls.Add(_tabHeader);
        _tabBodyShell.Controls.Add(_tabs);
        _tabBodyShell.Controls.Add(_btnApply);
        _tabBodyShell.Controls.Add(_btnClose);
        _btnApply.BringToFront();
        _btnClose.BringToFront();
        Controls.Add(_tabBodyShell);
        Controls.Add(_statusCard);
        Controls.Add(_lblVersion);

        LayoutFooterButtons();
        Resize += OnFormResize;
        Shown += OnFormShown;
        FormClosing += OnFormClosingInternal;

        ApplyDarkTheme(this);
        ResumeLayout(false);
    }

    private void BuildTabHeader()
    {
        var tabFont = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);
        const int gap = 21;
        const int buttonHeight = 50;
        const int minButtonWidth = 128;
        const int horizontalPadding = 42;

        _tabHeader = new Panel
        {
            Left = 10,
            Top = 0,
            Width = 1060,
            Height = 64,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = UiTheme.CardOuter
        };

        _tabButtons.Clear();
        var buttonWidths = new List<int>();
        var totalWidth = 0;
        for (var i = 0; i < _tabs.TabPages.Count; i++)
        {
            var tabText = _tabs.TabPages[i].Text;
            var measured = TextRenderer.MeasureText(tabText, tabFont);
            var width = Math.Max(minButtonWidth, measured.Width + horizontalPadding);
            buttonWidths.Add(width);
            totalWidth += width;
        }

        totalWidth += gap * Math.Max(0, buttonWidths.Count - 1);
        var visualZoneLeft = _clickerCard.Left;
        var visualZoneWidth = _clickerCard.Width;
        var x = visualZoneLeft + Math.Max(0, (visualZoneWidth - totalWidth) / 2);
        for (var i = 0; i < _tabs.TabPages.Count; i++)
        {
            var index = i;
            var tabText = _tabs.TabPages[i].Text;
            var width = buttonWidths[i];
            var button = new AccentButton
            {
                Text = tabText,
                Left = x,
                Top = ((_tabHeader.Height - buttonHeight) / 2) + 8,
                Width = width,
                Height = buttonHeight,
                Font = tabFont,
                Primary = i == _tabs.SelectedIndex
            };
            button.Click += (_, _) => _tabs.SelectedIndex = index;
            _tabButtons.Add(button);
            _tabHeader.Controls.Add(button);
            x += width + gap;
        }

        UpdateTabHeaderVisuals();
    }

    private void UpdateTabHeaderVisuals()
    {
        for (var i = 0; i < _tabButtons.Count; i++)
        {
            _tabButtons[i].Primary = i == _tabs.SelectedIndex;
            _tabButtons[i].Invalidate();
        }
    }

    private void BuildClickerTab()
    {
        var tab = CreateTabPage("Clicker");
        var card = CreateCard(tab, 52, 32, 956, 356, "Clicker");
        _clickerCard = card;

        const int mainBlockShiftY = 20;

        var enabledLabel = CreateLabel("Enabled", 32, 75 + mainBlockShiftY, 92);
        card.Controls.Add(enabledLabel);
        _chkEnabled = CreateToggleSwitch(172, 67 + mainBlockShiftY, 112, card, (_, _) => OnEnabledChange());
        ConfigureOnOffToggle(_chkEnabled);

        var hotkeyLabel = CreateLabel("Hotkey", 560, 127 + mainBlockShiftY, 78);
        hotkeyLabel.Height = 32;
        hotkeyLabel.TextAlign = ContentAlignment.MiddleLeft;
        card.Controls.Add(hotkeyLabel);
        _txtTriggerHotkey = new InfoPill
        {
            Left = 648,
            Top = 128 + mainBlockShiftY,
            Width = 146,
            Height = 32
        };
        card.Controls.Add(_txtTriggerHotkey);
        _txtTriggerHotkey.TextChanged += (_, _) => UpdateTriggerHotkeyDisplayLayout();
        _btnBindTrigger = CreateButton("Bind", 802, 125 + mainBlockShiftY, 96, card, (_, _) => StartRecordHotkeyFor("triggerKey"), primary: true);

        var modeLabel = CreateLabel("Mode", 32, 123 + mainBlockShiftY, 92);
        modeLabel.Height = 42;
        modeLabel.TextAlign = ContentAlignment.MiddleLeft;
        card.Controls.Add(modeLabel);
        var modeGroup = new Panel
        {
            Left = 172,
            Top = 124 + mainBlockShiftY,
            Width = 297,
            Height = 42,
            BackColor = UiTheme.CardInner
        };
        card.Controls.Add(modeGroup);
        _rbHold = CreateSegmentRadioButton("Hold", 0, 0, 146, modeGroup, (_, _) => OnModeChanged(), primarySegment: true);
        _rbToggle = CreateSegmentRadioButton("Toggle", 151, 0, 146, modeGroup, (_, _) => OnModeChanged());

        _trkCps = new ModernSlider
        {
            Left = 143,
            Top = 228 + mainBlockShiftY,
            Width = 670,
            Minimum = 1,
            Maximum = 100
        };
        _trkCps.ValueChanged += (_, _) => UpdateFromSlider();
        _trkCps.MouseUp += (_, _) => CommitSliderCpsIfPending();
        _trkCps.KeyUp += (_, _) => CommitSliderCpsIfPending();
        _trkCps.MouseCaptureChanged += (_, _) => CommitSliderCpsIfPending();
        _trkCps.Leave += (_, _) => CommitSliderCpsIfPending();
        card.Controls.Add(_trkCps);

        var minCpsLabel = CreateMutedLabel("1", 96, 238 + mainBlockShiftY, 28);
        minCpsLabel.Font = new Font("Segoe UI", 18f, FontStyle.Regular);
        minCpsLabel.Height = 32;
        card.Controls.Add(minCpsLabel);

        var maxCpsLabel = CreateMutedLabel("100", 784, 238 + mainBlockShiftY, 72, alignRight: true);
        maxCpsLabel.Font = new Font("Segoe UI", 18f, FontStyle.Regular);
        maxCpsLabel.Height = 32;
        card.Controls.Add(maxCpsLabel);
        _lblCpsValue = new Label
        {
            Left = 392,
            Top = 185 + mainBlockShiftY,
            Width = 170,
            Height = 37,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
            ForeColor = UiTheme.TextPrimary,
            Font = new Font("Segoe UI Semibold", 24f, FontStyle.Bold),
            Cursor = Cursors.IBeam
        };
        _lblCpsValue.Click += (_, _) => BeginCpsEdit();
        card.Controls.Add(_lblCpsValue);

        _txtCps = CreateEditableBox(392, 185 + mainBlockShiftY, 170, card);
        _txtCps.Leave += (_, _) => UpdateFromEdit();
        _txtCps.KeyDown += OnCpsTextBoxKeyDown;
        _txtCps.Enter += (_, _) => _txtCps.SelectAll();
        _txtCps.TextAlign = HorizontalAlignment.Center;
        _txtCps.BorderStyle = BorderStyle.None;
        _txtCps.BackColor = UiTheme.CardInner;
        _txtCps.Font = new Font("Segoe UI Semibold", 24f, FontStyle.Bold);
        _txtCps.Visible = false;

        card.MouseDown += (_, _) => CommitCpsEditIfVisible();
        tab.MouseDown += (_, _) => CommitCpsEditIfVisible();

        card.Controls.Add(CreateLabel("Humanized", 560, 95, 140));
        _chkHumanized = CreateToggleSwitch(722, 87, 118, card, (_, _) => OnHumanizedToggle());
        ConfigureOnOffToggle(_chkHumanized);
        _humanizedPresetGroup = new Panel
        {
            Left = 410,
            Top = 295,
            Width = 348,
            Height = 40,
            BackColor = UiTheme.CardInner,
            Visible = false,
            Enabled = false
        };
        card.Controls.Add(_humanizedPresetGroup);
        _rbPresetStable = CreateSegmentRadioButton("Stable", 0, 0, 102, _humanizedPresetGroup, (_, _) => SelectHumanizedPreset("Stable"), primarySegment: true);
        _rbPresetNatural = CreateSegmentRadioButton("Natural", 104, 0, 110, _humanizedPresetGroup, (_, _) => SelectHumanizedPreset("Natural"));
        _rbPresetAggressive = CreateSegmentRadioButton("Aggressive", 216, 0, 128, _humanizedPresetGroup, (_, _) => SelectHumanizedPreset("Aggressive"));

        _tabs.Controls.Add(tab);
    }

    private void BuildPatternTab()
    {
        var tab = CreateTabPage("Pattern");
        var patternCard = CreateCard(tab, StandardTabCardLeft, StandardTabCardTop, StandardTabCardWidth, StandardTabCardHeight, "Click Pattern");

        patternCard.Controls.Add(CreateLabel("Pattern", 26, 76, 88));
        _cmbPattern = CreatePillDropdown(164, 68, 240, patternCard, ["Standard", "Burst", "Double Click", "Hold then Burst"]);
        _cmbPattern.SelectedIndexChanged += (_, _) => OnClickPatternChanged();

        patternCard.Controls.Add(CreateLabel("Clicks", 26, 128, 70));
        patternCard.Controls.Add(CreateLabel("Gap ms", 146, 128, 70));
        patternCard.Controls.Add(CreateLabel("Hold ms", 266, 128, 70));
        patternCard.Controls.Add(CreateLabel("Press ms", 386, 128, 70));
        patternCard.Controls.Add(CreateLabel("Release ms", 506, 128, 90));

        _txtBurstCount = CreatePillValueEditor(26, 154, 86, patternCard);
        _txtBurstGap = CreatePillValueEditor(146, 154, 86, patternCard);
        _txtHoldBurst = CreatePillValueEditor(266, 154, 86, patternCard);
        _txtPressDelay = CreatePillValueEditor(386, 154, 86, patternCard);
        _txtReleaseDelay = CreatePillValueEditor(506, 154, 86, patternCard);

        _txtBurstCount.ValueCommitted += (_, _) => UpdatePatternNumber("burstCount");
        _txtBurstGap.ValueCommitted += (_, _) => UpdatePatternNumber("burstGap");
        _txtHoldBurst.ValueCommitted += (_, _) => UpdatePatternNumber("holdBurst");
        _txtPressDelay.ValueCommitted += (_, _) => UpdatePatternNumber("pressDelay");
        _txtReleaseDelay.ValueCommitted += (_, _) => UpdatePatternNumber("releaseDelay");

        patternCard.Controls.Add(CreateLabel("Rate behavior", 26, 243, 132));
        var rateGroup = new Panel
        {
            Left = 164,
            Top = 236,
            Width = 258,
            Height = 40,
            BackColor = UiTheme.CardInner
        };
        patternCard.Controls.Add(rateGroup);
        _rbRateLocked = CreateSegmentRadioButton("Locked", 0, 0, 118, rateGroup, (_, _) => OnClickRateModeChanged(), primarySegment: true);
        _rbRateAmplified = CreateSegmentRadioButton("Amplified", 120, 0, 136, rateGroup, (_, _) => OnClickRateModeChanged());

        _lblPatternHelp = new Label
        {
            Left = 26,
            Top = 278,
            Width = 880,
            Height = 54,
            AutoSize = false
        };
        UiTheme.StyleLabel(_lblPatternHelp, muted: true);
        patternCard.Controls.Add(_lblPatternHelp);
        _tabs.Controls.Add(tab);
    }

    private void BuildMouseTab()
    {
        var tab = CreateTabPage("Mouse");
        var card = CreateCard(tab, StandardTabCardLeft, StandardTabCardTop, StandardTabCardWidth, StandardTabCardHeight, "Mouse Button");
        card.Controls.Add(CreateLabel("Mouse", 26, 76, 88));
        _cmbClickButton = CreatePillDropdown(122, 68, 220, card, ["Left", "Right"]);
        _cmbClickButton.SelectedIndexChanged += (_, _) => OnClickButtonChanged();

        var mouseHelp = new Label
        {
            Left = 26,
            Top = 122,
            Width = 420,
            Height = 34,
            AutoSize = false,
            Text = "Choose which mouse button Kofge-Clicker presses.",
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.TopLeft
        };
        UiTheme.StyleLabel(mouseHelp, muted: true);
        card.Controls.Add(mouseHelp);
        _tabs.Controls.Add(tab);
    }

    private void BuildHotkeyTab()
    {
        var tab = CreateTabPage("Hotkey");
        var card = CreateCard(tab, StandardTabCardLeft, StandardTabCardTop, StandardTabCardWidth, StandardTabCardHeight, "Service Hotkeys");
        BuildHotkeyRow(card, "Panic Stop", 30, 51, out _txtPanicHotkey, out var btnPanic, "panicHotkey");
        BuildHotkeyRow(card, "Show Window", 30, 121, out _txtShowWindowHotkey, out var btnShow, "showWindowHotkey");
        BuildHotkeyRow(card, "Toggle Power", 30, 191, out _txtTogglePowerHotkey, out var btnToggle, "togglePowerHotkey");
        BuildHotkeyRow(card, "Next Profile", 30, 261, out _txtProfileHotkey, out var btnProfile, "profileHotkey");
        _ = btnPanic;
        _ = btnShow;
        _ = btnToggle;
        _ = btnProfile;

        var btnReset = CreateButton("Reset All Hotkeys", 560, 179, 300, card, (_, _) => ResetHotkeysToDefaults());
        _ = btnReset;
        _tabs.Controls.Add(tab);
    }

    private void BuildProfilesTab()
    {
        var tab = CreateTabPage("Profiles");
        var card = CreateCard(tab, StandardTabCardLeft, StandardTabCardTop, StandardTabCardWidth, StandardTabCardHeight, "Profiles");
        const int profileRowTop = 68;
        const int profileRowHeight = 40;
        const int rowGap = 10;
        const int actionButtonsTop = profileRowTop + profileRowHeight + rowGap;
        const int actionButtonHeight = 38;
        const int startupProfileTop = actionButtonsTop + actionButtonHeight + rowGap;

        card.Controls.Add(CreateLabel("Current profile", 30, 76, 150));
        _cmbProfiles = CreatePillDropdown(184, profileRowTop, 240, card, []);
        _cmbProfiles.SelectedIndexChanged += (_, _) => OnProfileSelected();

        CreateButton("New", 442, 66, 90, card, (_, _) => CreateProfile(), primary: true);
        CreateButton("Rename", 30, actionButtonsTop, 100, card, (_, _) => RenameProfile());
        CreateButton("Duplicate", 142, actionButtonsTop, 110, card, (_, _) => DuplicateProfile());
        CreateButton("Delete", 264, actionButtonsTop, 100, card, (_, _) => DeleteProfile());
        CreateButton("Export", 376, actionButtonsTop, 100, card, (_, _) => ExportProfile());
        CreateButton("Import", 488, actionButtonsTop, 100, card, (_, _) => ImportProfile());
        _btnSetStartup = CreateButton("Set Startup", 600, actionButtonsTop, 120, card, (_, _) => SetCurrentProfileAsDefault());
        _btnRememberProfileFlag = CreateButton("Remember profile", 30, 194, 176, card, (_, _) => { });
        _btnRememberProfileFlag.Visible = false;

        _btnRememberProfileValue = CreateButton("Last used profile", 211, 194, 176, card, (_, _) => { });
        _btnRememberProfileValue.Visible = false;

        _lblStartupProfile = new Label
        {
            Left = 30,
            Top = startupProfileTop,
            Width = 760,
            Height = 28
        };
        UiTheme.StyleLabel(_lblStartupProfile, muted: true);
        card.Controls.Add(_lblStartupProfile);

        var dataLocationLabel = new Label
        {
            Left = 30,
            Top = 282,
            Width = 900,
            Height = 28,
            Text = $"Profiles, settings and logs:   {AppPaths.DataDirectory}"
        };
        UiTheme.StyleLabel(dataLocationLabel, muted: true);
        card.Controls.Add(dataLocationLabel);
        _tabs.Controls.Add(tab);
    }

    private void BuildOptionsTab()
    {
        var tab = CreateTabPage("Options");
        var leftCard = CreateCard(tab, StandardTabCardLeft, StandardTabCardTop, 444, StandardTabCardHeight, "Window and Tray");
        var rightCard = CreateCard(tab, StandardTabCardLeft + 470, StandardTabCardTop, 486, StandardTabCardHeight, "Window Target");

        _chkRunAsAdministrator = CreateOptionToggleRow(leftCard, "Run as administrator", 28, 78, 222, 276, (_, _) => OnRunAsAdministratorToggle());
        _chkStartMinimized = CreateOptionToggleRow(leftCard, "Start hidden to tray", 28, 128, 222, 276, (_, _) => OnStartMinimizedToggle());
        _chkRunOnStartup = CreateOptionToggleRow(leftCard, "Run on startup", 28, 178, 222, 276, (_, _) => OnRunOnStartupToggle());
        _chkRememberProfile = CreateOptionToggleRow(leftCard, "Remember profile", 28, 228, 222, 276, (_, _) => OnRememberLastProfileToggle());
        if (leftCard.Controls.Count >= 2 && leftCard.Controls[^2] is Label rememberProfileLabel)
        {
            rememberProfileLabel.Visible = false;
            rememberProfileLabel.Enabled = false;
        }
        _chkRememberProfile.Visible = false;
        _chkRememberProfile.Enabled = false;
        _chkMinimizeToTray = CreateOptionToggleRow(leftCard, "Minimize button to tray", 28, 228, 222, 276, (_, _) => OnMinimizeToTrayToggle());
        _chkCloseToTray = CreateOptionToggleRow(leftCard, "Close window to tray", 28, 278, 222, 276, (_, _) => OnCloseToTrayToggle());

        _chkRestrictWindow = CreateOptionToggleRow(rightCard, "Only while selected window is focused", 28, 69, 266, 332, (_, _) => OnRestrictWindowToggle());
        _cmbTargetWindow = CreatePillDropdown(28, 123, 320, rightCard, ["Any window"]);
        _cmbTargetWindow.SelectedIndexChanged += (_, _) => OnTargetWindowSelected();
        _btnRefreshWindows = CreateButton("Refresh", 364, 121, 94, rightCard, (_, _) => RefreshTargetWindowList(), primary: true);
        _lblTargetWindow = new Label
        {
            Left = 28,
            Top = 171,
            Width = 420,
            Height = 28
        };
        UiTheme.StyleLabel(_lblTargetWindow, muted: true);
        rightCard.Controls.Add(_lblTargetWindow);
        _tabs.Controls.Add(tab);
    }

    private void BuildHotkeyRow(Control parent, string label, int x, out InfoPill box, out Button button, string targetName)
    {
        BuildHotkeyRow(parent, label, x, 32, out box, out button, targetName);
    }

    private void BuildHotkeyRow(Control parent, string label, int x, int y, out InfoPill box, out Button button, string targetName)
    {
        var rowLabel = CreateLabel(label, x, y + 29, 168);
        rowLabel.Height = 32;
        rowLabel.TextAlign = ContentAlignment.MiddleLeft;
        parent.Controls.Add(rowLabel);

        box = CreateInfoPill(x + 174, y + 29, 164, parent);
        button = CreateButton("Bind", x + 346, y + 25, 96, parent, (_, _) => StartRecordHotkeyFor(targetName), primary: true);
    }

    private TabPage CreateTabPage(string text)
    {
        return new TabPage(text)
        {
            BackColor = UiTheme.CardOuter,
            ForeColor = UiTheme.TextPrimary
        };
    }

    private Label CreateLabel(string text, int left, int top, int width)
    {
        var label = new Label
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = 24,
            BackColor = Color.Transparent
        };
        UiTheme.StyleLabel(label);
        return label;
    }

    private Label CreateMutedLabel(string text, int left, int top, int width, bool alignRight = false)
    {
        var label = new Label
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = 18,
            BackColor = Color.Transparent,
            TextAlign = alignRight ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft
        };
        UiTheme.StyleLabel(label, muted: true);
        return label;
    }

    private CheckBox CreateCheckBox(string text, int left, int top, int width, Control parent, EventHandler handler)
    {
        var checkBox = new CheckBox
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            BackColor = Color.Transparent
        };
        UiTheme.StyleCheckLike(checkBox);
        checkBox.CheckedChanged += handler;
        parent.Controls.Add(checkBox);
        return checkBox;
    }

    private CheckBox CreateOptionToggleRow(Control parent, string text, int left, int top, int labelWidth, int toggleLeft, EventHandler handler)
    {
        var label = CreateLabel(text, left, top + 6, labelWidth);
        label.Height = 28;
        label.TextAlign = ContentAlignment.MiddleLeft;
        parent.Controls.Add(label);

        var toggle = CreateToggleSwitch(toggleLeft, top, 112, parent, handler);
        ConfigureOnOffToggle(toggle);
        return toggle;
    }

    private ToggleSwitchCheckBox CreateToggleSwitch(int left, int top, int width, Control parent, EventHandler handler)
    {
        var toggle = new ToggleSwitchCheckBox
        {
            Left = left,
            Top = top,
            Width = width
        };
        toggle.CheckedChanged += handler;
        parent.Controls.Add(toggle);
        return toggle;
    }

    private static void ConfigureOnOffToggle(CheckBox toggle)
    {
        if (toggle is not ToggleSwitchCheckBox stateToggle)
        {
            return;
        }

        stateToggle.UseSlidingKnob = false;
        stateToggle.CheckedFillColor = Color.FromArgb(60, 153, 84);
        stateToggle.CheckedBorderColor = Color.FromArgb(112, 198, 130);
        stateToggle.UncheckedFillColor = Color.FromArgb(164, 58, 58);
        stateToggle.UncheckedBorderColor = Color.FromArgb(220, 102, 102);
    }

    private RadioButton CreateRadioButton(string text, int left, int top, int width, Control parent, EventHandler handler)
    {
        var radio = new RadioButton
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = 28,
            AutoSize = false,
            BackColor = Color.Transparent
        };
        UiTheme.StyleCheckLike(radio);
        radio.FlatStyle = FlatStyle.Standard;
        radio.CheckedChanged += handler;
        parent.Controls.Add(radio);
        return radio;
    }

    private SegmentRadioButton CreateSegmentRadioButton(string text, int left, int top, int width, Control parent, EventHandler handler, bool primarySegment = false)
    {
        var radio = new SegmentRadioButton
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            PrimarySegment = primarySegment
        };
        radio.CheckedChanged += handler;
        parent.Controls.Add(radio);
        return radio;
    }

    private TextBox CreateReadOnlyBox(int left, int top, int width, Control parent)
    {
        var box = new TextBox
        {
            Left = left,
            Top = top,
            Width = width,
            ReadOnly = true,
            HideSelection = true
        };
        UiTheme.StyleInput(box);
        parent.Controls.Add(box);
        return box;
    }

    private InfoPill CreateInfoPill(int left, int top, int width, Control parent)
    {
        var pill = new InfoPill
        {
            Left = left,
            Top = top,
            Width = width,
            Height = 32
        };
        parent.Controls.Add(pill);
        return pill;
    }

    private void UpdateTriggerHotkeyDisplayLayout()
    {
        if (_txtTriggerHotkey is null)
        {
            return;
        }

        const float maxSize = 14f;
        const float minSize = 8f;
        var selectedFont = maxSize;
        var text = _txtTriggerHotkey.Text;
        var availableWidth = Math.Max(20, _txtTriggerHotkey.ClientSize.Width - 10);

        for (var size = maxSize; size >= minSize; size -= 0.5f)
        {
            using var testFont = new Font("Segoe UI", size, FontStyle.Regular);
            var measured = TextRenderer.MeasureText(text, testFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            if (measured.Width <= availableWidth)
            {
                selectedFont = size;
                break;
            }
        }

        _txtTriggerHotkey.Font = new Font("Segoe UI", selectedFont, FontStyle.Regular);
        _txtTriggerHotkey.Invalidate();
    }

    private TextBox CreateEditableBox(int left, int top, int width, Control parent)
    {
        var box = new TextBox
        {
            Left = left,
            Top = top,
            Width = width
        };
        UiTheme.StyleInput(box);
        parent.Controls.Add(box);
        return box;
    }

    private PillValueEditor CreatePillValueEditor(int left, int top, int width, Control parent)
    {
        var box = new PillValueEditor
        {
            Left = left,
            Top = top,
            Width = width,
            Height = 40,
            FillColor = UiTheme.Surface,
            BorderColor = Color.FromArgb(76, 86, 118)
        };
        parent.Controls.Add(box);
        return box;
    }

    private PillDropdown CreatePillDropdown(int left, int top, int width, Control parent, IEnumerable<string> items)
    {
        var combo = new PillDropdown
        {
            Left = left,
            Top = top,
            Width = width,
            Height = 40,
            FillColor = UiTheme.Surface,
            BorderColor = Color.FromArgb(76, 86, 118)
        };
        combo.SetItems(items);
        parent.Controls.Add(combo);
        return combo;
    }

    private AccentButton CreateButton(string text, int left, int top, int width, Control parent, EventHandler handler, bool primary = false)
    {
        var button = new AccentButton
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = 38,
            Primary = primary
        };
        button.Click += handler;
        parent.Controls.Add(button);
        return button;
    }

    private RoundedPanel CreateCard(Control parent, int left, int top, int width, int height, string title)
    {
        var card = new RoundedPanel
        {
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            FillColor = UiTheme.CardInner,
            BorderColor = Color.FromArgb(72, 89, 127)
        };

        var titleLabel = new Label
        {
            Left = 20,
            Top = 10,
            Width = width - 40,
            Height = 32,
            Text = title
        };
        UiTheme.StyleLabel(titleLabel, section: true);
        card.Controls.Add(titleLabel);

        var divider = new Panel
        {
            Left = 20,
            Top = 52,
            Width = width - 40,
            Height = 1,
            BackColor = Color.FromArgb(78, 92, 129)
        };
        card.Controls.Add(divider);

        parent.Controls.Add(card);
        return card;
    }

    private void ApplyDarkTheme(Control control)
    {
        foreach (Control child in control.Controls)
        {
            if (child is AccentButton)
            {
            }
            else if (child is Button)
            {
                child.BackColor = UiTheme.Surface;
                child.ForeColor = UiTheme.TextPrimary;
            }
            else if (child is TextBox or ComboBox)
            {
                child.BackColor = UiTheme.Surface;
                child.ForeColor = UiTheme.TextPrimary;
            }

            if (child == _txtCps)
            {
                child.BackColor = UiTheme.CardInner;
                child.ForeColor = UiTheme.TextPrimary;
            }
            else if (child is Label or CheckBox or RadioButton)
            {
                child.ForeColor = UiTheme.TextPrimary;
            }

            ApplyDarkTheme(child);
        }
    }

    private Icon LoadAppIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath))
        {
            return new Icon(iconPath);
        }

        try
        {
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    private void UpdateStatusIcon()
    {
        var statusIcon = !_settings.AutoEnabled
            ? _disabledStatusIcon ??= CreateStatusIcon(StatusIconState.Disabled)
            : _isActive
                ? _activeStatusIcon ??= CreateStatusIcon(StatusIconState.Active)
                : _enabledStatusIcon ??= CreateStatusIcon(StatusIconState.Enabled);

        Icon = statusIcon;
        _trayIcon.Icon = statusIcon;
    }

    private Icon CreateStatusIcon(StatusIconState state)
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawIcon(_baseAppIcon, new Rectangle(0, 0, size, size));

            if (state == StatusIconState.Active)
            {
                DrawStatusBolt(graphics);
            }
            else if (state == StatusIconState.Enabled)
            {
                DrawStatusCheck(graphics);
            }
            else
            {
                DrawStatusCross(graphics);
            }
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    private enum StatusIconState
    {
        Disabled,
        Enabled,
        Active
    }

    private static void DrawStatusCheck(Graphics graphics)
    {
        using var shadowPen = new Pen(Color.FromArgb(210, 0, 0, 0), 4.4f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        using var checkPen = new Pen(Color.FromArgb(70, 230, 105), 3.0f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        PointF[] points =
        [
            new(20.0f, 25.5f),
            new(23.8f, 29.0f),
            new(30.5f, 20.8f)
        ];

        graphics.DrawLines(shadowPen, points);
        graphics.DrawLines(checkPen, points);
    }

    private static void DrawStatusCross(Graphics graphics)
    {
        using var shadowPen = new Pen(Color.FromArgb(210, 0, 0, 0), 4.4f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var crossPen = new Pen(Color.FromArgb(242, 68, 68), 3.0f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        DrawCrossLines(graphics, shadowPen);
        DrawCrossLines(graphics, crossPen);
    }

    private static void DrawStatusBolt(Graphics graphics)
    {
        PointF[] shadowPoints =
        [
            new(25.0f, 18.0f),
            new(19.6f, 26.2f),
            new(24.1f, 26.2f),
            new(21.1f, 31.2f),
            new(30.4f, 22.7f),
            new(25.8f, 22.7f)
        ];
        PointF[] boltPoints =
        [
            new(24.8f, 17.6f),
            new(19.6f, 25.6f),
            new(24.0f, 25.6f),
            new(21.2f, 30.6f),
            new(30.0f, 22.2f),
            new(25.5f, 22.2f)
        ];

        using var shadowBrush = new SolidBrush(Color.FromArgb(220, 0, 0, 0));
        using var boltBrush = new SolidBrush(Color.FromArgb(255, 212, 54));
        using var borderPen = new Pen(Color.FromArgb(255, 245, 148), 1.2f)
        {
            LineJoin = LineJoin.Round
        };

        graphics.FillPolygon(shadowBrush, shadowPoints);
        graphics.FillPolygon(boltBrush, boltPoints);
        graphics.DrawPolygon(borderPen, boltPoints);
    }

    private static void DrawCrossLines(Graphics graphics, Pen pen)
    {
        graphics.DrawLine(pen, 21.0f, 21.5f, 30.0f, 30.5f);
        graphics.DrawLine(pen, 30.0f, 21.5f, 21.0f, 30.5f);
    }

    private void OnFormShown(object? sender, EventArgs e)
    {
        BeginInvoke(new Action(() =>
        {
            PrepareWindowForTaskbar();
            if (_tabs.TabCount > 0 && _tabs.SelectedIndex < 0)
            {
                _tabs.SelectedIndex = 0;
            }

            UpdateTabHeaderVisuals();
            _tabHeader?.Invalidate();

            if (_settings.StartMinimized)
            {
                _startupCompleted = true;
                HideToTray(true);
                return;
            }

            if (!Visible)
            {
                Show();
            }

            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            NativeMethods.ShowWindow(Handle, 9);
            NativeMethods.ShowWindow(Handle, 5);
            Activate();
            UpdateTabHeaderVisuals();
            _tabHeader?.Invalidate();
            RefreshTrayMenu();
            _startupCompleted = true;
        }));
    }

    private void OnFormResize(object? sender, EventArgs e)
    {
        LayoutFooterButtons();
    }

    private void LayoutFooterButtons()
    {
        if (_tabBodyShell is null || _btnApply is null || _btnClose is null || _tabs is null || _clickerCard is null)
        {
            return;
        }

        const int gap = 50;
        var totalWidth = _btnApply.Width + gap + _btnClose.Width;
        var startX = (_tabBodyShell.Width - totalWidth) / 2;
        var clickerCardTopInTabs = _clickerCard.Parent?.Top ?? 0;
        var topGapGlobal = _tabBodyShell.Top + _tabs.Top + clickerCardTopInTabs + _clickerCard.Bottom;
        var bottomGapGlobal = _tabBodyShell.Top + _tabBodyShell.Height - (_tabBodyShell.DrawShadow ? 9 : 1);
        var gapHeight = Math.Max(_btnApply.Height, bottomGapGlobal - topGapGlobal);
        var topGlobal = topGapGlobal + Math.Max(0, (gapHeight - _btnApply.Height) / 2);
        var top = (topGlobal - _tabBodyShell.Top) - 6;

        _btnApply.Left = startX;
        _btnApply.Top = top;
        _btnClose.Left = _btnApply.Right + gap;
        _btnClose.Top = top;
        _btnApply.BringToFront();
        _btnClose.BringToFront();
    }

    private void OnFormClosingInternal(object? sender, FormClosingEventArgs e)
    {
        if (!_allowClose && _settings.CloseToTrayOnClose)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        SaveSettings();
        _trayIcon.Visible = false;
    }


}
