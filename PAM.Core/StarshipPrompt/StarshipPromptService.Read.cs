using System.IO;
using System.Text;

namespace PAM.Core;

public sealed partial class StarshipPromptService
{
    /// <summary>读取当前状态。没有 PAM 标记块时返回固定的默认 format。</summary>
    public StarshipPromptState Read()
    {
        var path = GetConfigPath();
        var exists = File.Exists(path);
        var content = exists ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
        var parsed = TryParse(content);

        return new StarshipPromptState(
            path,
            exists,
            parsed is not null,
            File.Exists(GetBackupPath()),
            parsed ?? CreateDefaultFormat());
    }

    /// <summary>
    /// 默认 format：用户名 / 主机名 / 冒号 / 路径同用一色，结束符单独一色。
    /// 固定组合（作者确认过的观感），不再从现有配置取色——托管块接管模块段落后，
    /// 应用本来就会按段颜色重写配色。
    /// </summary>
    public static string CreateDefaultFormat()
        => "[$username](fg:#BBC4B9)[$hostname](fg:#BBC4B9)[:](fg:#BBC4B9)[$directory](fg:#BBC4B9)[$character](fg:#BFA8BF)";

    /// <summary>
    /// 从自有标记块里读回一行 Starship format；块不存在或格式不符时返回 null。
    /// 只认块内第一个段落表头之前的 format 行——托管块把模块段落写在顶层 format 之后，
    /// 段落里的 format = "…" 是模块局部样式，不是顶层 format。
    /// </summary>
    private static string? TryParse(string content)
    {
        if (string.IsNullOrEmpty(content)) return null;

        var inside = false;
        var pastHeader = false;
        string? formatLine = null;
        foreach (var rawLine in content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = rawLine.TrimStart();
            if (line.Equals(StartMarker, StringComparison.OrdinalIgnoreCase))
            {
                inside = true;
                continue;
            }

            if (!inside) continue;
            if (line.Equals(EndMarker, StringComparison.OrdinalIgnoreCase)) break;
            if (!pastHeader && TryGetHeaderName(line, out _)) pastHeader = true;
            if (!pastHeader && IsFormatAssignment(line)) formatLine = line;
        }

        if (formatLine is null) return null;

        var equals = formatLine.IndexOf('=');
        var rawValue = formatLine[(equals + 1)..].Trim();
        if (rawValue.Length < 2 || rawValue[0] != '"' || rawValue[^1] != '"') return null;

        return DecodeTomlString(rawValue[1..^1]);
    }
}
