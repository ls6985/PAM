using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PAM.Core;

/// <summary>
/// 外观设置的读写：把「字体 / 复古效果 / 光标 / 背景图 / 透明度」映射到
/// <c>profiles.defaults</c> 的属性，用通用 JSONC 文本拼接逐项写入或删除。
/// <para>只提交界面上有变化的项目；删除属性等于把该项交还给 Windows Terminal 默认值。</para>
/// </summary>
public sealed partial class WindowsTerminalService
{
    /// <summary>属性名与值之间允许空白与 // /* */ 注释。</summary>
    private const string Gap = @"(?:\s|//[^\r\n]*|/\*[\s\S]*?\*/)*";

    /// <summary>本服务只改简单标量；对象 / 数组属性一律拒绝覆盖，避免破坏用户结构。</summary>
    private const string Scalar = @"(?<value>""(?:\\.|[^""\\])*""|true|false|-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)";

    /// <summary>读取当前外观值；结构缺失、文件不存在或解析失败时全部返回 null（跟随 WT 默认）。</summary>
    public TerminalAppearance ReadAppearance(string? settingsPath = null)
    {
        var path = settingsPath ?? DiscoverSettingsPath();
        if (path is null || !File.Exists(path))
        {
            return TerminalAppearance.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path), ParseOptions);
            return ReadAppearanceFrom(document.RootElement);
        }
        catch (JsonException)
        {
            return TerminalAppearance.Empty;
        }
        catch (IOException)
        {
            return TerminalAppearance.Empty;
        }
    }

    /// <summary>
    /// 把一组变更写进 settings.json：首次写入前留原始快照 → 逐项文本拼接 → 整体解析 + 逐项回读校验 → 原子落盘。
    /// 任何一步不通过都保持原文件不变。
    /// </summary>
    public TerminalAppearanceResult ApplyAppearance(
        IReadOnlyList<TerminalAppearanceChange> changes,
        string? settingsPath = null)
    {
        if (changes is null || changes.Count == 0)
        {
            return new TerminalAppearanceResult(true, "没有需要保存的更改。");
        }

        foreach (var change in changes)
        {
            var error = TerminalAppearanceSchema.Validate(change);
            if (error is not null)
            {
                return new TerminalAppearanceResult(false, error);
            }
        }

        settingsPath ??= DiscoverSettingsPath();
        if (settingsPath is null || !File.Exists(settingsPath))
        {
            return new TerminalAppearanceResult(
                false,
                "未找到 Windows Terminal 的 settings.json，请先启动一次 Windows Terminal。");
        }

        EnsureOriginalBackup(settingsPath);

        var originalBytes = File.ReadAllBytes(settingsPath);
        var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(
            StripBom(originalBytes, out var hasBom));
        var encoding = new UTF8Encoding(hasBom);

        try
        {
            foreach (var change in changes)
            {
                text = SpliceProperty(text, TerminalAppearanceSchema.GetPath(change.Key), change.JsonValue);
            }
        }
        catch (NotSupportedException ex)
        {
            return new TerminalAppearanceResult(false, ex.Message);
        }

        try
        {
            using var verify = JsonDocument.Parse(text, ParseOptions);
            foreach (var change in changes)
            {
                if (!Matches(verify.RootElement, TerminalAppearanceSchema.GetPath(change.Key), change.JsonValue))
                {
                    return new TerminalAppearanceResult(false, "写入后校验未通过，已保持原配置不变。");
                }
            }
        }
        catch (JsonException)
        {
            return new TerminalAppearanceResult(false, "写入后校验未通过，已保持原配置不变。");
        }

        ProfileFileStore.Write(settingsPath, text, encoding);
        return new TerminalAppearanceResult(true, $"已保存 {changes.Count} 项终端外观设置，打开新终端窗口即可看到效果。");
    }

    private static TerminalAppearance ReadAppearanceFrom(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("profiles", out var profiles) ||
            profiles.ValueKind != JsonValueKind.Object ||
            !profiles.TryGetProperty("defaults", out var defaults) ||
            defaults.ValueKind != JsonValueKind.Object)
        {
            return TerminalAppearance.Empty;
        }

        string? fontFace = null;
        if (defaults.TryGetProperty("font", out var font) &&
            font.ValueKind == JsonValueKind.Object &&
            font.TryGetProperty("face", out var face) &&
            face.ValueKind == JsonValueKind.String)
        {
            fontFace = face.GetString();
        }

        return new TerminalAppearance(
            fontFace,
            Bool(defaults, "experimental.retroTerminalEffect"),
            String(defaults, "cursorShape"),
            String(defaults, "cursorColor"),
            String(defaults, "backgroundImage"),
            Double(defaults, "backgroundImageOpacity"),
            Int(defaults, "opacity"),
            Bool(defaults, "useAcrylic"));

        static string? String(JsonElement obj, string key)
            => obj.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        static bool? Bool(JsonElement obj, string key)
            => obj.TryGetProperty(key, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? value.GetBoolean()
                : null;

        static double? Double(JsonElement obj, string key)
            => obj.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number &&
               value.TryGetDouble(out var number)
                ? number
                : null;

        static int? Int(JsonElement obj, string key)
            => obj.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number &&
               value.TryGetInt32(out var number)
                ? number
                : null;
    }

    /// <summary>校验拼接结果：设置项要读回期望值，删除项要确实不存在。</summary>
    private static bool Matches(JsonElement root, IReadOnlyList<string> path, string? expectedJson)
    {
        var current = root;
        for (var i = 0; i < path.Count - 1; i++)
        {
            if (current.ValueKind != JsonValueKind.Object ||
                !current.TryGetProperty(path[i], out var child))
            {
                return expectedJson is null;
            }

            current = child;
        }

        if (current.ValueKind != JsonValueKind.Object ||
            !current.TryGetProperty(path[^1], out var value))
        {
            return expectedJson is null;
        }

        if (expectedJson is null)
        {
            return false;
        }

        using var document = JsonDocument.Parse(expectedJson);
        var expected = document.RootElement;
        if (value.ValueKind != expected.ValueKind)
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() == expected.GetString(),
            JsonValueKind.True or JsonValueKind.False => value.GetBoolean() == expected.GetBoolean(),
            JsonValueKind.Number => value.GetDouble() == expected.GetDouble(),
            _ => false,
        };
    }

    // --- 通用 JSONC 文本拼接 ---

    /// <summary>
    /// 在 JSONC 原文里把 <paramref name="path" /> 指向的属性设为 JSON 字面量（null = 删除）。
    /// 逐层补齐缺失的对象；只动目标属性，注释、尾逗号、其它 profile 的同名属性原样保留。
    /// 属性名带点（如 <c>experimental.retroTerminalEffect</c>）按单个键处理。
    /// </summary>
    internal static string SpliceProperty(string text, IReadOnlyList<string> path, string? jsonValue)
    {
        if (path.Count == 0)
        {
            throw new NotSupportedException("外观设置的属性路径为空。");
        }

        using var document = JsonDocument.Parse(text, ParseOptions);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new NotSupportedException("settings.json 的顶层不是对象，无法自动修改。");
        }

        var rootOpen = text.IndexOf('{');
        if (rootOpen < 0)
        {
            throw new NotSupportedException("settings.json 结构异常，无法自动修改。");
        }

        var parentStart = rootOpen;
        var parentEnd = FindMatchingBrace(text, rootOpen);
        var current = root;

        // 逐层走进父对象；某一层缺失时直接构造剩下的嵌套子树插入。
        for (var i = 0; i < path.Count - 1; i++)
        {
            var key = path[i];
            if (!TryFindChildObject(text, key, parentStart, parentEnd, out var childOpen, out var childClose))
            {
                return jsonValue is null
                    ? text
                    : InsertValueAfter(text, parentStart + 1, BuildNested(path, i, jsonValue) + ",");
            }

            if (current.ValueKind == JsonValueKind.Object &&
                current.TryGetProperty(key, out var childElement))
            {
                if (childElement.ValueKind != JsonValueKind.Object)
                {
                    throw new NotSupportedException("settings.json 的目标层级不是对象，无法自动修改，请手动调整。");
                }

                current = childElement;
            }

            parentStart = childOpen;
            parentEnd = childClose;
        }

        var propertyKey = path[^1];
        JsonElement existing = default;
        var existsAtLevel = current.ValueKind == JsonValueKind.Object &&
                            current.TryGetProperty(propertyKey, out existing);
        var expectedRaw = existsAtLevel ? existing.GetRawText() : null;

        // 只认「本层」的属性：用 JsonDocument 给的值原文核对，避免误改嵌套对象里的同名项。
        Match? target = null;
        if (existsAtLevel)
        {
            foreach (var match in FindProperties(text, propertyKey, parentStart, parentEnd))
            {
                if (match.Groups["value"].Value == expectedRaw)
                {
                    target = match;
                    break;
                }
            }

            if (target is null)
            {
                throw new NotSupportedException(
                    "settings.json 的目标属性不是简单值或结构异常，无法自动修改，请手动调整。");
            }
        }

        if (target is null)
        {
            return jsonValue is null
                ? text
                : InsertValueAfter(text, parentStart + 1, $"\"{propertyKey}\": {jsonValue},");
        }

        var valueGroup = target.Groups["value"];
        var valueStart = parentStart + valueGroup.Index;

        if (jsonValue is not null)
        {
            return text[..valueStart] + jsonValue + text[(valueStart + valueGroup.Length)..];
        }

        // 删除整条属性：优先连它后面的逗号一起删；若它是最后一项，则删掉前面那条逗号，
        // 避免留下 JSONC 之外的多余逗号。
        var removeStart = parentStart + target.Index;
        var removeEnd = removeStart + target.Length;
        if (!target.Groups["tail"].Success)
        {
            var before = text[..removeStart];
            var j = before.Length - 1;
            while (j >= 0 && char.IsWhiteSpace(before[j])) j--;
            if (j >= 0 && before[j] == ',')
            {
                // before 已经去掉了那个逗号；后面的 text 仍用原始下标，不能再减一。
                before = before.Remove(j, 1);
            }

            return before + text[removeEnd..];
        }

        return text[..removeStart] + text[removeEnd..];
    }

    /// <summary>构造缺失层级的紧凑子树，例如 "profiles": { "defaults": { "font": { "face": "X" } } }。</summary>
    private static string BuildNested(IReadOnlyList<string> path, int startIndex, string jsonValue)
    {
        var builder = new StringBuilder();
        for (var i = startIndex; i < path.Count - 1; i++)
        {
            builder.Append('"').Append(path[i]).Append("\": { ");
        }

        builder.Append('"').Append(path[^1]).Append("\": ").Append(jsonValue);
        for (var i = startIndex; i < path.Count - 1; i++)
        {
            builder.Append(" }");
        }

        return builder.ToString();
    }

    private static IEnumerable<Match> FindProperties(string text, string key, int start, int end)
    {
        if (end <= start)
        {
            yield break;
        }

        var segment = text[start..Math.Min(end, text.Length)];
        var pattern = "\"" + Regex.Escape(key) + "\"" + Gap + ":" + Gap + Scalar + @"(?<tail>\s*,)?";
        foreach (Match match in Regex.Matches(segment, pattern, RegexOptions.CultureInvariant))
        {
            yield return match;
        }
    }

    /// <summary>在父对象范围内找形如 "key": { 的子对象，返回其花括号范围。</summary>
    private static bool TryFindChildObject(
        string text,
        string key,
        int start,
        int end,
        out int childOpen,
        out int childClose)
    {
        childOpen = -1;
        childClose = -1;
        if (end <= start)
        {
            return false;
        }

        var segment = text[start..Math.Min(end, text.Length)];
        var pattern = "\"" + Regex.Escape(key) + "\"" + Gap + ":" + Gap + @"\{";
        var match = Regex.Match(segment, pattern, RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        childOpen = start + match.Index + match.Length - 1;
        childClose = FindMatchingBrace(text, childOpen);
        return true;
    }

    /// <summary>从开括号扫到配对的闭括号，跳过字符串与 // /* */ 注释里的花括号。</summary>
    private static int FindMatchingBrace(string text, int openBrace)
    {
        var depth = 0;
        for (var i = openBrace; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                i = SkipString(text, i);
                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                i = SkipLineComment(text, i);
                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i = SkipBlockComment(text, i);
                continue;
            }

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return Math.Max(openBrace, text.Length - 1);
    }

    private static int SkipString(string text, int quoteIndex)
    {
        for (var i = quoteIndex + 1; i < text.Length; i++)
        {
            if (text[i] == '\\')
            {
                i++;
                continue;
            }

            if (text[i] == '"')
            {
                return i;
            }
        }

        return text.Length - 1;
    }

    private static int SkipLineComment(string text, int slashIndex)
    {
        var newline = text.IndexOfAny(['\r', '\n'], slashIndex + 2);
        return newline < 0 ? text.Length - 1 : newline - 1;
    }

    private static int SkipBlockComment(string text, int slashIndex)
    {
        var close = text.IndexOf("*/", slashIndex + 2, StringComparison.Ordinal);
        return close < 0 ? text.Length - 1 : close + 1;
    }
}
