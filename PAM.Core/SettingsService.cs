using System.Text.Json;
using System.Text.Json.Serialization;

namespace PAM.Core;

/// <summary>
/// 将 <see cref="AppSettings"/> 读写到 <c>%AppData%\PAM\settings.json</c>。
/// 读取失败或文件损坏时回退默认值，保证启动不受影响。
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        // 配置文件给人看：热键里的 + 等字符不做 \uXXXX 转义。
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>settings.json 的完整路径。</summary>
    public string SettingsFilePath { get; }

    /// <summary>默认位置：<c>%AppData%\PAM\settings.json</c>。</summary>
    public SettingsService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PAM",
            "settings.json"))
    {
    }

    public SettingsService(string settingsFilePath)
    {
        if (string.IsNullOrWhiteSpace(settingsFilePath))
        {
            throw new ArgumentException("设置文件路径不能为空。", nameof(settingsFilePath));
        }

        SettingsFilePath = settingsFilePath;
    }

    /// <summary>读取设置；文件不存在或无法解析时返回默认设置。</summary>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AppSettings();
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (Exception)
        {
            // 设置损坏、被占用或无权限时一律回退默认值，不让设置页拖垮启动。
            return new AppSettings();
        }
    }

    /// <summary>返回配置文件最后写入时间；文件不存在时返回 <see cref="DateTime.MinValue"/>。</summary>
    public DateTime GetLastWriteTimeUtc()
    {
        try
        {
            return File.Exists(SettingsFilePath) ? File.GetLastWriteTimeUtc(SettingsFilePath) : DateTime.MinValue;
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    /// <summary>写入设置；目录不存在时自动创建。</summary>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(SettingsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        settings.Normalize();
        var json = JsonSerializer.Serialize(settings, SerializerOptions);

        // 先写临时文件再原子替换：托盘进程会并发读取 settings.json，
        // 直接 WriteAllText 可能让它读到写了一半的 JSON，导致热键被错误重置。
        var tempPath = SettingsFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, SettingsFilePath, overwrite: true);
    }
}
