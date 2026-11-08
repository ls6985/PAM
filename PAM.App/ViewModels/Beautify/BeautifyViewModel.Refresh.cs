using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>
/// 状态探测与展示：进入页签时 / 每次操作后重新探测 Starship、Scoop、字体、配置状态，
/// 并把结果摊平到只读展示属性。修改刷新逻辑只需看本文件。
/// </summary>
public sealed partial class BeautifyViewModel
{
    // --- 状态展示（保存原文，读取时翻译，语言切换零成本） ---

    public string StarshipStatusText => Loc.T(_starshipStatusSource);
    public string ScoopStatusText => Loc.T(_scoopStatusSource);
    public bool IsStarshipInstalled => _isStarshipInstalled;
    public bool IsScoopInstalled => _isScoopInstalled;

    public bool CanRefresh => !IsBusy;

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
            RefreshPromptState();

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
        RaisePromptChanged();
    }
}
