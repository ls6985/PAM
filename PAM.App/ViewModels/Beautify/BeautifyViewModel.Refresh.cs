using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>
/// 状态探测与展示：构造时探测一次，之后由页头「刷新状态」按钮或各操作（应用外观 / 安装 / 恢复等）
/// 触发重新探测 Starship、Scoop、字体、配置状态，并把结果摊平到只读展示属性。修改刷新逻辑只需看本文件。
/// </summary>
public sealed partial class BeautifyViewModel
{
    // --- 状态展示（按当前语言从 LocKey 即时生成） ---

    public string StarshipStatusText => !_starshipDetected
        ? Loc.T(LocKey.Detecting)
        : _isStarshipInstalled
            ? LocalizationService.Instance.F(LocKey.Installed0, _starshipVersion)
            : Loc.T(LocKey.NotInstalled);

    public string ScoopStatusText => !_scoopDetected
        ? Loc.T(LocKey.Detecting)
        : _isScoopInstalled
            ? Loc.T(LocKey.ScoopIsReady)
            : Loc.T(LocKey.ScoopNotDetected);
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
            var wtAppearance = await Task.Run(() => _terminalService.ReadAppearance(wtState.SettingsPath));
            var wtFlags = await Task.Run(() => _terminalService.ReadGlobalFlags(wtState.SettingsPath));
            var candidates = await Task.Run(() => FontInstaller.GetMonospaceCandidates());
            var bundledInstalled = await Task.Run(() => FontInstaller.IsBundledFontInstalled());
            var state = await Task.Run(() => _starshipService.GetState(profilePath));
            var detectedPreset = await Task.Run(() => _starshipService.DetectAppliedPreset());

            _starshipDetected = true;
            _starshipVersion = detect.Version;
            _isStarshipInstalled = detect.Installed;
            _scoopDetected = true;
            _isScoopInstalled = scoopInstalled;
            _currentWtFont = wtState.CurrentFont;
            _hasWtSettings = wtState.SettingsExists;
            _wtSettingsPath = wtState.SettingsPath;
            _hasWtBackup = wtState.BackupExists;
            _isBundledFontInstalled = bundledInstalled;
            _tomlPath = state.ConfigPath;
            _hasTomlBackup = state.BackupExists;
            _detectedPresetId = detectedPreset;

            RebuildFontOptions(candidates, wtState.CurrentFont, bundledInstalled);
            RebuildPresetOptions();
            RefreshPromptState();
            RefreshCommonOptions();
            ApplyAppearanceFromState(wtState, wtAppearance, wtFlags);

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

        // 下拉重建属于「读盘回填」，不能算用户改动；否则保存按钮会无故亮起。
        _suppressAppearanceDirty = true;
        try
        {
            SelectedFont = selection;

            // ItemsSource 换新会把 ComboBox 的内部选中清空；若值恰好未变，上面的赋值不触发通知，
            // 这里强制补一次，让 ComboBox 从空选中重新匹配 SelectedItem 并重算 SelectionBoxItem。
            OnPropertyChanged(nameof(SelectedFont));
        }
        finally
        {
            _suppressAppearanceDirty = false;
        }
    }

    private void RebuildPresetOptions()
    {
        var presets = _starshipService.GetPresets();
        // ComboBox 换 ItemsSource 会把选中清空并经 TwoWay 绑定回推 null（字体下拉修过的同类问题），
        // 先保存当前选中项，刷新后恢复；未选中或选中项失效时优先采用识别出的当前主题。
        var preserve = _selectedPresetId;
        PresetOptions = presets
            .Select(static preset => new SettingOption(preset.Id, preset.DisplayNameSource))
            .ToList();
        OnPropertyChanged(nameof(PresetOptions));

        var fallback = _detectedPresetId
            ?? presets.FirstOrDefault(static p => !p.RequiresNerdFont)?.Id
            ?? string.Empty;
        SelectedPresetId = presets.Any(preset => preset.Id == preserve) ? preserve : fallback;

        // ItemsSource 换新可能清空 ComboBox 内部选中；若值恰好未变，上面的赋值不触发通知，
        // 这里强制补一次，让 ComboBox 从空选中重新匹配 SelectedItem。
        OnPropertyChanged(nameof(SelectedPresetId));
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
        OnPropertyChanged(nameof(HasTomlBackup));
        OnPropertyChanged(nameof(HasWtBackup));
        OnPropertyChanged(nameof(PresetStatusText));
        OnPropertyChanged(nameof(CanOneClickInstall));
        OnPropertyChanged(nameof(CanInstallBundledFont));
        OnPropertyChanged(nameof(CanApplyAppearance));
        OnPropertyChanged(nameof(CanDiscardAppearance));
        OnPropertyChanged(nameof(AppearanceStatusText));
        OnPropertyChanged(nameof(WtBackupText));
        OnPropertyChanged(nameof(WtBackupPathText));
        OnPropertyChanged(nameof(CanApplyPreset));
        OnPropertyChanged(nameof(CanSaveSettings));
        OnPropertyChanged(nameof(CanRestoreDefaults));
        OnPropertyChanged(nameof(CanDiscardChanges));
        OnPropertyChanged(nameof(CanOpenToml));
        OnPropertyChanged(nameof(CanRestoreToml));
        OnPropertyChanged(nameof(CanRestoreWtBackup));
        RaisePromptChanged();
    }
}
