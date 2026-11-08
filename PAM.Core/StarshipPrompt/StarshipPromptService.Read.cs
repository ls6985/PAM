using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PAM.Core;

public sealed partial class StarshipPromptService
{
    /// <summary>读取当前状态。没有 PAM 标记块时按现有配置的配色生成一行默认的变量式 format。</summary>
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
            parsed ?? CreateDefaultFormat(content));
    }

    /// <summary>
    /// 默认 format：四个动态变量。颜色优先复用现有 starship.toml 里出现过的 fg:#RRGGBB
    /// （去重按出现顺序），这样第一次点「应用」也不会把用户手工配的配色冲掉。
    /// 实测在用户现有配置上渲染结果仍是 Black@DESKTOP-J64G5VA :~ $，且路径随 cd 变化。
    /// </summary>
    public static string CreateDefaultFormat(string? existingContent = null)
    {
        var colors = new List<string>();
        if (!string.IsNullOrEmpty(existingContent))
        {
            foreach (Match match in HexColorRegex.Matches(existingContent))
            {
                var hex = match.Value.ToUpperInvariant();
                if (!colors.Contains(hex, StringComparer.Ordinal)) colors.Add(hex);
            }
        }

        string Pick(int index, string fallback) => index < colors.Count ? colors[index] : fallback;
        var primary = Pick(0, "#BBC4B9");
        var path = Pick(1, "#B6D6F2");
        var tail = Pick(2, "#BFA8BF");

        return $"[$username](fg:{primary})[$hostname](fg:{primary})"
            + $"[$directory](fg:{path})[$character](fg:{tail})";
    }

    /// <summary>从自有标记块里读回一行 Starship format；块不存在或格式不符时返回 null。</summary>
    private static string? TryParse(string content)
    {
        if (string.IsNullOrEmpty(content)) return null;

        var inside = false;
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
            if (IsFormatAssignment(line)) formatLine = line;
        }

        if (formatLine is null) return null;

        var equals = formatLine.IndexOf('=');
        var rawValue = formatLine[(equals + 1)..].Trim();
        if (rawValue.Length < 2 || rawValue[0] != '"' || rawValue[^1] != '"') return null;

        return DecodeTomlString(rawValue[1..^1]);
    }
}
