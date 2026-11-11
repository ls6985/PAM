using System.Collections.ObjectModel;
using System.ComponentModel;
using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>
/// 主题预设功能：把内置 Starship 主题写入 starship.toml（写前单槽备份，一并写入两个通用
/// 提示符选项），以及下拉选项维护。修改主题逻辑只看本文件。
/// </summary>
public sealed partial class BeautifyViewModel
{
    public IReadOnlyList<SettingOption> PresetOptions { get; private set; } = [];

    public string SelectedPresetId
    {
        get => _selectedPresetId;
        set => SetField(ref _selectedPresetId, value ?? string.Empty);
    }

    public bool CanApplyPreset => !IsBusy && PresetOptions.Count > 0;

    /// <summary>
    /// 主题卡状态行：自定义提示符接管 / 已识别的当前主题 / 未应用预设。
    /// 依赖 _promptCustomized 与 _detectedPresetId，刷新尾部统一通知。
    /// </summary>
    public string PresetStatusText
    {
        get
        {
            if (_promptCustomized)
            {
                return Loc.T("当前使用自定义提示符；重新应用主题会覆盖它。");
            }

            if (_detectedPresetId is not null)
            {
                var preset = _starshipService.GetPresets().FirstOrDefault(p => p.Id == _detectedPresetId);
                if (preset is not null)
                {
                    return Loc.F("当前主题：{0}", Loc.T(preset.DisplayNameSource));
                }
            }

            return Loc.T("当前未应用内置主题预设。");
        }
    }

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
}
