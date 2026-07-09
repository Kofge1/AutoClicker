using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace KofgeClicker;

public sealed partial class MainForm : Form
{
    private const string DefaultProfileId = "default";
    private const int StandardTabCardLeft = 52;
    private const int StandardTabCardTop = 32;
    private const int StandardTabCardWidth = 956;
    private const int StandardTabCardHeight = 356;
    private readonly string _settingsPath;
    private readonly string _localSettingsPath;
    private readonly string _executableSettingsPath;
    private readonly string _legacySettingsPath;
    private readonly IniFile _ini;
    private readonly GlobalInputHook _inputHook;
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _trayMenu;
    private readonly System.Windows.Forms.Timer _recordTimeoutTimer;
    private readonly AppSettings _settings = new();
    private readonly List<ProfileInfo> _profiles = [];
    private readonly List<TargetWindowInfo> _availableTargetWindows = [];
    private readonly List<AccentButton> _tabButtons = [];
    private readonly double _qpcFrequency;
    private readonly Icon _baseAppIcon;

    private ThemedTabControl _tabs = null!;
    private RoundedPanel _tabBodyShell = null!;
    private Panel _tabHeader = null!;
    private RoundedPanel _clickerCard = null!;
    private CheckBox _chkEnabled = null!;
    private InfoPill _txtTriggerHotkey = null!;
    private Button _btnBindTrigger = null!;
    private RadioButton _rbHold = null!;
    private RadioButton _rbToggle = null!;
    private ModernSlider _trkCps = null!;
    private TextBox _txtCps = null!;
    private Label _lblCpsValue = null!;
    private CheckBox _chkHumanized = null!;
    private RadioButton _rbPresetStable = null!;
    private RadioButton _rbPresetNatural = null!;
    private RadioButton _rbPresetAggressive = null!;
    private Panel _humanizedPresetGroup = null!;

    private PillDropdown _cmbPattern = null!;
    private PillValueEditor _txtBurstCount = null!;
    private PillValueEditor _txtBurstGap = null!;
    private PillValueEditor _txtHoldBurst = null!;
    private PillValueEditor _txtPressDelay = null!;
    private PillValueEditor _txtReleaseDelay = null!;
    private RadioButton _rbRateLocked = null!;
    private RadioButton _rbRateAmplified = null!;
    private Label _lblPatternHelp = null!;

    private PillDropdown _cmbClickButton = null!;

    private InfoPill _txtPanicHotkey = null!;
    private InfoPill _txtShowWindowHotkey = null!;
    private InfoPill _txtTogglePowerHotkey = null!;
    private InfoPill _txtProfileHotkey = null!;

    private PillDropdown _cmbProfiles = null!;
    private Label _lblStartupProfile = null!;
    private Button _btnSetStartup = null!;
    private Button _btnRememberProfileFlag = null!;
    private Button _btnRememberProfileValue = null!;

    private CheckBox _chkRestrictWindow = null!;
    private PillDropdown _cmbTargetWindow = null!;
    private Button _btnRefreshWindows = null!;
    private Label _lblTargetWindow = null!;
    private CheckBox _chkStartMinimized = null!;
    private CheckBox _chkRunAsAdministrator = null!;
    private CheckBox _chkRunOnStartup = null!;
    private CheckBox _chkRememberProfile = null!;
    private CheckBox _chkMinimizeToTray = null!;
    private CheckBox _chkCloseToTray = null!;

    private Button _btnApply = null!;
    private Button _btnClose = null!;
    private Label _lblStatus = null!;
    private Label _lblVersion = null!;
    private RoundedPanel _statusCard = null!;

    private bool _suppressUiEvents;
    private bool _targetWindowListBusy;
    private bool _allowClose;
    private bool _isActive;
    private bool _startupCompleted;
    private bool _pendingCpsCommit;
    private double _intervalMs = 1000.0 / 15.0;
    private double _humanizedWavePhase;
    private int _humanizedClickCounter;
    private double _humanizedRecoveryBudgetMs;
    private string _activeProfileId = DefaultProfileId;
    private string _defaultProfileId = DefaultProfileId;
    private string _lastValidTriggerKey = "F2";
    private string _lastValidPanicHotkey = "F12";
    private string _lastValidShowWindowHotkey = "F10";
    private string _lastValidTogglePowerHotkey = "F7";
    private string _lastValidProfileHotkey = "F9";
    private string _lastValidMode = "hold";
    private string? _recordingTargetName;
    private string _mouseButtonHeldByClicker = string.Empty;
    private long _recordStartTick;
    private long _lastTargetMismatchLogTick;
    private CancellationTokenSource? _clickCts;
    private Icon? _enabledStatusIcon;
    private Icon? _disabledStatusIcon;
    private bool _settingsSaveQueued;
    private bool _queuedStartupShortcutSync;
    private bool _windowTraySettingsSaveQueued;
    private bool _queuedWindowTrayStartupShortcutSync;
    private bool _autoEnabledSettingSaveQueued;
    private bool _humanizedSettingSaveQueued;
    private bool _modeSettingSaveQueued;
    private bool _clickButtonSaveQueued;
    private bool _clickPatternSaveQueued;
    private bool _patternNumbersSaveQueued;
    private bool _clickRateModeSaveQueued;
    private bool _trayMenuRefreshQueued;
    private int _clickSessionVersion;

    public MainForm()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        UpdateStyles();

        _localSettingsPath = AppPaths.SettingsPath;
        _executableSettingsPath = AppPaths.ExecutableSettingsPath;
        _legacySettingsPath = AppPaths.LegacySettingsPath;
        _settingsPath = ResolveSettingsPath(_localSettingsPath, _executableSettingsPath, _legacySettingsPath);
        _ini = new IniFile(_settingsPath);
        _inputHook = new GlobalInputHook();
        _inputHook.ShouldSuppressMouseInput = ShouldSuppressPhysicalMouseInput;
        _inputHook.InputChanged += OnGlobalInputChanged;
        NativeMethods.QueryPerformanceFrequency(out var frequency);
        _qpcFrequency = frequency;
        _baseAppIcon = LoadAppIcon();

        _trayMenu = new ContextMenuStrip();
        _trayIcon = new NotifyIcon
        {
            Text = "Kofge-Clicker",
            Visible = true,
            Icon = _baseAppIcon,
            ContextMenuStrip = _trayMenu
        };
        _trayIcon.DoubleClick += (_, _) => ShowFromTray();

        _recordTimeoutTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _recordTimeoutTimer.Tick += (_, _) => StopRecordingHotkey();

        BuildUi();
        LoadSettings();
        ApplySettingsToUi();
        UpdateInterval();
        UpdateStatus();
        RefreshTrayMenu();
        _inputHook.Start();
        StartUpdateCheck();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clickCts?.Cancel();
            if (_isActive || _mouseButtonHeldByClicker.Length > 0)
            {
                ReleaseClickerMouseState(ClickStopReason.Shutdown);
            }

            _recordTimeoutTimer.Dispose();
            _trayIcon.Dispose();
            _trayMenu.Dispose();
            _inputHook.Dispose();
            _enabledStatusIcon?.Dispose();
            _disabledStatusIcon?.Dispose();
            _baseAppIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
