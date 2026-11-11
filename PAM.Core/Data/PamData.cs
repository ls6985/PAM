namespace PAM.Core;

/// <summary>
/// PAM 托管的用户数据，持久化在 <c>%AppData%\PAM\pam-data.json</c>。
/// $PROFILE 的托管区由这份数据单向生成，不从 $PROFILE 回读。
/// </summary>
public sealed class PamData
{
    public const int CurrentVersion = 1;

    /// <summary>数据格式版本，供后续结构演进时做兼容迁移。</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>最近一次生成的托管区内容哈希，用于识别 $PROFILE 是否被外部修改。</summary>
    public string? ManagedHash { get; set; }

    public List<AliasData> Aliases { get; set; } = [];

    public List<CustomCommandData> CustomCommands { get; set; } = [];

    /// <summary>修正缺失字段与空白名称，保证下游拿到的列表可直接使用。</summary>
    public void Normalize()
    {
        if (Version <= 0)
        {
            Version = CurrentVersion;
        }

        Aliases ??= [];
        CustomCommands ??= [];

        Aliases.RemoveAll(static alias => string.IsNullOrWhiteSpace(alias?.Name));
        CustomCommands.RemoveAll(static command => string.IsNullOrWhiteSpace(command?.Name));

        foreach (var alias in Aliases)
        {
            alias.Name = alias.Name.Trim();
            alias.Path = alias.Path ?? string.Empty;
            alias.Remark = alias.Remark ?? string.Empty;
            alias.Arguments = alias.Arguments ?? string.Empty;
        }

        foreach (var command in CustomCommands)
        {
            command.Name = command.Name.Trim();
            command.CodeLines ??= [];
            command.Remark = command.Remark ?? string.Empty;
            command.Arguments = command.Arguments ?? string.Empty;
        }
    }

    public PamData Clone()
    {
        var clone = new PamData
        {
            Version = Version,
            ManagedHash = ManagedHash,
            Aliases = Aliases.Select(static alias => new AliasData
            {
                Name = alias.Name,
                Path = alias.Path,
                Remark = alias.Remark,
                Arguments = alias.Arguments,
            }).ToList(),
            CustomCommands = CustomCommands.Select(static command => new CustomCommandData
            {
                Name = command.Name,
                CodeLines = [.. command.CodeLines],
                IsEnabled = command.IsEnabled,
                Remark = command.Remark,
                AllowsArguments = command.AllowsArguments,
                Arguments = command.Arguments,
            }).ToList(),
        };

        return clone;
    }
}

/// <summary>一条别名启动记录，对应托管区里生成的同名函数。</summary>
public sealed class AliasData
{
    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Remark { get; set; } = string.Empty;

    /// <summary>每次启动该程序时固定附带的参数字符串，空白分隔，双引号用于包裹含空格的段。</summary>
    public string Arguments { get; set; } = string.Empty;
}

/// <summary>一条自定义命令，代码按行存储以保持 JSON 可读。</summary>
public sealed class CustomCommandData
{
    public string Name { get; set; } = string.Empty;

    /// <summary>命令体（不含 <c>function</c> 外壳），渲染时包成以 <see cref="Name"/> 命名的函数。</summary>
    public string[] CodeLines { get; set; } = [];

    public bool IsEnabled { get; set; } = true;

    public string Remark { get; set; } = string.Empty;

    /// <summary>是否允许在命令名后传入参数，参数追加到命令体末尾。</summary>
    public bool AllowsArguments { get; set; }

    /// <summary>每次调用都附带的固定参数。</summary>
    public string Arguments { get; set; } = string.Empty;
}
