using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>
/// "美化"页签的数据源：Starship 探测与一键安装（含 Scoop）、内置 Cascadia Code NF 字体安装、
/// 主题预设写入 starship.toml、Windows Terminal 默认字体，以及配置备份与恢复。
/// 状态在进入页签时刷新；所有耗时操作走 IsBusy，期间禁用操作按钮。
/// </summary>
public sealed class BeautifyViewModel : INotifyPropertyChanged
{
    private const string BundledFontFileName = "CascadiaCodeNF.ttf";

    private readonly StarshipService _starshipService = new();
    private readonly WindowsTerminalService _terminalService = new();
    private readonly ProfileService _profileService = new();
    private readonly Dispatcher _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

    /// <summary>刷新重入保护：页签 Loaded 与 SelectionChanged 可能同时触发刷新。</summary>
    private int _refreshing;

    private bool _isBusy;
    private string _starshipStatusSource = "检测中…";
    private bool _isStarshipInstalled;
    private string _scoopStatusSource = "检测中…";
    private bool _isScoopInstalled;
    private string? _currentWtFont;
    private bool _hasWtSettings;
    private string _wtSettingsPath = string.Empty;
    private bool _hasWtBackup;
    private bool _isBundledFontInstalled = true;
    private string _tomlPath = string.Empty;
    private string _tomlStatusSource = "未创建";
    private bool _hasTomlBackup;
    private string _initLineSource = "未知";
    private string? _selectedFont;
    private string _selectedPresetId = string.Empty;
    private ObservableCollection<string> _fontOptions = [];

    private string _feedbackSource = string.Empty;
    private object[] _feedbackArgs = [];
    private string _feedbackMessage = string.Empty;
    private StatusType _feedbackType = StatusType.Info;

    public BeautifyViewModel()
    {
        // 同步初始化字体下拉（注册表 + 读 WT 配置，都是毫秒级文件操作），
        // 打开页签的第一帧就能看到当前字体，而不是等异步探测完成。
        var candidates = FontInstaller.GetMonospaceCandidates();
        var wtState = _terminalService.GetState();
        _currentWtFont = wtState.CurrentFont;
        _hasWtSettings = wtState.SettingsExists;
        _wtSettingsPath = wtState.SettingsPath;
        _hasWtBackup = wtState.BackupExists;
        _isBundledFontInstalled = FontInstaller.IsBundledFontInstalled();
        _tomlPath = _starshipService.GetConfigPath();
        _tomlStatusSource = File.Exists(_tomlPath) ? "已创建" : "未创建";
        RebuildFontOptions(candidates, wtState.CurrentFont, _isBundledFontInstalled);
        RebuildPresetOptions();
        RaiseStatusChanged();

        // 默认选中无需特殊字体的预设，保证"应用"之后立即可用。
        SelectedPresetId = _starshipService.GetPresets().FirstOrDefault(static p => !p.RequiresNerdFont)?.Id
            ?? string.Empty;
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    // --- 选项 ---

    public IReadOnlyList<SettingOption> PresetOptions { get; private set; } = [];

    /// <summary>
    /// 字体候选。整体替换而不是 Clear+Add：ComboBox 对"原地改动"的集合不会重算
    /// 显示用 <c>SelectionBoxItem</c>（SelectedItem 正确、显示却空白），换新集合强制它重建。
    /// </summary>
    public ObservableCollection<string> FontOptions
    {
        get => _fontOptions;
        private set
        {
            if (ReferenceEquals(_fontOptions, value)) return;
            _fontOptions = value;
            OnPropertyChanged(nameof(FontOptions));
        }
    }

    public string? SelectedFont
    {
        get => _selectedFont;
        set
        {
            if (SetField(ref _selectedFont, value))
            {
                OnPropertyChanged(nameof(CanApplyFont));
                OnPropertyChanged(nameof(HasFontSelection));
                OnPropertyChanged(nameof(CanOneClickInstall));
            }
        }
    }

    public string SelectedPresetId
    {
        get => _selectedPresetId;
        set => SetField(ref _selectedPresetId, value ?? string.Empty);
    }

    // --- 状态展示（保存原文，读取时翻译，语言切换零成本） ---

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetField(ref _isBusy, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanOneClickInstall));
            OnPropertyChanged(nameof(CanRefresh));
            OnPropertyChanged(nameof(CanInstallBundledFont));
            OnPropertyChanged(nameof(CanApplyFont));
            OnPropertyChanged(nameof(CanApplyPreset));
            OnPropertyChanged(nameof(CanOpenToml));
            OnPropertyChanged(nameof(CanRestoreToml));
            OnPropertyChanged(nameof(CanRestoreWtBackup));
        }
    }

    public string StarshipStatusText => Loc.T(_starshipStatusSource);
    public string ScoopStatusText => Loc.T(_scoopStatusSource);
    public bool IsStarshipInstalled => _isStarshipInstalled;
    public bool IsScoopInstalled => _isScoopInstalled;
    public bool HasFontSelection => !string.IsNullOrWhiteSpace(_selectedFont);
    public bool HasWtSettings => _hasWtSettings;
    public bool IsBundledFontInstalled => _isBundledFontInstalled;
    public bool HasTomlBackup => _hasTomlBackup;
    public bool HasWtBackup => _hasWtBackup;
    public string TomlPath => _tomlPath;
    public string WtSettingsPath => _wtSettingsPath;
    public string TomlStatusText => Loc.T(_tomlStatusSource);
    public string InitLineText => Loc.F("starship init 行：{0}", Loc.T(_initLineSource));

    public string CurrentFontText => _currentWtFont is null
        ? Loc.T("Windows Terminal 当前字体：未设置（跟随 WT 默认）")
        : Loc.F("Windows Terminal 当前字体：{0}", _currentWtFont);

    // Starship 官方建议先装好 Nerd Font 再启用，因此未选字体时不允许安装。
    // 只保留一键安装：Scoop 缺失时由 Core 自动先装 Scoop，不需要用户自己挑安装方式。
    public bool CanOneClickInstall => !IsBusy && !_isStarshipInstalled && HasFontSelection;
    public bool CanRefresh => !IsBusy;
    public bool CanInstallBundledFont => !IsBusy && !_isBundledFontInstalled;
    public bool CanApplyFont => !IsBusy && _hasWtSettings && !string.IsNullOrWhiteSpace(_selectedFont);
    public bool CanApplyPreset => !IsBusy && PresetOptions.Count > 0;
    public bool CanOpenToml => !IsBusy;
    public bool CanRestoreToml => !IsBusy && _hasTomlBackup;
    public bool CanRestoreWtBackup => !IsBusy && _hasWtBackup;

    public string FeedbackKind => _feedbackType.ToString();

    public string FeedbackMessage
    {
        get => _feedbackMessage;
        private set => SetField(ref _feedbackMessage, value);
    }

    // --- 刷新 ---

    /// <summary>重新探测 Starship / Scoop / 字体 / 配置状态。重入时直接跳过。</summary>
    public async Task RefreshStateAsync()
    {
        if (Interlocked.Increment(ref _refreshing) > 1)
        {
            Interlocked.Decrement(ref _refreshing);
            return;
        }

        try
        {
            var terminalType = AppServices.Settings.TerminalType;
            var (profilePath, _) = await Task.Run(() => _profileService.ResolveProfilePath(terminalType));
            var detect = await Task.Run(() => _starshipService.Detect());
            var scoopInstalled = await Task.Run(() => _starshipService.IsScoopInstalled());
            var wtState = await Task.Run(() => _terminalService.GetState());
            var candidates = await Task.Run(() => FontInstaller.GetMonospaceCandidates());
            var bundledInstalled = await Task.Run(() => FontInstaller.IsBundledFontInstalled());
            var state = await Task.Run(() => _starshipService.GetState(profilePath));

            _starshipStatusSource = detect.Installed ? $"已安装 {detect.Version}" : "未安装";
            _isStarshipInstalled = detect.Installed;
            _scoopStatusSource = scoopInstalled ? "Scoop 已就绪" : "未检测到 Scoop";
            _isScoopInstalled = scoopInstalled;
            _currentWtFont = wtState.CurrentFont;
            _hasWtSettings = wtState.SettingsExists;
            _wtSettingsPath = wtState.SettingsPath;
            _hasWtBackup = wtState.BackupExists;
            _isBundledFontInstalled = bundledInstalled;
            _tomlPath = state.ConfigPath;
            _tomlStatusSource = state.ConfigExists ? "已创建" : "未创建";
            _hasTomlBackup = state.BackupExists;
            _initLineSource = state.ProfileHasInitLine ? "已写入" : "未写入";

            RebuildFontOptions(candidates, wtState.CurrentFont, bundledInstalled);
            RebuildPresetOptions();

            RaiseStatusChanged();
        }
        catch (Exception ex)
        {
            SetFeedback(StatusType.Error, "状态刷新失败：{0}", ex.Message);
        }
        finally
        {
            Interlocked.Decrement(ref _refreshing);
        }
    }

    private void RebuildFontOptions(IReadOnlyList<string> candidates, string? currentFont, bool bundledInstalled)
    {
        var options = new ObservableCollection<string>();

        foreach (var name in candidates)
        {
            options.Add(name);
        }

        if (bundledInstalled && !options.Contains(FontInstaller.BundledFontFamily))
        {
            options.Insert(0, FontInstaller.BundledFontFamily);
        }

        if (!string.IsNullOrWhiteSpace(currentFont) && !options.Contains(currentFont))
        {
            options.Insert(0, currentFont);
        }

        // 整体换新集合（见 FontOptions 注释）：ComboBox 重建条目后，
        // 内部选择先被清空，下面的 SelectedFont 通知才是"真变化"，显示层才会跟着更新。
        FontOptions = options;

        // 用户已选且仍有效的选择保持不变；否则默认显示终端当前字体，
        // 没有配置过字体时选内置字体（已装）或第一个候选，绝不让下拉空着。
        var preserve = _selectedFont;
        var selection = !string.IsNullOrWhiteSpace(preserve) && options.Contains(preserve)
            ? preserve
            : currentFont ?? (_isBundledFontInstalled ? FontInstaller.BundledFontFamily : options.FirstOrDefault());
        SelectedFont = selection;

        // ItemsSource 换新会把 ComboBox 的内部选中清空；若值恰好未变，上面的赋值不触发通知，
        // 这里强制补一次，让 ComboBox 从空选中重新匹配 SelectedItem 并重算 SelectionBoxItem。
        OnPropertyChanged(nameof(SelectedFont));
    }

    private void RebuildPresetOptions()
    {
        var presets = _starshipService.GetPresets();
        PresetOptions = presets
            .Select(static preset => new SettingOption(preset.Id, preset.DisplayNameSource))
            .ToList();
        OnPropertyChanged(nameof(PresetOptions));

        if (presets.All(preset => preset.Id != _selectedPresetId))
        {
            SelectedPresetId = presets.FirstOrDefault(static p => !p.RequiresNerdFont)?.Id ?? string.Empty;
        }
    }

    private void RaiseStatusChanged()
    {
        OnPropertyChanged(nameof(StarshipStatusText));
        OnPropertyChanged(nameof(ScoopStatusText));
        OnPropertyChanged(nameof(IsStarshipInstalled));
        OnPropertyChanged(nameof(IsScoopInstalled));
        OnPropertyChanged(nameof(CurrentFontText));
        OnPropertyChanged(nameof(HasFontSelection));
        OnPropertyChanged(nameof(HasWtSettings));
        OnPropertyChanged(nameof(WtSettingsPath));
        OnPropertyChanged(nameof(IsBundledFontInstalled));
        OnPropertyChanged(nameof(TomlPath));
        OnPropertyChanged(nameof(TomlStatusText));
        OnPropertyChanged(nameof(InitLineText));
        OnPropertyChanged(nameof(HasTomlBackup));
        OnPropertyChanged(nameof(HasWtBackup));
        OnPropertyChanged(nameof(CanOneClickInstall));
        OnPropertyChanged(nameof(CanInstallBundledFont));
        OnPropertyChanged(nameof(CanApplyFont));
        OnPropertyChanged(nameof(CanApplyPreset));
        OnPropertyChanged(nameof(CanOpenToml));
        OnPropertyChanged(nameof(CanRestoreToml));
        OnPropertyChanged(nameof(CanRestoreWtBackup));
    }

    // --- Starship 安装 ---

    /// <summary>一键安装：Scoop 不存在时先装 Scoop（官方引导脚本），再装 Starship。</summary>
    public async Task InstallStarshipWithScoopAsync()
    {
        await RunBusyAsync(
            "正在安装 Scoop 与 Starship，可能需要几分钟…",
            () => _starshipService.InstallScoopAndStarship(ReportProgress),
            static result => new[]
            {
                (result.Success ? StatusType.Saved : StatusType.Error, result.Message),
            });
    }

    private void ReportProgress(string line)
    {
        // 安装输出从后台线程到达，统一投递到 UI 线程展示。
        void Update() => FeedbackMessage = Loc.T(line.Trim());

        if (_dispatcher.CheckAccess())
        {
            Update();
        }
        else
        {
            _ = _dispatcher.BeginInvoke(Update);
        }
    }

    // --- 字体 ---

    public async Task InstallBundledFontAsync()
    {
        await RunBusyAsync(
            "正在安装内置字体…",
            () =>
            {
                var ttfPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", BundledFontFileName);
                return FontInstaller.InstallFont(ttfPath);
            },
            static result => new[]
            {
                (result.Success ? StatusType.Saved : StatusType.Error, result.Message),
            });
    }

    public async Task ApplyFontAsync()
    {
        var face = SelectedFont;
        if (string.IsNullOrWhiteSpace(face))
        {
            SetFeedback(StatusType.Warning, "请先选择一个字体。");
            return;
        }

        await RunBusyAsync(
            "正在写入 Windows Terminal 字体设置…",
            () => _terminalService.ApplyFont(face),
            static result => new[]
            {
                (result.Success ? StatusType.Saved : StatusType.Error, result.Message),
            });
    }

    // --- 主题预设 ---

    public async Task ApplyPresetAsync()
    {
        var preset = _starshipService.GetPresets().FirstOrDefault(p => p.Id == _selectedPresetId);
        if (preset is null)
        {
            SetFeedback(StatusType.Warning, "请先选择一个主题预设。");
            return;
        }

        await RunBusyAsync(
            "正在应用主题预设…",
            () =>
            {
                var (profilePath, _) = _profileService.ResolveProfilePath(AppServices.Settings.TerminalType);
                return _starshipService.ApplyPreset(preset.Id, profilePath);
            },
            _ => preset.RequiresNerdFont && !_isBundledFontInstalled
                ?
                [
                    (StatusType.Warning, "主题已应用：{0}。该预设需要 Nerd Font，当前未安装内置字体，提示符可能显示乱码方块，建议先安装。"),
                ]
                :
                [
                    (StatusType.Saved, "主题已应用：{0}；原 starship.toml 已自动备份。"),
                ],
            new object[] { Loc.T(preset.DisplayNameSource) });
    }

    // --- 配置维护 ---

    public async Task OpenTomlAsync()
    {
        await RunBusyAsync(
            "正在打开 starship.toml…",
            () => _starshipService.OpenConfig(),
            static result => new[]
            {
                (StatusType.Saved, result.FileCreated ? "已创建并打开配置文件：{0}" : "已打开配置文件：{0}"),
            },
            new object[] { _starshipService.GetConfigPath() });
    }

    public async Task RestoreTomlAsync()
    {
        await RunBusyAsync(
            "正在恢复 starship.toml 备份…",
            () => _starshipService.RestoreConfig(),
            static restored => new[]
            {
                restored
                    ? (StatusType.Saved, "已从备份恢复 starship.toml。")
                    : ((StatusType.Warning, "没有可恢复的备份。")),
            });
    }

    public async Task RestoreWtBackupAsync()
    {
        await RunBusyAsync(
            "正在恢复 Windows Terminal 备份…",
            () => _terminalService.RestoreBackup(),
            static restored => new[]
            {
                restored
                    ? (StatusType.Saved, "已从备份恢复 Windows Terminal 的 settings.json。")
                    : ((StatusType.Warning, "没有可恢复的备份。")),
            });
    }
    // --- 通用执行 ---

    /// <summary>IsBusy 包裹的耗时操作：进入时给出进行中提示，结束后按结果反馈并刷新状态。</summary>
    private async Task RunBusyAsync<TResult>(
        string busySource,
        Func<TResult> work,
        Func<TResult, (StatusType Type, string Source)[]> feedbackFor,
        object[]? extraArgs = null)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        SetFeedback(StatusType.Info, busySource);

        try
        {
            var result = await Task.Run(work);
            var reported = false;

            foreach (var (type, source) in feedbackFor(result))
            {
                if (string.IsNullOrEmpty(source))
                {
                    continue;
                }

                // 多条结果时后一条覆盖前一条：警告（如缺字体）优先于常规保存提示。
                SetFeedback(type, source, extraArgs ?? []);
                reported = true;
            }

            if (!reported)
            {
                SetFeedback(StatusType.Saved, "操作完成。");
            }
        }
        catch (Exception ex)
        {
            SetFeedback(StatusType.Error, "操作失败：{0}", ex.Message);
        }
        finally
        {
            IsBusy = false;
            await RefreshStateAsync();
        }
    }

    // --- 反馈与本地化 ---

    private void SetFeedback(StatusType type, string source, params object[] args)
    {
        _feedbackSource = source;
        _feedbackArgs = args;
        _feedbackType = type;
        FeedbackMessage = Loc.F(source, args);
        OnPropertyChanged(nameof(FeedbackKind));
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var option in PresetOptions)
        {
            option.Refresh();
        }

        RaiseStatusChanged();
        FeedbackMessage = Loc.F(_feedbackSource, _feedbackArgs);
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
