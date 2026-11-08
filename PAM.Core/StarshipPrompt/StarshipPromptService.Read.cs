using System.IO;
using System.Text;

namespace PAM.Core;

public sealed partial class StarshipPromptService
{
    /// <summary>读取当前状态。没有 PAM 标记块时返回固定的默认 format 与默认选项。</summary>
    public StarshipPromptState Read()
    {
        var path = GetConfigPath();
        var exists = File.Exists(path);
        var content = exists ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
        var parsed = TryParse(content);

        return new StarshipPromptState(
            path,
            exists,
            parsed.Format is not null,
            File.Exists(GetBackupPath()),
            parsed.Format ?? CreateDefaultFormat(),
            parsed.AddNewline,
            parsed.UseForwardSlash);
    }

    /// <summary>
    /// 默认 format：用户名 / 主机名 / 冒号 / 路径同用一色，结束符单独一色。
    /// 固定组合（作者确认过的观感），不再从现有配置取色——托管块接管模块段落后，
    /// 应用本来就会按段颜色重写配色。
    /// </summary>
    public static string CreateDefaultFormat()
        => "[$username](fg:#BBC4B9)[$hostname](fg:#BBC4B9)[:](fg:#BBC4B9)[$directory](fg:#BBC4B9)[$character](fg:#BFA8BF)";

    /// <summary>默认选项：提示符前不空一行、路径分隔符用 /。块里没写对应键时也按这两个默认值回显。</summary>
    public static (bool AddNewline, bool UseForwardSlash) CreateDefaultOptions() => (false, true);

    /// <summary>
    /// 从自有标记块里读回一行 Starship format 与两个选项；块不存在或格式不符时 format 为 null。
    /// 只认块内第一个段落表头之前的 format 行——托管块把模块段落写在顶层 format 之后，
    /// 段落里的 format = "…" 是模块局部样式，不是顶层 format。add_newline 取同一区域的根级键，
    /// use_os_path_sep 取 [directory] 段内的键（false = 路径分隔符显示为 /）。
    /// 块里没写的键回退到默认选项。
    /// </summary>
    private static (string? Format, bool AddNewline, bool UseForwardSlash) TryParse(string content)
    {
        var (addNewline, useForwardSlash) = CreateDefaultOptions();
        if (string.IsNullOrEmpty(content)) return (null, addNewline, useForwardSlash);

        var inside = false;
        string? formatLine = null;
        bool? addNewlineValue = null;
        string? section = null;
        bool? useOsPathSep = null;
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

            if (TryGetHeaderName(line, out var header))
            {
                section = header;
                continue;
            }

            if (section is null)
            {
                if (formatLine is null && IsFormatAssignment(line)) formatLine = line;
                if (addNewlineValue is null && IsKeyAssignment(line, "add_newline"))
                {
                    addNewlineValue = ParseTomlBool(line);
                }
            }
            else if (section == "directory" && useOsPathSep is null && IsKeyAssignment(line, "use_os_path_sep"))
            {
                useOsPathSep = ParseTomlBool(line);
            }
        }

        if (formatLine is null) return (null, addNewline, useForwardSlash);

        var equals = formatLine.IndexOf('=');
        var rawValue = formatLine[(equals + 1)..].Trim();
        if (rawValue.Length < 2 || rawValue[0] != '"' || rawValue[^1] != '"')
        {
            return (null, addNewline, useForwardSlash);
        }

        return (
            DecodeTomlString(rawValue[1..^1]),
            addNewlineValue == true,
            useOsPathSep is null ? useForwardSlash : useOsPathSep == false);
    }

    /// <summary>解析一行 <c>key = true|false</c>（可带行内注释）的布尔值；不是布尔字面量时返回 null。</summary>
    private static bool? ParseTomlBool(string line)
    {
        var equals = line.IndexOf('=');
        if (equals < 0) return null;
        var value = line[(equals + 1)..].Trim();
        if (value.StartsWith("true", StringComparison.Ordinal)) return true;
        if (value.StartsWith("false", StringComparison.Ordinal)) return false;
        return null;
    }
}
