using System.Text;

namespace PAM.Core;

public sealed partial class StarshipService
{
    /// <summary>通用选项行的标记；主题识别比对预设原文时忽略带此标记的行。</summary>
    private const string CommonOptionsMarker = "# PAM-COMMON-OPTIONS";

    private readonly record struct TomlLine(string Text, string Section, bool InMultiLineString);

    /// <summary>默认值与自定义提示符保持一致：提示符前不空一行、路径分隔符用 /。</summary>
    public static (bool AddNewline, bool UseForwardSlash) CreateDefaultCommonOptions()
        => StarshipPromptService.CreateDefaultOptions();

    /// <summary>
    /// 把两个通用选项写进主题预设文本：根级 add_newline 与 [directory] 段的 use_os_path_sep。
    /// 已存在的同名键先移除，避免 TOML 重复键；预设没有 [directory] 段时在末尾补一个。
    /// 新增的每一行都带 <see cref="CommonOptionsMarker" />，主题识别可以据此还原预设原文。
    /// </summary>
    public static string ApplyCommonOptions(string content, bool addNewline, bool useForwardSlash)
    {
        var newLine = content.Contains("\r\n", StringComparison.Ordinal)
            ? "\r\n"
            : content.Contains('\n') ? "\n" : Environment.NewLine;

        var pathSeparator = "use_os_path_sep = " + TomlBool(!useForwardSlash) + " " + CommonOptionsMarker;
        var result = new List<string>();
        var hasDirectory = false;

        foreach (var line in Scan(content))
        {
            var trimmed = line.Text.TrimStart();
            if (!line.InMultiLineString)
            {
                if (line.Section.Length == 0 && IsKeyAssignment(trimmed, "add_newline")) continue;
                if (line.Section == "directory" && IsKeyAssignment(trimmed, "use_os_path_sep")) continue;
                if (line.Text.Contains(CommonOptionsMarker, StringComparison.Ordinal)) continue;
            }

            result.Add(line.Text);
            if (!line.InMultiLineString && IsHeader(trimmed, out var header) && header == "directory")
            {
                hasDirectory = true;
                result.Add(pathSeparator);
            }
        }

        if (!hasDirectory)
        {
            // 表头本身也带标记：比对预设原文时这两行会被整体移除。
            result.Add("[directory] " + CommonOptionsMarker);
            result.Add(pathSeparator);
        }

        result.Insert(0, "add_newline = " + TomlBool(addNewline) + " " + CommonOptionsMarker);

        var output = string.Join(newLine, result);
        if (!output.EndsWith(newLine, StringComparison.Ordinal)) output += newLine;
        return output;
    }

    /// <summary>
    /// 从当前 starship.toml 读回通用选项。主题预设与自定义提示符的托管块都按同一套键解析；
    /// 键不存在时返回程序默认值。
    /// </summary>
    public (bool AddNewline, bool UseForwardSlash) ReadCommonOptions()
    {
        var (addNewline, useForwardSlash) = CreateDefaultCommonOptions();
        var path = GetConfigPath();
        if (!File.Exists(path)) return (addNewline, useForwardSlash);

        bool? rootAddNewline = null;
        bool? useOsPathSep = null;
        foreach (var line in Scan(File.ReadAllText(path, Encoding.UTF8)))
        {
            if (line.InMultiLineString) continue;
            var trimmed = line.Text.TrimStart();
            if (rootAddNewline is null && line.Section.Length == 0 && IsKeyAssignment(trimmed, "add_newline"))
            {
                rootAddNewline = ParseTomlBool(trimmed);
            }

            if (useOsPathSep is null && line.Section == "directory" && IsKeyAssignment(trimmed, "use_os_path_sep"))
            {
                useOsPathSep = ParseTomlBool(trimmed);
            }
        }

        return (
            rootAddNewline ?? addNewline,
            useOsPathSep is null ? useForwardSlash : useOsPathSep == false);
    }

    /// <summary>移除带通用选项标记的行并归一化换行，供主题识别还原预设原文。</summary>
    public static string StripCommonOptions(string content)
    {
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n')
            .Where(static line => !line.Contains(CommonOptionsMarker, StringComparison.Ordinal));
        return string.Join("\n", lines).TrimEnd();
    }

    /// <summary>
    /// 逐行扫描出所在段落与是否处于多行字符串内部。主题预设的顶层 format 是三引号多行字符串，
    /// 其中的 "[…]" 是提示符语法而非 TOML 表头，必须跳过。
    /// </summary>
    private static List<TomlLine> Scan(string content)
    {
        var raw = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var result = new List<TomlLine>(raw.Length);
        var inMultiLine = false;
        var section = string.Empty;

        foreach (var text in raw)
        {
            var startedInside = inMultiLine;
            var position = 0;
            while ((position = text.IndexOf("\"\"\"", position, StringComparison.Ordinal)) >= 0)
            {
                inMultiLine = !inMultiLine;
                position += 3;
            }

            var trimmed = text.TrimStart();
            if (!startedInside && IsHeader(trimmed, out var header)) section = header;
            result.Add(new TomlLine(text, section, startedInside));
        }

        return result;
    }

    private static bool IsHeader(string trimmed, out string name)
    {
        name = string.Empty;
        if (!trimmed.StartsWith('[')) return false;
        var end = trimmed.IndexOf(']');
        if (end <= 1) return false;

        name = trimmed[1..end].Trim();
        return name.Length > 0;
    }

    private static bool IsKeyAssignment(string trimmed, string key)
    {
        var equals = trimmed.IndexOf('=');
        return equals > 0 && trimmed[..equals].Trim().Equals(key, StringComparison.OrdinalIgnoreCase);
    }

    private static bool? ParseTomlBool(string trimmed)
    {
        var equals = trimmed.IndexOf('=');
        if (equals < 0) return null;

        var value = trimmed[(equals + 1)..].Trim();
        if (value.StartsWith("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (value.StartsWith("false", StringComparison.OrdinalIgnoreCase)) return false;
        return null;
    }

    private static string TomlBool(bool value) => value ? "true" : "false";
}
