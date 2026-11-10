using System.IO;
using System.Text;
using System.Text.Json;

namespace PAM.Core;

/// <summary>Windows Terminal settings.json 与字体的当前状态。</summary>
public sealed record WindowsTerminalState(
    string SettingsPath,
    bool SettingsExists,
    string? CurrentFont,
    bool BackupExists,
    string BackupPath = "",
    DateTime? BackupCreatedAt = null);

/// <summary>写字体操作的结果。</summary>
public sealed record WindowsTerminalFontResult(bool Success, string Message);

/// <summary>
/// Windows Terminal 的 settings.json 读写服务：定位配置文件、读取/写入
/// <c>profiles.defaults</c> 下的外观属性（字体、光标、背景图、透明度等）。
/// <para>
/// settings.json 是允许注释与尾逗号的 JSONC，不能走"反序列化 → 再序列化"（会丢注释），
/// 因此采用文本拼接：用 JsonDocument 判断结构、在原文上做最小改动，
/// 改完重新解析校验通过才落盘。结构不符合预期时报错且不写，绝不破坏用户配置。
/// </para>
/// <para>
/// 备份语义：<c>settings.json.bak</c> 是「本软件第一次修改终端配置之前」的原始快照，
/// <b>只在首次写入时创建，之后无论改多少次都不覆盖</b>；恢复也保留备份，可反复恢复。
/// 所以「恢复 Windows Terminal 备份」永远回到安装本软件前的配置，而不是上一次修改前的配置。
/// </para>
/// </summary>
public sealed partial class WindowsTerminalService
{
    private const string BackupSuffix = ".bak";
    private const string SettingsFileName = "settings.json";

    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>按优先级列出候选配置路径：Stable 包 → Preview 包 → Canary 包 → 非打包安装。</summary>
    public IReadOnlyList<string> GetCandidateSettingsPaths()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return
        [
            Path.Combine(localAppData, "Packages", "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "LocalState", SettingsFileName),
            Path.Combine(localAppData, "Packages", "Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe", "LocalState", SettingsFileName),
            Path.Combine(localAppData, "Packages", "Microsoft.WindowsTerminalCanary_8wekyb3d8bbwe", "LocalState", SettingsFileName),
            Path.Combine(localAppData, "Microsoft", "Windows Terminal", SettingsFileName),
        ];
    }

    /// <summary>返回第一个实际存在的 settings.json；都不存在时返回 null（界面据此禁用应用按钮）。</summary>
    public string? DiscoverSettingsPath()
        => GetCandidateSettingsPaths().FirstOrDefault(File.Exists);

    /// <summary>当前状态：路径、是否存在、defaults 里配置的字体、原始备份的路径与时间。
    /// <paramref name="settingsPath" /> 缺省时自动发现，测试可直接注入路径。</summary>
    public WindowsTerminalState GetState(string? settingsPath = null)
    {
        var path = settingsPath ?? DiscoverSettingsPath();
        if (path is null)
        {
            // 一个都不存在时用 Stable 的预期路径做展示，避免界面出现空路径。
            path = GetCandidateSettingsPaths()[0];
        }

        var backupPath = GetBackupPath(path);
        return new WindowsTerminalState(
            path,
            File.Exists(path),
            ReadFontFace(path),
            File.Exists(backupPath),
            backupPath,
            File.Exists(backupPath) ? File.GetLastWriteTime(backupPath) : null);
    }

    /// <summary>原始快照的存放位置：与 settings.json 同目录的 settings.json.bak。</summary>
    public string GetBackupPath(string settingsPath) => settingsPath + BackupSuffix;

    /// <summary>
    /// 只在备份不存在时保存当前配置。任何写入（字体 / 外观）都必须先调用它，
    /// 之后重复写入一律不覆盖，保证 .bak 始终是「本软件第一次修改前」的原始配置。
    /// 返回是否新建了备份。
    /// </summary>
    private bool EnsureOriginalBackup(string settingsPath)
    {
        var backupPath = GetBackupPath(settingsPath);
        if (File.Exists(backupPath) || !File.Exists(settingsPath))
        {
            return false;
        }

        File.Copy(settingsPath, backupPath, overwrite: false);
        return true;
    }

    /// <summary>读取 profiles.defaults.font.face；结构缺失或文件不可解析时返回 null。</summary>
    public string? ReadFontFace(string settingsPath)
    {
        if (string.IsNullOrWhiteSpace(settingsPath) || !File.Exists(settingsPath))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath), ParseOptions);
            return TryGetFontFace(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>把字体写入 profiles.defaults.font.face：先备份，再文本拼接，校验通过后原子落盘。
    /// <paramref name="settingsPath" /> 缺省时自动发现，测试可直接注入路径。</summary>
    public WindowsTerminalFontResult ApplyFont(string face, string? settingsPath = null)
    {
        if (string.IsNullOrWhiteSpace(face))
        {
            return new WindowsTerminalFontResult(false, "字体名称不能为空。");
        }

        settingsPath ??= DiscoverSettingsPath();
        if (settingsPath is null || !File.Exists(settingsPath))
        {
            return new WindowsTerminalFontResult(false, LocalizationService.Instance.T("未找到 Windows Terminal 的 settings.json，请先启动一次 Windows Terminal。"));
        }

        // 只在首次写入时留原始快照；后续修改不再覆盖，恢复永远是「安装本软件前」的配置。
        EnsureOriginalBackup(settingsPath);

        var originalBytes = File.ReadAllBytes(settingsPath);
        var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(
            StripBom(originalBytes, out var hasBom));
        var encoding = new UTF8Encoding(hasBom);

        string updatedText;
        try
        {
            updatedText = SpliceFontFace(text, face);
        }
        catch (NotSupportedException ex)
        {
            return new WindowsTerminalFontResult(false, ex.Message);
        }

        // 拼接结果必须仍是合法 JSONC，否则不落盘。
        try
        {
            using var verify = JsonDocument.Parse(updatedText, ParseOptions);
            if (TryGetFontFace(verify.RootElement) != face)
            {
                return new WindowsTerminalFontResult(false, "写入后校验未通过，已保持原配置不变。");
            }
        }
        catch (JsonException)
        {
            return new WindowsTerminalFontResult(false, "写入后校验未通过，已保持原配置不变。");
        }

        ProfileFileStore.Write(settingsPath, updatedText, encoding);
        return new WindowsTerminalFontResult(true, $"已把 Windows Terminal 默认字体设置为 {face}。");
    }

    /// <summary>
    /// 用原始快照恢复 settings.json，回到本软件第一次修改终端配置之前的状态。
    /// 没有备份时返回 false。<b>备份文件本身保留</b>：恢复之后继续改、再点恢复，
    /// 依然是同一份「安装本软件前」的配置。
    /// <paramref name="settingsPath" /> 缺省时自动发现，测试可直接注入路径。
    /// </summary>
    public bool RestoreBackup(string? settingsPath = null)
    {
        settingsPath ??= DiscoverSettingsPath();
        if (settingsPath is null)
        {
            return false;
        }

        var backupPath = GetBackupPath(settingsPath);
        if (!File.Exists(backupPath))
        {
            return false;
        }

        File.Copy(backupPath, settingsPath, overwrite: true);
        return true;
    }

    // --- 文本拼接 ---

    /// <summary>
    /// 在 JSONC 原文里把 profiles.defaults.font.face 设为指定值：
    /// 已有 face 就替换值，缺 font/defaults/profiles 就逐层补上。注释与尾逗号原样保留。
    /// </summary>
    internal static string SpliceFontFace(string text, string face)
    {
        using var document = JsonDocument.Parse(text, ParseOptions);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new NotSupportedException("settings.json 的顶层不是对象，无法自动修改。");
        }

        var encodedFace = JsonEncodedText.Encode(face).ToString();

        // 1. profiles 缺失：在顶层对象开头插入完整子树。
        if (!root.TryGetProperty("profiles", out var profiles))
        {
            return InsertIntoObject(text, $"\"profiles\": {{ \"defaults\": {{ \"font\": {{ \"face\": \"{encodedFace}\" }} }} }},");
        }

        if (profiles.ValueKind != JsonValueKind.Object)
        {
            throw new NotSupportedException("settings.json 的 profiles 不是对象（可能是旧版数组结构），无法自动修改，请手动调整。");
        }

        // 2. defaults 缺失：在 profiles 对象开头插入。
        if (!profiles.TryGetProperty("defaults", out var defaults))
        {
            var profilesRaw = profiles.GetRawText();
            var profilesStart = LocateRaw(text, profilesRaw, "profiles");
            var openBrace = text.IndexOf('{', profilesStart);
            if (openBrace < 0)
            {
                throw new NotSupportedException("settings.json 的 profiles 结构异常，无法自动修改。");
            }

            return InsertValueAfter(text, openBrace + 1, $"\"defaults\": {{ \"font\": {{ \"face\": \"{encodedFace}\" }} }},");
        }

        if (defaults.ValueKind != JsonValueKind.Object)
        {
            throw new NotSupportedException("settings.json 的 profiles.defaults 不是对象，无法自动修改。");
        }

        var defaultsRaw = defaults.GetRawText();
        var defaultsStart = LocateRaw(text, defaultsRaw, "profiles.defaults");

        // 3. font 缺失：在 defaults 对象开头插入。
        if (!defaults.TryGetProperty("font", out var font))
        {
            var openBrace = text.IndexOf('{', defaultsStart);
            if (openBrace < 0)
            {
                throw new NotSupportedException("settings.json 的 profiles.defaults 结构异常，无法自动修改。");
            }

            return InsertValueAfter(text, openBrace + 1, $"\"font\": {{ \"face\": \"{encodedFace}\" }},");
        }

        if (font.ValueKind != JsonValueKind.Object)
        {
            throw new NotSupportedException("profiles.defaults.font 不是对象，无法自动修改。");
        }

        // 4. font 里没有 face：在 font 对象开头插入。
        if (!font.TryGetProperty("face", out _))
        {
            var fontStart = LocateRaw(text, font.GetRawText(), "profiles.defaults.font", defaultsStart);
            var openBrace = text.IndexOf('{', fontStart);
            if (openBrace < 0)
            {
                throw new NotSupportedException("profiles.defaults.font 结构异常，无法自动修改。");
            }

            return InsertValueAfter(text, openBrace + 1, $"\"face\": \"{encodedFace}\",");
        }

        // 5. face 已存在：只替换 defaults 范围内的第一个 face 值，其他 profile 的同名属性不动。
        return ReplaceFontFaceInDefaults(text, defaultsStart, defaultsRaw.Length, encodedFace);
    }

    /// <summary>在原文中定位节点原文片段。注释等额外内容会让精确匹配失败，此时退回按关键名扫描。</summary>
    private static int LocateRaw(string text, string raw, string displayName, int searchFrom = 0)
    {
        var index = text.IndexOf(raw, searchFrom, StringComparison.Ordinal);
        if (index >= 0)
        {
            return index;
        }

        // GetRawText 遇到节点内混有注释时可能拿不到连续片段；退化为按属性名找位置。
        var keyIndex = text.IndexOf($"\"{displayName[(displayName.LastIndexOf('.') + 1)..]}\"", searchFrom, StringComparison.Ordinal);
        if (keyIndex < 0)
        {
            throw new NotSupportedException($"无法在 settings.json 中定位 {displayName}，请手动修改字体设置。");
        }

        return keyIndex;
    }

    /// <summary>
    /// 往对象的开括号后插入一个属性；对象为空（紧跟 }）时不留尾逗号。
    /// 原对象首成员前的缩进 / 换行原样保留，插入项沿用同样的分隔符，避免把配置压成一整行。
    /// </summary>
    private static string InsertValueAfter(string text, int insertIndex, string valueWithComma)
    {
        var rest = text[insertIndex..];
        var trimmed = rest.AsSpan().TrimStart();
        var skipWhitespace = rest.Length - trimmed.Length;
        var whitespace = rest[..skipWhitespace];

        // 已有成员时插入的属性以逗号结尾；空对象里插入时不带尾逗号。
        var insertion = trimmed.StartsWith('}') ? valueWithComma.TrimEnd(',') : valueWithComma;
        var separator = insertion.EndsWith(',')
            ? (whitespace.Contains('\n') ? whitespace : " ")
            : string.Empty;

        return text[..insertIndex] + whitespace + insertion + separator + rest[skipWhitespace..];
    }

    private static string InsertIntoObject(string text, string value)
    {
        var openBrace = text.IndexOf('{');
        if (openBrace < 0)
        {
            throw new NotSupportedException("settings.json 结构异常，无法自动修改。");
        }

        return InsertValueAfter(text, openBrace + 1, value);
    }

    /// <summary>替换 defaults 对象范围内第一个 "face": "..." 的值。</summary>
    private static string ReplaceFontFaceInDefaults(string text, int defaultsStart, int defaultsLength, string encodedFace)
    {
        var searchEnd = Math.Min(text.Length, defaultsStart + defaultsLength + 1);
        var span = text[defaultsStart..searchEnd];

        var faceMatch = System.Text.RegularExpressions.Regex.Match(
            span,
            "\"face\"\\s*:\\s*\"[^\"]*\"",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        if (!faceMatch.Success)
        {
            throw new NotSupportedException("无法定位 profiles.defaults.font.face，请手动修改字体设置。");
        }

        var replacement = $"\"face\": \"{encodedFace}\"";
        return text[..(defaultsStart + faceMatch.Index)] + replacement +
               text[(defaultsStart + faceMatch.Index + faceMatch.Length)..];
    }

    private static string? TryGetFontFace(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("profiles", out var profiles) ||
            profiles.ValueKind != JsonValueKind.Object ||
            !profiles.TryGetProperty("defaults", out var defaults) ||
            defaults.ValueKind != JsonValueKind.Object ||
            !defaults.TryGetProperty("font", out var font) ||
            font.ValueKind != JsonValueKind.Object ||
            !font.TryGetProperty("face", out var face) ||
            face.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return face.GetString();
    }

    private static byte[] StripBom(byte[] bytes, out bool hasBom)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            hasBom = true;
            return bytes[3..];
        }

        hasBom = false;
        return bytes;
    }
}
