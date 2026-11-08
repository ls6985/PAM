using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>
/// "美化"页签的数据源：Windows Terminal 外观（字体 / 复古 / 光标 / 背景图 / 透明度）、
/// Starship 探测与一键安装（含 Scoop）、内置 Cascadia Code NF 字体安装、
/// 主题预设写入 starship.toml，以及配置备份与恢复。
/// 状态在进入页签时刷新；所有耗时操作走 IsBusy，期间禁用操作按钮。
///
/// 本文件只承载「外壳」：共享字段、INotifyPropertyChanged 基础设施、通用执行（RunBusyAsync）、
/// 反馈与本地化桥接。各功能（外观与字体 / 状态刷新 / 主题 / 自定义提示符 / Starship 安装 / 配置维护）
/// 分别落在同名 partial 文件里，互不干扰，修改某一功能不必通读其它功能。
/// </summary>
public sealed partial class BeautifyViewModel : INotifyPropertyChanged
{
    private const string BundledFontFileName = "CascadiaCodeNF.ttf";

    private readonly StarshipService _starshipService = new();
    private readonly StarshipPromptService _promptService = new();
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
    private string? _detectedPresetId;
    private ObservableCollection<string> _fontOptions = [];

    private bool _promptLoaded;
    private bool _promptCustomized;
    private bool _hasPromptBackup;
    private string _promptConfigPath = string.Empty;
    private string _promptStatusSource = "检测中…";
    private string _promptFormat = string.Empty;
    private bool _promptAddNewline;
    private bool _promptUseForwardSlash = true;
    private string _currentColor = PromptPalette.Colors[0];
    private ObservableCollection<PromptPreviewItem> _promptPreviewParts = [];

    // 点选组合：提示符由一串可选项片段拼成，不再允许直接编辑 format 文本。
    private readonly IReadOnlyList<ISegmentChoice> _segmentChoices;
    private readonly ObservableCollection<PromptSegment> _promptSegments = [];
    private bool _suppressRecompose;

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
        RefreshAppearanceFromDisk();
        RaiseStatusChanged();

        // 初始选中优先识别已应用的预设（重启后也能看到当前主题），识别不出选无需特殊字体的预设。
        _detectedPresetId = _starshipService.DetectAppliedPreset();
        SelectedPresetId = _detectedPresetId
            ?? _starshipService.GetPresets().FirstOrDefault(static p => !p.RequiresNerdFont)?.Id
            ?? string.Empty;

        // 片段内容可选项：自定义（可自由输入）放在第一条，其余是变量 + 固定符号；
        // 变量项复用 VariableOptions 同一批实例，语言切换时刷新显示名即可同步到每个片段的下拉。
        _segmentChoices = new List<ISegmentChoice> { new CustomOption() }
            .Concat(VariableOptions.Cast<ISegmentChoice>())
            .Concat(PromptSegment.LiteralChoices)
            .ToList();

        RefreshPromptState();
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

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
            OnPropertyChanged(nameof(CanApplyAppearance));
            OnPropertyChanged(nameof(CanDiscardAppearance));
            OnPropertyChanged(nameof(CanApplyPreset));
            OnPropertyChanged(nameof(CanOpenToml));
            OnPropertyChanged(nameof(CanRestoreToml));
            OnPropertyChanged(nameof(CanRestoreWtBackup));
            OnPropertyChanged(nameof(CanApplyPrompt));
            OnPropertyChanged(nameof(CanRestorePrompt));
            OnPropertyChanged(nameof(CanResetPrompt));
        }
    }

    public string FeedbackKind => _feedbackType.ToString();

    public string FeedbackMessage
    {
        get => _feedbackMessage;
        private set => SetField(ref _feedbackMessage, value);
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

        foreach (var option in VariableOptions)
        {
            option.Refresh();
        }

        foreach (var option in CursorShapeOptions)
        {
            option.Refresh();
        }

        OnPropertyChanged(nameof(AppearanceStatusText));
        OnPropertyChanged(nameof(WtBackupText));
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
