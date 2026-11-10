namespace PAM.Core;

/// <summary>
/// 将 <see cref="AppSettings"/> 读写到 <c>%AppData%\PAM\settings.json</c>。
/// 读取失败或文件损坏时回退默认值，保证启动不受影响。
/// 序列化与原子替换规则由 <see cref="JsonStore{T}"/> 统一提供。
/// </summary>
public sealed class SettingsService : JsonStore<AppSettings>
{
    public SettingsService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PAM",
            "settings.json"))
    {
    }

    public SettingsService(string settingsFilePath)
        : base(settingsFilePath)
    {
    }

    /// <summary>settings.json 的完整路径。</summary>
    public string SettingsFilePath => FilePath;

    protected override AppSettings CreateDefault() => new();

    protected override void Normalize(AppSettings settings) => settings.Normalize();
}
