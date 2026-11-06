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

            return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
        }
        catch (Exception)
        {
            // 设置损坏、被占用或无权限时一律回退默认值，不让设置页拖垮启动。
            return new AppSettings();
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

        File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(settings, SerializerOptions));
    }
}
