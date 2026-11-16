using System.Text.RegularExpressions;
using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>
/// 主题卡上的方案切换与统一操作入口：提示符方案二选一，三个按钮共用一套状态与反馈。
/// 保存时按当前方案只写入一种提示符配置，再提交有变化的外观项；外观无改动时跳过。
/// </summary>
public sealed partial class BeautifyViewModel
{
    private bool _useCustomPrompt;

    /// <summary>当前是否使用自定义提示符；初始按磁盘上是否存在 PAM 托管块推断。</summary>
    public bool UseCustomPrompt
    {
        get => _useCustomPrompt;
        set
        {
            if (_useCustomPrompt == value) return;
            _useCustomPrompt = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UsePresetScheme));
        }
    }

    /// <summary>单选组里「主题预设」的反向绑定，避免额外的取反转换器。</summary>
    public bool UsePresetScheme
    {
        get => !_useCustomPrompt;
        set => UseCustomPrompt = !value;
    }

    public bool CanRestoreDefaults => !IsBusy;
    public bool CanDiscardChanges => !IsBusy;
    public bool CanSaveSettings => !IsBusy;

    /// <summary>把当前方案恢复成程序默认值，不写盘；需要再点「保存设置」。</summary>
    public void RestoreDefaults()
    {
        if (IsBusy) return;

        ResetCommonOptions();
        SingleWindowMode = false;
        if (UseCustomPrompt)
        {
            ResetPrompt();
            return;
        }

        SelectedPresetId = _starshipService.GetPresets()
            .FirstOrDefault(static p => !p.RequiresNerdFont)?.Id ?? string.Empty;
        SetFeedback(StatusType.Info, "已恢复默认主题，点「保存设置」才会写入。");
    }

    /// <summary>放弃所有未保存的改动，回到磁盘上的状态（含方案选择、提示符片段、主题与外观）。</summary>
    public void DiscardChanges()
    {
        if (IsBusy) return;

        DiscardAppearance();
        RefreshPromptState(forceReload: true);
        RefreshCommonOptions(forceReload: true);
        UseCustomPrompt = _promptCustomized;
        SelectedPresetId = _detectedPresetId
            ?? _starshipService.GetPresets().FirstOrDefault(static p => !p.RequiresNerdFont)?.Id
            ?? string.Empty;
        SetFeedback(StatusType.Info, "已放弃未保存的改动，回到上次保存的状态。");
    }

    public async Task SaveSettingsAsync()
    {
        if (IsBusy) return;

        string? presetId = null;
        var format = string.Empty;
        var addNewline = _promptAddNewline;
        var useForwardSlash = _promptUseForwardSlash;

        if (UseCustomPrompt)
        {
            format = _promptFormat;
            if (string.IsNullOrWhiteSpace(format))
            {
                SetFeedback(StatusType.Warning, "请先添加至少一个片段。");
                return;
            }
        }
        else
        {
            var preset = _starshipService.GetPresets().FirstOrDefault(p => p.Id == _selectedPresetId);
            if (preset is null)
            {
                SetFeedback(StatusType.Warning, "请先选择一个主题预设。");
                return;
            }

            presetId = preset.Id;
        }

        var color = CursorColor.Trim();
        if (color.Length > 0 && !Regex.IsMatch(color, "^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$"))
        {
            SetFeedback(StatusType.Warning, "光标颜色要写成 #RGB 或 #RRGGBB；留空表示使用终端默认颜色。");
            return;
        }

        var changes = _hasWtSettings ? BuildChanges() : [];

        IsBusy = true;
        SetFeedback(StatusType.Info, "正在保存设置…");

        try
        {
            if (UseCustomPrompt)
            {
                await Task.Run(() => _promptService.Apply(format, addNewline, useForwardSlash));
            }
            else
            {
                var (profilePath, _) = await Task.Run(
                    () => _profileService.ResolveProfilePath(AppServices.Settings.TerminalType));
                await Task.Run(() => _starshipService.ApplyPreset(
                    presetId!, profilePath, (addNewline, useForwardSlash)));
            }

            string? appearanceError = null;
            if (changes.Count > 0)
            {
                var result = await Task.Run(() => _terminalService.ApplyAppearance(changes));
                if (!result.Success)
                {
                    appearanceError = result.Message;
                }
            }

            if (appearanceError is not null)
            {
                SetFeedback(StatusType.Error, "提示符设置已保存，但外观保存失败：{0}", appearanceError);
            }
            else if (!_isBundledFontInstalled)
            {
                SetFeedback(StatusType.Warning, "已保存设置，但没装图标字体，符号可能显示成方块。");
            }
            else if (changes.Count > 0)
            {
                SetFeedback(
                    StatusType.Saved,
                    "已保存提示符设置与 {0} 项外观改动；打开新终端窗口即可看到效果。",
                    changes.Count);
                RefreshAppearanceFromDisk();
            }
            else
            {
                SetFeedback(StatusType.Saved, "已保存提示符设置；打开新终端窗口即可看到效果。");
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
}
