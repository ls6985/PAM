using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PAM.Core;

/// <summary>预览中的一段文字及其颜色（null = 跟随默认前景色）。</summary>
public sealed record PromptPreviewPart(string Text, string? Color);

/// <summary>美化页读取到的提示符状态：<c>Format</c> 是一整行 Starship format 语法。</summary>
public sealed record StarshipPromptState(
    string ConfigPath,
    bool ConfigExists,
    bool Customized,
    bool HasBackup,
    string Format);

/// <summary>应用自定义提示符的结果。</summary>
public sealed record StarshipPromptApplyResult(bool ConfigWritten, bool BackupCreated, string ConfigPath);

/// <summary>美化页下拉用的预设色板。前三个正好是用户手工配置里已有的配色。</summary>
public static class PromptPalette
{
    public static IReadOnlyList<string> Colors { get; } =
    [
        "#BBC4B9", "#B6D6F2", "#BFA8BF", "#FFFFFF", "#F8F8F2", "#9CA3AF",
        "#61AFEF", "#56B6C2", "#98C379", "#E5C07B", "#C678DD", "#E06C75",
    ];
}

/// <summary>
/// 自定义 Starship 提示符：用户在美化页编辑**一整行** Starship <c>format</c> 语法
/// （例如 <c>[$username](fg:#BBC4B9)[$hostname](fg:#BBC4B9)…</c>），本服务负责把它写进
/// starship.toml 的自有标记块，并提供下拉变量清单、色板、预览解析与备份恢复。
///
/// <para>为什么用变量而不是固定文字：用户名、主机名、当前路径是 Starship 运行时读取的值，
/// 写死成 Black / ~ 会导致换个目录提示符还显示 ~。变量清单见 <see cref="StarshipVariables" />。
/// 固定装饰符（@ : $ 空格）仍可当纯文字写进 format，也可以在编辑器里直接改颜色。</para>
///
/// <para>为什么不整段覆盖 / 不引入 TOML 解析器：用户已有的 starship.toml 里还有
/// <c>add_newline</c>、注释以及 <c>[username]</c>/<c>[hostname]</c>/<c>[directory]</c>/
/// <c>[character]</c> 等段落，且顶层 <c>format</c> 是 <c>"""\</c> 多行字符串、
/// 段落内又有同名局部 format。本服务只做「移除顶层 format → 在原位置插入自有标记块」这一处最小改动，
/// 其余所有行原样保留。这些段内 format 自带 @ / " : " / 空格等装饰，所以默认 format 引用四个
/// 变量就能继续渲染出 Black@DESKTOP-J64G5VA :~ $，且路径会随 cd 变化。</para>
///
/// <para>备份用独立的 <c>starship.toml.pam-prompt.bak</c> 且**只在首次写入时创建**，
/// 不与主题预设的 <c>starship.toml.bak</c> 抢同一个槽位，也不会被连续两次应用覆盖掉原始配置。
/// 配置文件本身即数据源，因此不需要在 settings.json 里再存一份。</para>
/// </summary>
public sealed class StarshipPromptService
{
    public const string StartMarker = "# >>> PAM Prompt >>>";
    public const string EndMarker = "# <<< PAM Prompt <<<";
    public const string NoticeLine = "# 该区块由 PowerShell 管理器自动维护，请勿手动修改标记行。";

    private const string BackupSuffix = ".pam-prompt.bak";
    private const int MaxFormatLength = 4096;

    // 预览用：匹配 [内容](样式)，样式可能是 fg:#RRGGBB、$style、bold 等。
    private static readonly Regex BlockRegex = new(
        @"\[((?:\\.|[^\[\]\\])*)\]\(([^()]*)\)",
        RegexOptions.Compiled);

    private static readonly Regex HexColorRegex = new(@"#[0-9A-Fa-f]{6}", RegexOptions.Compiled);
    private static readonly Regex FgColorRegex = new(@"fg:([^)\s]+)", RegexOptions.Compiled);
    private static readonly Regex HexValidation = new(@"^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    private readonly string? _configPathOverride;

    public StarshipPromptService()
    {
    }

    /// <summary>测试注入专用：让 starship.toml 的读写落在指定目录，不碰真实用户配置。</summary>
    public StarshipPromptService(string configPathOverride) => _configPathOverride = configPathOverride;

    public string GetConfigPath() => _configPathOverride ?? new StarshipService().GetConfigPath();

    public string GetBackupPath() => GetConfigPath() + BackupSuffix;

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

    /// <summary>把一行 format 写入 starship.toml（只替换顶层 format，其他内容原样保留）。</summary>
    public StarshipPromptApplyResult Apply(string format)
    {
        Validate(format);

        var path = GetConfigPath();
        var exists = File.Exists(path);
        var content = exists ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;

        // 只在首次写入时备份：连续应用不会把「用户原始配置」覆盖成上一次结果。
        var backupCreated = false;
        if (exists && !File.Exists(GetBackupPath()))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(path, GetBackupPath(), overwrite: false);
            backupCreated = true;
        }

        var newLine = content.Contains("\r\n", StringComparison.Ordinal)
            ? "\r\n"
            : content.Contains('\n') ? "\n" : Environment.NewLine;

        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        var (clean, insertIndex) = StripManagedParts(lines);

        clean.Insert(insertIndex, StartMarker);
        clean.Insert(insertIndex + 1, NoticeLine);
        clean.Insert(insertIndex + 2, BuildFormatLine(format));
        clean.Insert(insertIndex + 3, EndMarker);

        var output = string.Join(newLine, clean);
        if (!output.EndsWith(newLine, StringComparison.Ordinal)) output += newLine;

        // TOML 规范不建议带 BOM；用临时文件 + Move 原子替换，避免写一半被 Starship 读到。
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tempPath, output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch (IOException) { }
            }
        }

        return new StarshipPromptApplyResult(true, backupCreated, path);
    }

    /// <summary>恢复首次应用前的原始 starship.toml；没有备份时返回 false。</summary>
    public bool Restore()
    {
        var backupPath = GetBackupPath();
        if (!File.Exists(backupPath)) return false;

        var path = GetConfigPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Copy(backupPath, path, overwrite: true);
        return true;
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

    /// <summary>把一行 format 拆成预览片段：变量换成示例值、样式里的 fg:#RRGGBB 取出来当颜色。</summary>
    public static IReadOnlyList<PromptPreviewPart> ParsePreview(string? format)
    {
        if (string.IsNullOrEmpty(format)) return [];

        var parts = new List<PromptPreviewPart>();
        var index = 0;
        foreach (Match match in BlockRegex.Matches(format))
        {
            if (match.Index > index)
            {
                parts.Add(new PromptPreviewPart(UnescapeStarship(format[index..match.Index]), null));
            }

            var inner = UnescapeStarship(match.Groups[1].Value);
            var text = StarshipVariables.IsVariable(inner) ? StarshipVariables.SampleOf(inner) : inner;
            var hex = HexColorRegex.Match(match.Groups[2].Value).Value;
            parts.Add(new PromptPreviewPart(text, hex.Length == 0 ? null : hex.ToUpperInvariant()));
            index = match.Index + match.Length;
        }

        if (index < format.Length)
        {
            parts.Add(new PromptPreviewPart(UnescapeStarship(format[index..]), null));
        }

        return parts;
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

    /// <summary>移除旧的 PAM 块和旧的顶层 format（含 """\ 多行字符串），返回插入位置。</summary>
    private static (List<string> Lines, int InsertIndex) StripManagedParts(List<string> lines)
    {
        var result = new List<string>(lines.Count);
        var inBlock = false;
        var topLevel = true;
        var blockIndex = -1;
        var formatIndex = -1;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            if (trimmed.Equals(StartMarker, StringComparison.OrdinalIgnoreCase))
            {
                inBlock = true;
                if (blockIndex < 0) blockIndex = result.Count;
                continue;
            }

            if (inBlock)
            {
                if (trimmed.Equals(EndMarker, StringComparison.OrdinalIgnoreCase)) inBlock = false;
                continue;
            }

            // 只认任何 [section] 之前（= 顶层）的 format，段落内的同名 format 不动。
            if (formatIndex < 0 && topLevel && IsFormatAssignment(trimmed))
            {
                formatIndex = result.Count;
                var equals = trimmed.IndexOf('=');
                var value = trimmed[(equals + 1)..].TrimStart();
                if (value.StartsWith("\"\"\"", StringComparison.Ordinal)
                    && !value[3..].Contains("\"\"\"", StringComparison.Ordinal))
                {
                    // 多行基本字符串：一直吃到含结束 """ 的那一行。
                    while (i + 1 < lines.Count && !lines[i + 1].Contains("\"\"\"", StringComparison.Ordinal)) i++;
                    if (i + 1 < lines.Count) i++;
                }

                continue;
            }

            if (trimmed.StartsWith('[') && trimmed.Contains(']')) topLevel = false;
            result.Add(line);
        }

        var insertIndex = blockIndex >= 0 ? Math.Min(blockIndex, result.Count)
            : formatIndex >= 0 ? formatIndex
            : 0;
        return (result, insertIndex);
    }

    private static bool IsFormatAssignment(string trimmed)
    {
        var equals = trimmed.IndexOf('=');
        if (equals <= 0) return false;
        return trimmed[..equals].Trim().Equals("format", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>只做 TOML 双引号字符串层转义；format 里的 Starship 语法（\[ \] \$ \\）原样保留。</summary>
    private static string BuildFormatLine(string format) => "format = \"" + EncodeToml(format) + "\"";

    private static string EncodeToml(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': builder.Append(@"\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default: builder.Append(c); break;
            }
        }

        return builder.ToString();
    }

    private static string DecodeTomlString(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 >= value.Length)
            {
                builder.Append(value[i]);
                continue;
            }

            var next = value[++i];
            builder.Append(next switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                '"' => '"',
                '\\' => '\\',
                _ => next,
            });
        }

        return builder.ToString();
    }

    /// <summary>Starship format 里的 \[ \] \$ \\ 反转义，只用于预览显示。</summary>
    private static string UnescapeStarship(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 >= value.Length)
            {
                builder.Append(value[i]);
                continue;
            }

            var next = value[++i];
            builder.Append(next switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => next,
            });
        }

        return builder.ToString();
    }

    private static void Validate(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            throw new ArgumentException("提示符 format 不能为空。", nameof(format));
        }

        if (format.Contains('\r') || format.Contains('\n'))
        {
            throw new ArgumentException("提示符 format 必须是一行。", nameof(format));
        }

        if (format.Length > MaxFormatLength)
        {
            throw new ArgumentException(
                string.Format(CultureInfo.InvariantCulture, "提示符 format 不能超过 {0} 个字符。", MaxFormatLength),
                nameof(format));
        }

        foreach (Match match in FgColorRegex.Matches(format))
        {
            if (!HexValidation.IsMatch(match.Groups[1].Value))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, "颜色 {0} 不是 #RRGGBB 格式。", match.Groups[1].Value),
                    nameof(format));
            }
        }
    }
}
