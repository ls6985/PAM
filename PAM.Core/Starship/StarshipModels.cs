namespace PAM.Core;

/// <summary>内置主题预设：Id 对应 EmbeddedResource，DisplayNameSource 为本地化词表键。</summary>
public sealed record StarshipPreset(string Id, string DisplayNameSource, bool RequiresNerdFont);

/// <summary>Starship 探测结果：Installed 为 false 时 Version 为空。</summary>
public sealed record StarshipDetectResult(bool Installed, string Version);

/// <summary>安装类操作的结果：Success 为 false 时 Message 携带可供界面展示的原因。</summary>
public sealed record StarshipInstallResult(bool Success, string Message);

/// <summary>starship.toml 与 $PROFILE init 行的当前状态，供设置页展示。</summary>
public sealed record StarshipConfigState(
    string ConfigPath,
    bool ConfigExists,
    bool BackupExists,
    string ProfilePath,
    bool ProfileHasInitLine);

/// <summary>打开 starship.toml 的结果。</summary>
public sealed record StarshipOpenResult(string Path, bool FileCreated);

/// <summary>确保 $PROFILE init 行的结果：FileExisted 表示写入前文件是否已存在。</summary>
public sealed record StarshipInitResult(bool FileExisted, bool Updated);

/// <summary>应用主题预设的结果。</summary>
public sealed record StarshipApplyResult(bool ConfigWritten, bool ProfileUpdated);
