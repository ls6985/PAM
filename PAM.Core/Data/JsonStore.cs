using System.Text.Json;
using System.Text.Json.Serialization;

namespace PAM.Core;

/// <summary>
/// JSON 文件读写与原子替换的通用基类。子类只指定数据类型、默认实例与归一化方式，
/// 序列化和“临时文件 + 原子替换”的逻辑由这里统一提供，避免各存储类重复实现。
/// </summary>
/// <typeparam name="T">持久化数据的类型，须为引用类型。</typeparam>
public abstract class JsonStore<T> where T : class
{
    // 配置文件给人看：热键里的 + 等字符不做 \uXXXX 转义。
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>被管理文件的完整路径。</summary>
    public string FilePath { get; }

    protected JsonStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("文件路径不能为空。", nameof(filePath));
        }

        FilePath = filePath;
    }

    /// <summary>文件缺失或无法解析时返回的默认实例。</summary>
    protected abstract T CreateDefault();

    /// <summary>反序列化后 / 保存前对实例做归一化（修正空集合、非法值等）。</summary>
    protected abstract void Normalize(T data);

    /// <summary>读取数据；文件不存在或无法解析时返回默认实例，不让读取失败拖垮启动。</summary>
    public T Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return CreateDefault();
            }

            var json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return CreateDefault();
            }

            var data = JsonSerializer.Deserialize<T>(json, SerializerOptions) ?? CreateDefault();
            Normalize(data);
            return data;
        }
        catch (Exception)
        {
            return CreateDefault();
        }
    }

    /// <summary>返回配置文件最后写入时间；文件不存在时返回 <see cref="DateTime.MinValue"/>。</summary>
    public DateTime GetLastWriteTimeUtc()
    {
        try
        {
            return File.Exists(FilePath) ? File.GetLastWriteTimeUtc(FilePath) : DateTime.MinValue;
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    /// <summary>写入数据；目录不存在时自动创建，先写临时文件再原子替换以防并发读取方读到半截 JSON。</summary>
    public void Save(T data)
    {
        ArgumentNullException.ThrowIfNull(data);

        Normalize(data);
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(data, SerializerOptions);
        var tempPath = FilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, FilePath, overwrite: true);
    }
}
