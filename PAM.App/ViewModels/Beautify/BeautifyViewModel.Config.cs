using System.ComponentModel;
using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>
/// 配置维护功能：打开主题配置、恢复上次主题设置，以及恢复终端的原始配置备份。
/// 修改配置维护逻辑只看本文件。
/// </summary>
public sealed partial class BeautifyViewModel
{
    private string _wtBackupPath = string.Empty;
    private DateTime? _wtBackupCreatedAt;

    public bool HasWtSettings => _hasWtSettings;
    public bool HasTomlBackup => _hasTomlBackup;
    public bool HasWtBackup => _hasWtBackup;
    public string TomlPath => _tomlPath;
    public string WtSettingsPath => _wtSettingsPath;
    public string TomlStatusText => Loc.T(_tomlStatusSource);
    public string InitLineText => Loc.F("starship init 行：{0}", Loc.T(_initLineSource));

    /// <summary>原始快照是什么时候留下的：说明恢复永远回到本软件首次修改前，而不是上一次修改前。</summary>
    public string WtBackupText => _wtBackupCreatedAt is { } created
        ? Loc.F(LocKey.OriginalBackupCreatedAt0RestoringAlwaysReturnsToThisConfig,  created.ToString("yyyy-MM-dd HH:mm"))
        : Loc.T(LocKey.NoOriginalBackupYetItIsCreatedAutomaticallyTheFirstTimeThisAppChangesTerminalSettings);

    public string WtBackupPathText => string.IsNullOrEmpty(_wtBackupPath)
        ? string.Empty
        : Loc.F(LocKey.BackupFile0,  _wtBackupPath);

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
            "正在恢复主题设置…",
            () => _starshipService.RestoreConfig(),
            static restored => new[]
            {
                restored
                    ? (StatusType.Saved, "已恢复上一次的主题设置。")
                    : ((StatusType.Warning, "没有可恢复的备份。")),
            });

        RefreshCommonOptions(forceReload: true);
    }

    public async Task RestoreWtBackupAsync()
    {
        await RunBusyAsync(
            "正在恢复终端备份…",
            () => _terminalService.RestoreBackup(),
            static restored => new[]
            {
                restored
                    ? (StatusType.Saved, "已还原到第一次修改前的终端配置；备份保留，可再次还原。")
                    : ((StatusType.Warning, "没有可恢复的备份。")),
            });

        // 恢复后界面必须显示这份原始配置，未保存的编辑一律作废（含字体下拉）；
        // 备份文件仍在，再次修改后仍可回到同一份原始配置。
        RefreshAppearanceFromDisk(resetFontSelection: true);
    }
}
