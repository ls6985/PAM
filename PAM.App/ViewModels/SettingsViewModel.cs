using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>下拉选项：Id 用于持久化，DisplayName 随语言切换刷新。</summary>
public sealed class SettingOption(string id, string sourceName) : INotifyPropertyChanged
{
    private string _displayName = Loc.T(sourceName);

    public string Id { get; } = id;

    public string DisplayName
    {
        get => _displayName;
        private set
        {
            if (_displayName == value) return;
            _displayName = value;
            OnPropertyChanged();
        }
    }

    public void Refresh() => DisplayName = Loc.T(sourceName);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// “设置”页签的数据源。所有改动立即写入 settings.json 并通知后台托盘；
/// 热键是例外：必须先通过冲突检测，再点“应用热键”才会生效。
/// </summary>
public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private const string CaptureHint = "点击“更改”后按下组合键；Esc 取消录制。";
    private const string FallbackHint = "未检测到 PowerShell 7，已回退到 Windows PowerShell 5.1。";

    private readonly ProfileService _profileService = new();

    private bool _isSyncing = true;
    private HotkeyChord? _pendingChord;
    private HotkeyCheckResult _check = HotkeyConflictDetector.Idle();

    private string _theme = AppSettings.ThemeSystem;
    private string _language = AppSettings.LanguageZhCN;
    private string _terminalType = AppSettings.TerminalTypePwsh;
    private bool _keepTrayOnClose = true;
    private bool _switchImeToEnglish = true;
    private bool _aliasLaunchEnhanced = true;
    private bool _showAliasRemarkOnLaunch = true;
    private string _hotkeyDisplay = AppSettings.DefaultHotkey;
    private string _pendingHotkeyDisplay = string.Empty;
    private bool _hasPendingHotkey;
    private bool _isCapturing;
    private string _capturePrompt = Loc.T(CaptureHint);
    private string _profilePathDisplay = string.Empty;
    private string _terminalHint = string.Empty;
    private bool _hasTerminalFallback;
    private bool _isProfileResolving;
    private bool _isTrayRunning;
    private string _trayStatusText = string.Empty;
    private string _feedbackSource = string.Empty;
    private object[] _feedbackArgs = [];
    private string _feedbackMessage = string.Empty;
    private StatusType _feedbackType = StatusType.Info;

    public SettingsViewModel()
    {
        ThemeOptions =
        [
            new(AppSettings.ThemeSystem, "跟随系统"),
            new(AppSettings.ThemeLight, "浅色"),
            new(AppSettings.ThemeDark, "深色"),
        ];
        LanguageOptions =
        [
            new(AppSettings.LanguageZhCN, "简体中文"),
            new(AppSettings.LanguageEnUS, "English"),
        ];
        TerminalOptions =
        [
            new(AppSettings.TerminalTypePwsh, "PowerShell 7 (pwsh.exe)"),
            new(AppSettings.TerminalTypePowerShell, "Windows PowerShell 5.1 (powershell.exe)"),
            new(AppSettings.TerminalTypeAuto, "自动探测（优先 PowerShell 7）"),
        ];

        SyncFromSettings(AppServices.Settings);
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    public IReadOnlyList<SettingOption> ThemeOptions { get; }
    public IReadOnlyList<SettingOption> LanguageOptions { get; }
    public IReadOnlyList<SettingOption> TerminalOptions { get; }
    public string SettingsFilePath => AppServices.SettingsFilePath;

    public string Theme
    {
        get => _theme;
        set { if (SetField(ref _theme, value) && !_isSyncing) Commit(s => s.Theme = value); }
    }

    public string Language
    {
        get => _language;
        set { if (SetField(ref _language, value) && !_isSyncing) Commit(s => s.Language = value); }
    }

    public string TerminalType
    {
        get => _terminalType;
        set
        {
            if (!SetField(ref _terminalType, value) || _isSyncing) return;
            Commit(s => s.TerminalType = value);
            _ = RefreshProfilePathAsync();
        }
    }

    public bool KeepTrayOnClose
    {
        get => _keepTrayOnClose;
        set { if (SetField(ref _keepTrayOnClose, value) && !_isSyncing) Commit(s => s.KeepTrayOnClose = value); }
    }

    public bool SwitchImeToEnglish
    {
        get => _switchImeToEnglish;
        set { if (SetField(ref _switchImeToEnglish, value) && !_isSyncing) Commit(s => s.SwitchImeToEnglish = value); }
    }

    public bool AliasLaunchEnhanced
    {
        get => _aliasLaunchEnhanced;
        set { if (SetField(ref _aliasLaunchEnhanced, value) && !_isSyncing) Commit(s => s.AliasLaunchEnhanced = value); }
    }

    public bool ShowAliasRemarkOnLaunch
    {
        get => _showAliasRemarkOnLaunch;
        set { if (SetField(ref _showAliasRemarkOnLaunch, value) && !_isSyncing) Commit(s => s.ShowAliasRemarkOnLaunch = value); }
    }

    /// <summary>已经生效并写入设置文件的热键。</summary>
    public string HotkeyDisplay { get => _hotkeyDisplay; private set => SetField(ref _hotkeyDisplay, value); }
    public string PendingHotkeyDisplay { get => _pendingHotkeyDisplay; private set => SetField(ref _pendingHotkeyDisplay, value); }
    public bool HasPendingHotkey { get => _hasPendingHotkey; private set => SetField(ref _hasPendingHotkey, value); }
    public bool IsCapturing { get => _isCapturing; private set => SetField(ref _isCapturing, value); }
    public string CapturePrompt { get => _capturePrompt; private set => SetField(ref _capturePrompt, value); }
    public string ProfilePathDisplay { get => _profilePathDisplay; private set => SetField(ref _profilePathDisplay, value); }
    public string TerminalHint { get => _terminalHint; private set => SetField(ref _terminalHint, value); }
    public bool IsTrayRunning { get => _isTrayRunning; private set => SetField(ref _isTrayRunning, value); }
    public string TrayStatusText { get => _trayStatusText; private set => SetField(ref _trayStatusText, value); }
    public string FeedbackMessage { get => _feedbackMessage; private set => SetField(ref _feedbackMessage, value); }

    public bool IsProfileResolving
    {
        get => _isProfileResolving;
        private set
        {
            if (!SetField(ref _isProfileResolving, value)) return;
            OnPropertyChanged(nameof(CanOpenProfile));
        }
    }

    public StatusType FeedbackType
    {
        get => _feedbackType;
        private set
        {
            if (!SetField(ref _feedbackType, value)) return;
            OnPropertyChanged(nameof(FeedbackKind));
        }
    }

    public bool CanOpenProfile => !_isProfileResolving;
    public string FeedbackKind => _feedbackType.ToString();
    public string HotkeyStatusKind => _check.Level.ToString();
    public string HotkeyStatusMessage => _check.Message;
    public bool CanApplyHotkey => _hasPendingHotkey && _check.CanApply;
    public bool CanCheckHotkey => !_isCapturing;
    public bool NeedsConfirmation => _check.NeedsConfirmation;

    // --- 初始化 / 同步 ---

    private void SyncFromSettings(AppSettings settings)
    {
        _isSyncing = true;

        _theme = settings.Theme;
        _language = settings.Language;
        _terminalType = settings.TerminalType;
        _keepTrayOnClose = settings.KeepTrayOnClose;
        _switchImeToEnglish = settings.SwitchImeToEnglish;
        _aliasLaunchEnhanced = settings.AliasLaunchEnhanced;
        _showAliasRemarkOnLaunch = settings.ShowAliasRemarkOnLaunch;
        _hotkeyDisplay = settings.Hotkey;
        _pendingChord = null;
        _check = HotkeyConflictDetector.Idle();

        PendingHotkeyDisplay = string.Empty;
        HasPendingHotkey = false;
        IsCapturing = false;
        CapturePrompt = Loc.T(CaptureHint);
        OnPropertyChanged(nameof(Theme));
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(TerminalType));
        OnPropertyChanged(nameof(KeepTrayOnClose));
        OnPropertyChanged(nameof(SwitchImeToEnglish));
        OnPropertyChanged(nameof(AliasLaunchEnhanced));
        OnPropertyChanged(nameof(ShowAliasRemarkOnLaunch));
        OnPropertyChanged(nameof(HotkeyDisplay));
        RaiseCheckChanged();

        _isSyncing = false;
        RefreshTrayState();
        _ = RefreshProfilePathAsync();
    }

    private void Commit(Action<AppSettings> change)
    {
        try
        {
            var updated = AppServices.Settings.Clone();
            change(updated);
            AppServices.Apply(updated);
            SetFeedback(StatusType.Saved, "设置已保存并同步到后台托盘。");
        }
        catch (Exception ex)
        {
            SetFeedback(StatusType.Error, "保存设置失败：{0}", ex.Message);
        }
    }

    // --- 配置文件 ---

    public async Task RefreshProfilePathAsync()
    {
        IsProfileResolving = true;
        var terminal = _terminalType;

        try
        {
            var (path, fallback) = await Task.Run(() => _profileService.ResolveProfilePath(terminal));
            ProfilePathDisplay = path;
            _hasTerminalFallback = fallback;
            TerminalHint = fallback ? Loc.T(FallbackHint) : string.Empty;
        }
        catch (Exception ex)
        {
            ProfilePathDisplay = Loc.T("尚未解析到 $PROFILE 路径");
            _hasTerminalFallback = false;
            TerminalHint = ex.Message;
        }
        finally
        {
            IsProfileResolving = false;
        }
    }

    /// <summary>按当前选中的默认终端打开对应的 $PROFILE（不存在则先创建）。</summary>
    public async Task OpenProfileAsync()
    {
        IsProfileResolving = true;
        var terminal = _terminalType;

        try
        {
            var result = await Task.Run(() => _profileService.OpenProfileFile(terminal));
            ProfilePathDisplay = result.Path;
            SetFeedback(
                StatusType.Saved,
                result.FileCreated ? "已创建并打开配置文件：{0}" : "已打开配置文件：{0}",
                result.Path);
        }
        catch (Exception ex)
        {
            SetFeedback(StatusType.Error, "打开配置文件失败：{0}", ex.Message);
        }
        finally
        {
            IsProfileResolving = false;
        }
    }

    // --- 热键录制 / 冲突检测 ---

    public void BeginCapture()
    {
        _pendingChord = null;
        PendingHotkeyDisplay = string.Empty;
        HasPendingHotkey = false;
        IsCapturing = true;
        CapturePrompt = Loc.T("请按下组合键…");
        _check = HotkeyConflictDetector.Idle();
        RaiseCheckChanged();
    }

    /// <summary>录制过程中显示已按下的修饰键，提升可感知性。</summary>
    public void SetCapturePreview(string text)
    {
        if (_isCapturing) CapturePrompt = text;
    }

    public void CancelCapture()
    {
        IsCapturing = false;
        _pendingChord = null;
        PendingHotkeyDisplay = string.Empty;
        HasPendingHotkey = false;
        CapturePrompt = Loc.T(CaptureHint);
        _check = HotkeyConflictDetector.Idle();
        RaiseCheckChanged();
    }

    public void SetCapturedChord(HotkeyChord chord)
    {
        _pendingChord = chord;
        PendingHotkeyDisplay = chord.Display;
        HasPendingHotkey = true;
        IsCapturing = false;
        CapturePrompt = Loc.T("至少包含一个修饰键（Ctrl / Alt / Shift / Win）。");
        CheckConflict();
    }

    public void RestoreDefaultHotkey()
    {
        var chord = HotkeyChord.Parse(AppSettings.DefaultHotkey);
        if (chord is not null) SetCapturedChord(chord);
    }

    /// <summary>有候选热键时检测候选，否则检测当前已生效的热键。</summary>
    public void CheckConflict()
    {
        var chord = _pendingChord ?? HotkeyChord.Parse(_hotkeyDisplay);
        _check = chord is null
            ? new HotkeyCheckResult(HotkeyCheckLevel.Error, Loc.T("热键不能为空。"), false, false)
            : HotkeyConflictDetector.Check(chord, AppServices.Settings.Hotkey, _isTrayRunning);
        RaiseCheckChanged();
    }

    /// <summary>应用候选热键；软冲突需由视图先弹出确认。</summary>
    public void ApplyHotkey(bool confirmed)
    {
        if (_pendingChord is null) return;

        if (!_check.CanApply)
        {
            SetFeedback(StatusType.Error, "热键冲突，未应用。");
            return;
        }

        if (_check.NeedsConfirmation && !confirmed) return;

        var display = _pendingChord.Display;
        Commit(s => s.Hotkey = display);
        HotkeyDisplay = display;
        _pendingChord = null;
        PendingHotkeyDisplay = string.Empty;
        HasPendingHotkey = false;
        CapturePrompt = Loc.T(CaptureHint);

        SetFeedback(StatusType.Saved, EnsureTrayForHotkey() ?? "热键已应用并同步到后台托盘。");
        _check = HotkeyConflictDetector.Idle();
        RaiseCheckChanged();
    }

    // --- 后台托盘 ---

    public void RefreshTrayState()
    {
        IsTrayRunning = TrayBridge.IsTrayRunning();
        TrayStatusText = Loc.T(_isTrayRunning ? "运行中" : "未运行");
    }

    public void StartTray()
    {
        var started = TrayBridge.EnsureTrayRunning();
        SetFeedback(
            started ? StatusType.Saved : StatusType.Error,
            started ? "后台托盘已启动。" : "无法启动后台托盘，请检查 PAM.Tray.exe 是否与管理程序在同一目录。");
        RefreshTrayState();
    }

    public async Task StopTrayAsync()
    {
        TrayBridge.SignalExit();
        SetFeedback(StatusType.Info, "已通知后台托盘退出。");
        await Task.Delay(900);
        RefreshTrayState();
    }

    private string? EnsureTrayForHotkey()
    {
        if (TrayBridge.IsTrayRunning())
        {
            RefreshTrayState();
            return null;
        }

        var started = TrayBridge.EnsureTrayRunning();
        RefreshTrayState();
        return started
            ? "热键修改后需要后台托盘重新注册；当前托盘未运行，已为你启动。"
            : "设置已保存，但未找到后台托盘程序，热键将在下次启动托盘后生效。";
    }

    // --- 恢复默认 ---

    public void RestoreDefaults()
    {
        var result = MessageBox.Show(
            Loc.T("确定要恢复全部默认设置吗？"),
            Loc.T("恢复默认设置"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            AppServices.Apply(new AppSettings());
            SyncFromSettings(AppServices.Settings);
            SetFeedback(StatusType.Saved, "已恢复默认设置。");
        }
        catch (Exception ex)
        {
            SetFeedback(StatusType.Error, "保存设置失败：{0}", ex.Message);
        }
    }

    // --- 反馈与本地化 ---

    private void SetFeedback(StatusType type, string source, params object[] args)
    {
        _feedbackSource = source;
        _feedbackArgs = args;
        FeedbackType = type;
        FeedbackMessage = Loc.F(source, args);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var option in ThemeOptions) option.Refresh();
        foreach (var option in LanguageOptions) option.Refresh();
        foreach (var option in TerminalOptions) option.Refresh();

        TerminalHint = _hasTerminalFallback ? Loc.T(FallbackHint) : string.Empty;
        CapturePrompt = Loc.T(_isCapturing ? "请按下组合键…" : CaptureHint);
        RefreshTrayState();
        FeedbackMessage = Loc.F(_feedbackSource, _feedbackArgs);

        // 冲突提示语里包含系统保留原因，重新检测一次即可刷新为当前语言。
        if (_pendingChord is not null || _check.Level != HotkeyCheckLevel.Idle) CheckConflict();
        else { _check = HotkeyConflictDetector.Idle(); RaiseCheckChanged(); }
    }

    private void RaiseCheckChanged()
    {
        OnPropertyChanged(nameof(HotkeyStatusKind));
        OnPropertyChanged(nameof(HotkeyStatusMessage));
        OnPropertyChanged(nameof(CanApplyHotkey));
        OnPropertyChanged(nameof(CanCheckHotkey));
        OnPropertyChanged(nameof(NeedsConfirmation));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value) || value is null) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
