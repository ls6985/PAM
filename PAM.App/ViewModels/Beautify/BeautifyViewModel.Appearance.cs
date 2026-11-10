using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>
/// 「外观」功能：把 Windows Terminal 设置里「默认值 → 外观」的 7 组配置
/// （字体、复古风格、光标形状 / 颜色、背景图、背景图透明度、窗口透明度、亚克力）
/// 映射成一组可编辑控件，一次性比较差异后写入 settings.json。
/// <para>
/// 值全部以「界面状态」为准：读盘时存一份快照，点保存只提交与快照不同的项目；
/// 留空 / 选「终端默认」等于删除该属性，把控制权交还 Windows Terminal。
/// 未改动的项不会被写进配置文件，避免无谓地把默认值写死。
/// </para>
/// <para>
/// 字体的下拉候选、内置 Cascadia Code NF 安装也放在本文件（原本独立的 Font.cs 已并入）；
/// 字体值不再单独应用，与其它外观项一起由「保存外观设置」统一写入。
/// </para>
/// </summary>
public sealed partial class BeautifyViewModel
{
    private AppearanceSnapshot _appearanceSnapshot = AppearanceSnapshot.Default;
    private bool _appearanceDirty;
    private bool _suppressAppearanceDirty;

    private bool _retroTerminalEffect;
    private string _cursorShapeId = string.Empty;
    private string _cursorColor = string.Empty;
    private string _backgroundImage = string.Empty;
    private int _backgroundImageOpacity = 100;
    private int _windowOpacity = 100;
    private bool _useAcrylic;

    // --- 字体（外观卡片的「文本」分组）：下拉候选、内置 Nerd Font 安装 ---

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
                MarkAppearanceDirty();
                OnPropertyChanged(nameof(HasFontSelection));
                OnPropertyChanged(nameof(CanOneClickInstall));
            }
        }
    }

    public bool HasFontSelection => !string.IsNullOrWhiteSpace(_selectedFont);
    public bool IsBundledFontInstalled => _isBundledFontInstalled;

    public string CurrentFontText => _currentWtFont is null
        ? Loc.T("Windows Terminal 当前字体：未设置（跟随 WT 默认）")
        : Loc.F("Windows Terminal 当前字体：{0}", _currentWtFont);

    public bool CanInstallBundledFont => !IsBusy && !_isBundledFontInstalled;

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

    /// <summary>光标形状下拉：首项 Id 为空，代表「终端默认」（删除属性）。</summary>
    public IReadOnlyList<SettingOption> CursorShapeOptions { get; } =
        TerminalAppearanceSchema.CursorShapes
            .Select(static option => new SettingOption(option.Id, option.Name))
            .ToList();

    public bool RetroTerminalEffect
    {
        get => _retroTerminalEffect;
        set
        {
            if (SetField(ref _retroTerminalEffect, value)) MarkAppearanceDirty();
        }
    }

    public string SelectedCursorShapeId
    {
        get => _cursorShapeId;
        set
        {
            if (SetField(ref _cursorShapeId, value ?? string.Empty)) MarkAppearanceDirty();
        }
    }

    public string CursorColor
    {
        get => _cursorColor;
        set
        {
            if (SetField(ref _cursorColor, value ?? string.Empty)) MarkAppearanceDirty();
        }
    }

    public string BackgroundImage
    {
        get => _backgroundImage;
        set
        {
            if (SetField(ref _backgroundImage, value ?? string.Empty)) MarkAppearanceDirty();
        }
    }

    /// <summary>背景图透明度，0~100（写入时换算成 WT 的 0~1）。</summary>
    public int BackgroundImageOpacity
    {
        get => _backgroundImageOpacity;
        set
        {
            if (SetField(ref _backgroundImageOpacity, Math.Clamp(value, 0, 100))) MarkAppearanceDirty();
        }
    }

    /// <summary>窗口透明度，0~100，直接对应 WT 的 opacity。</summary>
    public int WindowOpacity
    {
        get => _windowOpacity;
        set
        {
            if (SetField(ref _windowOpacity, Math.Clamp(value, 0, 100))) MarkAppearanceDirty();
        }
    }

    public bool UseAcrylic
    {
        get => _useAcrylic;
        set
        {
            if (SetField(ref _useAcrylic, value)) MarkAppearanceDirty();
        }
    }

    public string BackgroundImageOpacityText => Loc.F("{0}%", _backgroundImageOpacity);
    public string WindowOpacityText => Loc.F("{0}%", _windowOpacity);

    public bool IsAppearanceDirty => _appearanceDirty;
    public bool CanApplyAppearance => !IsBusy && _hasWtSettings && _appearanceDirty;
    public bool CanDiscardAppearance => !IsBusy && _appearanceDirty;

    public string AppearanceStatusText => !_hasWtSettings
        ? Loc.T("未找到 Windows Terminal 的 settings.json，请先启动一次 Windows Terminal。")
        : _appearanceDirty
            ? Loc.T("有尚未保存的修改；保存后打开新终端窗口即可看到效果。")
            : Loc.T("当前显示的是 settings.json 里已保存的值；改动后点「保存外观设置」。");

    // --- 读盘 / 载入界面 ---

    /// <summary>构造与页签刷新时调用：把磁盘上的外观值填进控件，并重建「未修改」快照。</summary>
    /// <param name="resetFontSelection">恢复原始配置后为 true：连字体下拉也按盘上值重新选，避免残留旧的选中项。</param>
    public void RefreshAppearanceFromDisk(bool resetFontSelection = false)
    {
        var state = _terminalService.GetState();
        _wtBackupPath = state.BackupPath;
        _wtBackupCreatedAt = state.BackupCreatedAt;

        if (resetFontSelection)
        {
            _suppressAppearanceDirty = true;
            try
            {
                _selectedFont = null;
                RebuildFontOptions(
                    FontInstaller.GetMonospaceCandidates(), state.CurrentFont, _isBundledFontInstalled);
            }
            finally
            {
                _suppressAppearanceDirty = false;
            }
        }

        ApplyAppearanceToUi(_terminalService.ReadAppearance(state.SettingsPath));
        OnPropertyChanged(nameof(WtBackupText));
        OnPropertyChanged(nameof(WtBackupPathText));
    }

    /// <summary>刷新状态时使用：已有 GetState / ReadAppearance 的结果，避免重复读盘。</summary>
    private void ApplyAppearanceFromState(WindowsTerminalState state, TerminalAppearance appearance)
    {
        _wtBackupPath = state.BackupPath;
        _wtBackupCreatedAt = state.BackupCreatedAt;
        OnPropertyChanged(nameof(WtBackupText));
        OnPropertyChanged(nameof(WtBackupPathText));

        // 用户正在编辑时不要用磁盘值盖掉未保存的改动。
        if (_appearanceDirty)
        {
            return;
        }

        ApplyAppearanceToUi(appearance);
    }

    private void ApplyAppearanceToUi(TerminalAppearance appearance)
    {
        ApplySnapshot(new AppearanceSnapshot(
            appearance.FontFace ?? _selectedFont,
            appearance.RetroTerminalEffect ?? false,
            appearance.CursorShape ?? string.Empty,
            appearance.CursorColor ?? string.Empty,
            appearance.BackgroundImage ?? string.Empty,
            appearance.BackgroundImageOpacity is { } bg
                ? Math.Clamp((int)Math.Round(bg * 100), 0, 100)
                : 100,
            appearance.Opacity ?? 100,
            appearance.UseAcrylic ?? false));
    }

    /// <summary>把一组值（读盘结果或已保存快照）整体回填控件，并重置「未修改」状态。</summary>
    private void ApplySnapshot(AppearanceSnapshot snapshot)
    {
        _suppressAppearanceDirty = true;
        try
        {
            _retroTerminalEffect = snapshot.Retro;
            _cursorShapeId = snapshot.CursorShape;
            _cursorColor = snapshot.CursorColor;
            _backgroundImage = snapshot.BackgroundImage;
            _backgroundImageOpacity = snapshot.BackgroundImageOpacity;
            _windowOpacity = snapshot.WindowOpacity;
            _useAcrylic = snapshot.UseAcrylic;
            if (snapshot.Font is not null)
            {
                SelectedFont = snapshot.Font;
            }

            _appearanceSnapshot = SnapshotFromUi();
            _appearanceDirty = false;
            RaiseAppearanceChanged();
        }
        finally
        {
            _suppressAppearanceDirty = false;
        }
    }

    private AppearanceSnapshot SnapshotFromUi() => new(
        _selectedFont,
        _retroTerminalEffect,
        _cursorShapeId,
        _cursorColor,
        _backgroundImage,
        _backgroundImageOpacity,
        _windowOpacity,
        _useAcrylic);

    private void RaiseAppearanceChanged()
    {
        OnPropertyChanged(nameof(RetroTerminalEffect));
        OnPropertyChanged(nameof(SelectedCursorShapeId));
        OnPropertyChanged(nameof(CursorColor));
        OnPropertyChanged(nameof(BackgroundImage));
        OnPropertyChanged(nameof(BackgroundImageOpacity));
        OnPropertyChanged(nameof(WindowOpacity));
        OnPropertyChanged(nameof(UseAcrylic));
        OnPropertyChanged(nameof(BackgroundImageOpacityText));
        OnPropertyChanged(nameof(WindowOpacityText));
        OnPropertyChanged(nameof(IsAppearanceDirty));
        OnPropertyChanged(nameof(CanApplyAppearance));
        OnPropertyChanged(nameof(CanDiscardAppearance));
        OnPropertyChanged(nameof(AppearanceStatusText));
    }

    private void MarkAppearanceDirty()
    {
        if (_suppressAppearanceDirty)
        {
            return;
        }

        if (!_appearanceDirty)
        {
            _appearanceDirty = true;
            OnPropertyChanged(nameof(IsAppearanceDirty));
            OnPropertyChanged(nameof(CanApplyAppearance));
            OnPropertyChanged(nameof(CanDiscardAppearance));
            OnPropertyChanged(nameof(AppearanceStatusText));
        }

        // 滑块可能连续拖动，百分比文字每次都要跟着变。
        OnPropertyChanged(nameof(BackgroundImageOpacityText));
        OnPropertyChanged(nameof(WindowOpacityText));
    }

    // --- 操作 ---

    /// <summary>放弃更改：控件整体回到上次读盘 / 保存时的值（包括字体下拉）。</summary>
    public void DiscardAppearance() => ApplySnapshot(_appearanceSnapshot);

    /// <summary>选一张本地图片作为终端背景图（WT 也支持 desktopWallpaper / ms-appdata URI，可手填）。</summary>
    public void BrowseBackgroundImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.T("选择终端背景图"),
            Filter = Loc.T("图片文件 (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|所有文件 (*.*)|*.*"),
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() == true)
        {
            BackgroundImage = dialog.FileName;
        }
    }

    public void ClearBackgroundImage() => BackgroundImage = string.Empty;

    /// <summary>只把有变化的项目提交给 Core；任一项校验不过就不写盘。</summary>
    public async Task ApplyAppearanceAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!_hasWtSettings)
        {
            SetFeedback(StatusType.Warning, Loc.T("未找到 Windows Terminal 的 settings.json，请先启动一次 Windows Terminal。"));
            return;
        }

        var color = CursorColor.Trim();
        if (color.Length > 0 && !Regex.IsMatch(color, "^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$"))
        {
            SetFeedback(StatusType.Warning, "光标颜色要写成 #RGB 或 #RRGGBB；留空表示使用终端默认颜色。");
            return;
        }

        var changes = BuildChanges();
        if (changes.Count == 0)
        {
            SetFeedback(StatusType.Info, "没有需要保存的更改。");
            return;
        }

        IsBusy = true;
        SetFeedback(StatusType.Info, "正在保存终端外观设置…");
        var succeeded = false;

        try
        {
            var result = await Task.Run(() => _terminalService.ApplyAppearance(changes));
            succeeded = result.Success;
            if (result.Success)
            {
                // 用带占位符的模板走本地化；Core 返回的是已拼好的中文提示，不适合做词表键。
                SetFeedback(StatusType.Saved, "已保存 {0} 项终端外观设置，打开新终端窗口即可看到效果。", changes.Count);
            }
            else
            {
                SetFeedback(StatusType.Error, result.Message);
            }
        }
        catch (Exception ex)
        {
            SetFeedback(StatusType.Error, "操作失败：{0}", ex.Message);
        }
        finally
        {
            IsBusy = false;
            if (succeeded)
            {
                // 保存成功后把「已保存」状态同步回控件，按钮回到禁用。
                RefreshAppearanceFromDisk();
            }

            await RefreshStateAsync();
        }
    }

    private List<TerminalAppearanceChange> BuildChanges()
    {
        var before = _appearanceSnapshot;
        var changes = new List<TerminalAppearanceChange>();

        var font = _selectedFont?.Trim() ?? string.Empty;
        if (font != (before.Font ?? string.Empty) && font.Length > 0)
        {
            changes.Add(TerminalAppearanceChange.SetString(TerminalAppearanceKey.FontFace, font));
        }

        if (RetroTerminalEffect != before.Retro)
        {
            changes.Add(TerminalAppearanceChange.SetBool(
                TerminalAppearanceKey.RetroTerminalEffect, RetroTerminalEffect));
        }

        var shape = SelectedCursorShapeId;
        if (shape != before.CursorShape)
        {
            changes.Add(shape.Length == 0
                ? TerminalAppearanceChange.Remove(TerminalAppearanceKey.CursorShape)
                : TerminalAppearanceChange.SetString(TerminalAppearanceKey.CursorShape, shape));
        }

        var color = CursorColor.Trim();
        if (color != before.CursorColor)
        {
            changes.Add(color.Length == 0
                ? TerminalAppearanceChange.Remove(TerminalAppearanceKey.CursorColor)
                : TerminalAppearanceChange.SetString(TerminalAppearanceKey.CursorColor, color));
        }

        var image = BackgroundImage.Trim();
        if (image != before.BackgroundImage)
        {
            changes.Add(image.Length == 0
                ? TerminalAppearanceChange.Remove(TerminalAppearanceKey.BackgroundImage)
                : TerminalAppearanceChange.SetString(TerminalAppearanceKey.BackgroundImage, image));
        }

        if (BackgroundImageOpacity != before.BackgroundImageOpacity)
        {
            changes.Add(TerminalAppearanceChange.SetNumber(
                TerminalAppearanceKey.BackgroundImageOpacity, BackgroundImageOpacity / 100d));
        }

        if (WindowOpacity != before.WindowOpacity)
        {
            changes.Add(TerminalAppearanceChange.SetNumber(
                TerminalAppearanceKey.Opacity, WindowOpacity));
        }

        if (UseAcrylic != before.UseAcrylic)
        {
            changes.Add(TerminalAppearanceChange.SetBool(TerminalAppearanceKey.UseAcrylic, UseAcrylic));
        }

        return changes;
    }

    /// <summary>控件状态快照；保存时与它逐项比较，决定要写哪些属性。</summary>
    private sealed record AppearanceSnapshot(
        string? Font,
        bool Retro,
        string CursorShape,
        string CursorColor,
        string BackgroundImage,
        int BackgroundImageOpacity,
        int WindowOpacity,
        bool UseAcrylic)
    {
        public static AppearanceSnapshot Default { get; } =
            new(null, false, string.Empty, string.Empty, string.Empty, 100, 100, false);
    }
}
