using System.Text.Json;
using System.Text.Json.Serialization;

namespace PAM.Core;

/// <summary>
/// 读写 <c>%AppData%\PAM\pam-data.json</c>。文件损坏或缺失时回退空数据，
/// 与 <see cref="SettingsService"/> 共用同一套序列化与原子替换规则。
/// </summary>
public sealed class PamDataService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string DataFilePath { get; }

    public PamDataService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PAM",
            "pam-data.json"))
    {
    }

    public PamDataService(string dataFilePath)
    {
        if (string.IsNullOrWhiteSpace(dataFilePath))
        {
            throw new ArgumentException("数据文件路径不能为空。", nameof(dataFilePath));
        }

        DataFilePath = dataFilePath;
    }

    /// <summary>读取托管数据；文件不存在或无法解析时返回空数据。</summary>
    public PamData Load()
    {
        try
        {
            if (!File.Exists(DataFilePath))
            {
                return new PamData();
            }

            var json = File.ReadAllText(DataFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new PamData();
            }

            var data = JsonSerializer.Deserialize<PamData>(json, SerializerOptions) ?? new PamData();
            data.Normalize();
            return data;
        }
        catch (Exception)
        {
            return new PamData();
        }
    }

    /// <summary>写入托管数据；目录不存在时自动创建。</summary>
    public void Save(PamData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var directory = Path.GetDirectoryName(DataFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        data.Normalize();
        var json = JsonSerializer.Serialize(data, SerializerOptions);

        var tempPath = DataFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, DataFilePath, overwrite: true);
    }
}
