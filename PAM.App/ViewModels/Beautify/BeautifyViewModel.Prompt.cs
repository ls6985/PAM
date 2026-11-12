using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>
/// 自定义提示符功能：点选片段组合出一行 Starship format、带色实时预览、写入 / 恢复 starship.toml。
/// 修改提示符编辑器逻辑只看本文件。
/// </summary>
public sealed partial class BeautifyViewModel
{
    /// <summary>精选 20 个 Starship 变量（中文名 + 分组），供下拉「选一选」插入。</summary>
    public IReadOnlyList<VariableOption> VariableOptions { get; } =
        StarshipVariables.All.Select(static item => new VariableOption(item)).ToList();

    /// <summary>片段「内容」可选项：变量 + 固定符号，供每个片段的下拉选内容。</summary>
    public IReadOnlyList<ISegmentChoice> SegmentChoices => _segmentChoices;

    /// <summary>提示符片段集合：整体由这些片段组合而成，变量不再手填、不可直接编辑。</summary>
    public ObservableCollection<PromptSegment> PromptSegments => _promptSegments;

    /// <summary>当前颜色：新片段默认取它；每个片段也能单独改自己的颜色。</summary>
    public string CurrentColor
    {
        get => _currentColor;
        set => SetField(ref _currentColor, value ?? PromptPalette.Colors[0]);
    }

    /// <summary>一行 Starship format 语法，用户可直接编辑整行。</summary>
    public string PromptFormat
    {
        get => _promptFormat;
        set
        {
            if (SetField(ref _promptFormat, value ?? string.Empty))
            {
                RebuildPreview();
                OnPropertyChanged(nameof(CanApplyPrompt));
            }
        }
    }

    /// <summary>带色实时预览；变量显示示例值，实际文字由 Starship 运行时读取。</summary>
    public ObservableCollection<PromptPreviewItem> PromptPreviewParts
    {
        get => _promptPreviewParts;
        private set
        {
            _promptPreviewParts = value;
            OnPropertyChanged();
        }
    }

    /// <summary>提示符前空一行，两种提示符方案通用。默认关闭：回车后紧跟提示符，不自动空行。</summary>
    public bool PromptAddNewline
    {
        get => _promptAddNewline;
        set => SetField(ref _promptAddNewline, value);
    }

    /// <summary>路径分隔符显示为 /，两种提示符方案通用。默认开启。</summary>
    public bool PromptUseForwardSlash
    {
        get => _promptUseForwardSlash;
        set => SetField(ref _promptUseForwardSlash, value);
    }

    public string PromptStatusText => Loc.T(_promptStatusKey);
    public string PromptConfigPath => _promptConfigPath;
    public bool PromptCustomized => _promptCustomized;
    public bool HasPromptBackup => _hasPromptBackup;

    public bool CanApplyPrompt => !IsBusy && !string.IsNullOrWhiteSpace(_promptFormat);
    public bool CanRestorePrompt => !IsBusy && _hasPromptBackup;
    public bool CanResetPrompt => !IsBusy;

    // --- 状态与预览 ---

    /// <summary>
    /// 刷新提示符状态。默认只更新状态字段、不覆盖输入框：用户可能正在编辑但还没点应用，
    /// 此时做一次"重新检测"不应把输入冲掉；forceReload 留给"恢复应用前配置"。
    /// </summary>
    private void RefreshPromptState(bool forceReload = false)
    {
        var state = _promptService.Read();
        _promptConfigPath = state.ConfigPath;
        _promptCustomized = state.Customized;
        _hasPromptBackup = state.HasBackup;
        _promptStatusKey = !state.ConfigExists
            ? LocKey.StarshipTomlDoesNotExistYetApplyingWillCreateIt
            : state.Customized
                ? LocKey.ThePromptIsCurrentlyManagedByPowerShellManager
                : LocKey.UsingTheOriginalPromptCustomPromptNotAppliedYet;

        if (forceReload || !_promptLoaded)
        {
            LoadFormat(state.Format);
            _promptLoaded = true;
        }

        RaisePromptChanged();
    }

    private void RaisePromptChanged()
    {
        OnPropertyChanged(nameof(PromptStatusText));
        OnPropertyChanged(nameof(PromptConfigPath));
        OnPropertyChanged(nameof(PromptCustomized));
        OnPropertyChanged(nameof(HasPromptBackup));
        OnPropertyChanged(nameof(CanApplyPrompt));
        OnPropertyChanged(nameof(CanRestorePrompt));
        OnPropertyChanged(nameof(PromptFormat));
        OnPropertyChanged(nameof(PromptPreviewParts));
        OnPropertyChanged(nameof(PromptAddNewline));
        OnPropertyChanged(nameof(PromptUseForwardSlash));
        OnPropertyChanged(nameof(CurrentColor));
    }

    private void RebuildPreview()
        => PromptPreviewParts = new ObservableCollection<PromptPreviewItem>(
            StarshipPromptService.ParsePreview(_promptFormat)
                .Select(static part => new PromptPreviewItem(part.Text, part.Color)));

    /// <summary>外部载入（初始读取 / 重置 / 恢复）一行 format：先更新字段与预览，再还原成可选项片段。</summary>
    private void LoadFormat(string format)
    {
        _promptFormat = format ?? string.Empty;
        OnPropertyChanged(nameof(PromptFormat));
        RebuildPreview();
        OnPropertyChanged(nameof(CanApplyPrompt));
        SyncSegmentsFromFormat();
    }

    /// <summary>把当前片段组合重组成一行 Starship format，写回 <see cref="PromptFormat" />（预览随之刷新）。</summary>
    private void Recompose()
    {
        if (_suppressRecompose) return;
        var builder = new StringBuilder(_promptSegments.Count * 12);
        foreach (var segment in _promptSegments)
        {
            builder.Append(segment.ToSnippet());
        }

        // 直接写字段避免再次触发同步；RebuildPreview + CanApplyPrompt 仍需刷新。
        _promptFormat = builder.ToString();
        OnPropertyChanged(nameof(PromptFormat));
        RebuildPreview();
        OnPropertyChanged(nameof(CanApplyPrompt));
    }

    /// <summary>把一行 format 反向拆成片段集合；用 <see cref="_suppressRecompose" /> 防止填充时回写。</summary>
    private void SyncSegmentsFromFormat()
    {
        _suppressRecompose = true;
        try
        {
            _promptSegments.Clear();
            foreach (var data in StarshipPromptService.ParseFormatSegments(_promptFormat))
            {
                var segment = new PromptSegment(_segmentChoices, FindChoice(data.Kind, data.Value), data.Color);
                segment.PropertyChanged += OnSegmentChanged;
                _promptSegments.Add(segment);
            }

            if (_promptSegments.Count == 0)
            {
                AddBlankSegment();
            }
        }
        finally
        {
            _suppressRecompose = false;
        }
    }

    /// <summary>新增一个空白片段（默认选第一个变量，颜色取当前默认色）。</summary>
    public void AddSegment()
    {
        AddBlankSegment();
        Recompose();
    }

    private void AddBlankSegment()
    {
        // 新片段默认选第一个变量（用户名），而不是排在最前面的「自定义」空项。
        var initial = _segmentChoices.FirstOrDefault(static c => c.Kind == PromptSegmentKind.Variable)
            ?? _segmentChoices[0];
        var segment = new PromptSegment(_segmentChoices, initial, CurrentColor);
        segment.PropertyChanged += OnSegmentChanged;
        _promptSegments.Add(segment);
    }

    /// <summary>删除一个片段（先解绑事件，再移除，最后重组成 format）。</summary>
    public void RemoveSegment(PromptSegment segment)
    {
        segment.PropertyChanged -= OnSegmentChanged;
        _promptSegments.Remove(segment);
        Recompose();
    }

    private void OnSegmentChanged(object? sender, PropertyChangedEventArgs e) => Recompose();

    /// <summary>按类别与取值匹配可选项；清单外的变量或符号以临时项呈现。</summary>
    private ISegmentChoice FindChoice(PromptSegmentKind kind, string value)
    {
        foreach (var choice in _segmentChoices)
        {
            if (choice.Kind == kind && string.Equals(choice.Value, value, StringComparison.Ordinal))
            {
                return choice;
            }
        }

        return kind == PromptSegmentKind.Variable
            ? new VariableOption(new StarshipVariableDefinition(value, value, value, "其他", "Other", value))
            : new SymbolOption(value, value, value);
    }

    // --- 写入 / 恢复 ---

    public async Task ApplyPromptAsync()
    {
        var format = _promptFormat;
        var addNewline = _promptAddNewline;
        var useForwardSlash = _promptUseForwardSlash;
        await RunBusyAsync(
            "正在写入自定义提示符…",
            () => _promptService.Apply(format, addNewline, useForwardSlash),
            static _ => new[]
            {
                (StatusType.Saved, "自定义提示符已写入 starship.toml，原配置已单独备份；打开新终端即可看到效果。"),
            });
    }

    public async Task RestorePromptAsync()
    {
        await RunBusyAsync(
            "正在恢复应用前的提示符配置…",
            () => _promptService.Restore(),
            static restored => new[]
            {
                restored
                    ? (StatusType.Saved, "已恢复应用前的 starship.toml，自定义提示符已移除。")
                    : ((StatusType.Warning, "没有可恢复的应用前备份。")),
            });

        RefreshPromptState(forceReload: true);
        RefreshCommonOptions(forceReload: true);
    }

    /// <summary>只把片段组合重置为默认值，不写文件；需要再点「保存设置」。</summary>
    public void ResetPrompt()
    {
        LoadFormat(StarshipPromptService.CreateDefaultFormat());
        SetFeedback(StatusType.Info, "已恢复默认提示符，点「保存设置」才会写入。");
    }
}
