using System.ComponentModel;
using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>
/// 配置维护功能：打开 / 恢复 starship.toml，以及恢复 Windows Terminal 的 settings.json 备份。
/// 修改配置维护逻辑只看本文件。
/// </summary>
public sealed partial class BeautifyViewModel
{
    public bool HasWtSettings => _hasWtSettings;
    public bool HasTomlBackup => _hasTomlBackup;
    public bool HasWtBackup => _hasWtBackup;
    public string TomlPath => _tomlPath;
    public string WtSettingsPath => _wtSettingsPath;
    public string TomlStatusText => Loc.T(_tomlStatusSource);
    public string InitLineText => Loc.F("starship init 行：{0}", Loc.T(_initLineSource));

    public bool CanOpenToml => !IsBusy;
    public bool CanRestoreToml => !IsBusy && _hasTomlBackup;
    public bool CanRestoreWtBackup => !IsBusy && _hasWtBackup;

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
}
