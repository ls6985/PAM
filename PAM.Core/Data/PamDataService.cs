namespace PAM.Core;

/// <summary>
/// 读写 <c>%AppData%\PAM\pam-data.json</c>。文件损坏或缺失时回退空数据。
/// 序列化与原子替换规则由 <see cref="JsonStore{T}"/> 统一提供，本类只指定数据类型与归一化。
/// </summary>
public sealed class PamDataService : JsonStore<PamData>
{
    public PamDataService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PAM",
            "pam-data.json"))
    {
    }

    public PamDataService(string dataFilePath)
        : base(dataFilePath)
    {
    }

    /// <summary>数据文件路径，供界面展示与复制。</summary>
    public string DataFilePath => FilePath;

    protected override PamData CreateDefault() => new();

    protected override void Normalize(PamData data) => data.Normalize();
}
