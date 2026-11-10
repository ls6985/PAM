using System.IO;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PAM.Core;

/// <summary>
/// 外观设置的读写：把「字体 / 复古效果 / 光标 / 背景图 / 透明度」映射到
/// <c>profiles.defaults</c> 的属性，用通用 JSONC 文本拼接逐项写入或删除。
/// <para>只提交界面上有变化的项目；删除属性等于把该项交还给 Windows Terminal 默认值。</para>
/// </summary>
public sealed partial class WindowsTerminalService
{
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
        catch (System.Text.Json.JsonException)
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
                LocalizationService.Instance.T("未找到 Windows Terminal 的 settings.json，请先启动一次 Windows Terminal。"));
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
        catch (System.Text.Json.JsonException)
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
    /// 结构由 Newtonsoft 解析定位，再只替换目标片段，注释、缩进、其它 profile 的同名属性原样保留。
    /// 属性名带点（如 <c>experimental.retroTerminalEffect</c>）按单个键处理。
    /// </summary>
    internal static string SpliceProperty(string text, IReadOnlyList<string> path, string? jsonValue)
    {
        if (path.Count == 0)
        {
            throw new NotSupportedException("外观设置的属性路径为空。");
        }

        var lineStarts = BuildLineStarts(text);
        var root = LoadSettings(text);

        // 沿路径下钻到父容器；记录第一个缺失的层级（用于补全嵌套）。
        // Newtonsoft 的 JObject 索引即结构导航，注释会被自动忽略。
        var parent = root;
        var missingIndex = -1;
        for (var i = 0; i < path.Count - 1; i++)
        {
            var child = parent[path[i]];
            if (child is JObject next)
            {
                parent = next;
                continue;
            }

            if (child is null)
            {
                missingIndex = i;
                break;
            }

            throw new NotSupportedException("settings.json 的目标层级不是对象，无法自动修改，请手动调整。");
        }

        // 目标属性不存在：在最近存在的祖先里补全缺失层级或插入属性。
        if (missingIndex >= 0 || parent.Property(path[^1]) is null)
        {
            if (jsonValue is null)
            {
                return text; // 删除不存在的属性：保持原文件不变。
            }

            var buildStart = missingIndex < 0 ? path.Count - 1 : missingIndex;
            var braceOff = OffsetOf(lineStarts, (IJsonLineInfo)parent);
            return InsertValueAfter(text, braceOff + 1, BuildNested(path, buildStart, jsonValue) + ",");
        }

        // 目标属性存在：Newtonsoft 给出的 JProperty 行号信息指向键后的冒号，
        // 据此在原文里做局部扫描，精确取得键 / 值的边界，只替换目标片段。
        var prop = parent.Property(path[^1])!;
        var keyStart = ScanKeyStart(text, OffsetOf(lineStarts, (IJsonLineInfo)prop), prop.Name);
        var valueStart = ScanValueStart(text, keyStart, prop.Name.Length);
        var valueEnd = ScanValueEnd(text, valueStart);

        if (jsonValue is not null)
        {
            return text[..valueStart] + jsonValue + text[valueEnd..];
        }

        // 删除整条属性：优先连它后面的逗号一起删；若它是最后一项，则删掉前面那条逗号，
        // 避免留下 JSONC 之外的多余逗号。
        var removeStart = keyStart;
        var after = text[valueEnd..];
        var k = 0;
        while (k < after.Length && char.IsWhiteSpace(after[k])) k++;
        if (k < after.Length && after[k] == ',')
        {
            return text[..removeStart] + text[(valueEnd + k + 1)..];
        }

        var before = text[..removeStart];
        var bi = before.Length - 1;
        while (bi >= 0 && char.IsWhiteSpace(before[bi])) bi--;
        if (bi >= 0 && before[bi] == ',')
        {
            before = before.Remove(bi, 1);
        }

        return before + text[valueEnd..];
    }

    private static JObject LoadSettings(string text)
    {
        try
        {
            using var reader = new JsonTextReader(new StringReader(text));
            reader.DateParseHandling = DateParseHandling.None;
            return JObject.Load(
                reader,
                new JsonLoadSettings
                {
                    CommentHandling = CommentHandling.Ignore,
                    LineInfoHandling = LineInfoHandling.Load,
                });
        }
        catch (JsonReaderException)
        {
            throw new NotSupportedException("settings.json 结构异常，无法自动修改。");
        }
    }

    /// <summary>
    /// 从属性行号信息偏移（指向键后的冒号）向前回扫，定位键名起始的引号。
    /// 键与冒号之间允许出现空白与块注释。
    /// </summary>
    private static int ScanKeyStart(string text, int propOff, string name)
    {
        var i = propOff - 1;
        while (i >= 0 && (text[i] == ':' || char.IsWhiteSpace(text[i]))) i--;
        if (i >= 1 && text[i] == '*' && text[i - 1] == '/')
        {
            var open = text.LastIndexOf("/*", i - 1, StringComparison.Ordinal);
            i = open < 0 ? 0 : open - 1;
            while (i >= 0 && char.IsWhiteSpace(text[i])) i--;
        }

        // text[i] 现在是键名的闭合引号；键起始引号在其左侧 name.Length + 1 处。
        return i - (name.Length + 1);
    }

    /// <summary>
    /// 从键名闭合引号之后扫过冒号、空白与注释，定位值的真正起点。
    /// </summary>
    private static int ScanValueStart(string text, int keyStart, int nameLength)
    {
        var p = keyStart + nameLength + 2; // 越过闭合引号
        while (p < text.Length)
        {
            var c = text[p];
            if (c == ':') { p++; continue; }
            if (char.IsWhiteSpace(c)) { p++; continue; }
            if (c == '/' && p + 1 < text.Length && text[p + 1] == '/')
            {
                var nl = text.IndexOf('\n', p + 2);
                p = nl < 0 ? text.Length : nl + 1;
                continue;
            }

            if (c == '/' && p + 1 < text.Length && text[p + 1] == '*')
            {
                var close = text.IndexOf("*/", p + 2, StringComparison.Ordinal);
                p = close < 0 ? text.Length : close + 2;
                continue;
            }

            break;
        }

        return p;
    }

    /// <summary>
    /// 返回从 <paramref name="start" /> 起始的 JSON 标量 / 字符串值的结束偏移（不含）。
    /// 受管的外观属性只接受简单值，遇到对象 / 数组一律交给用户手动处理。
    /// </summary>
    private static int ScanValueEnd(string text, int start)
    {
        var c = text[start];
        if (c == '"')
        {
            for (var i = start + 1; i < text.Length; i++)
            {
                if (text[i] == '\\') { i++; continue; }
                if (text[i] == '"') return i + 1;
            }

            return text.Length;
        }

        if (c == '{' || c == '[')
        {
            throw new NotSupportedException("settings.json 的目标属性不是简单值，无法自动修改，请手动调整。");
        }

        var j = start;
        while (j < text.Length)
        {
            var ch = text[j];
            if (ch == ',' || ch == '}' || ch == ']'
                || char.IsWhiteSpace(ch)
                || ch == '/' && j + 1 < text.Length && (text[j + 1] == '/' || text[j + 1] == '*'))
            {
                break;
            }

            j++;
        }

        return j;
    }

    private static int[] BuildLineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n') starts.Add(i + 1);
        }

        return starts.ToArray();
    }

    private static int OffsetOf(int[] lineStarts, IJsonLineInfo info)
        => lineStarts[info.LineNumber - 1] + (info.LinePosition - 1);

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

}
