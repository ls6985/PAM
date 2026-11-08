using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace PAM.Core;

/// <summary>
/// 美化页「外观」卡片能改动的 Windows Terminal 设置项。
/// 全部落在 <c>profiles.defaults</c> 下（即 WT 设置里的「默认值 → 外观」），
/// 键名含点的 <c>experimental.retroTerminalEffect</c> 是 WT 的单个属性名，不是层级。
/// </summary>
public enum TerminalAppearanceKey
{
    FontFace,
    RetroTerminalEffect,
    CursorShape,
    CursorColor,
    BackgroundImage,
    BackgroundImageOpacity,
    Opacity,
    UseAcrylic,
}

/// <summary>一次外观写入：JsonValue 为 JSON 字面量，null 表示删除该属性、交还终端默认值。</summary>
public sealed class TerminalAppearanceChange
{
    private TerminalAppearanceChange(TerminalAppearanceKey key, string? jsonValue)
    {
        Key = key;
        JsonValue = jsonValue;
    }

    public TerminalAppearanceKey Key { get; }

    /// <summary>已编码好的 JSON 标量字面量；为 null 时删除属性。</summary>
    public string? JsonValue { get; }

    public static TerminalAppearanceChange SetString(TerminalAppearanceKey key, string value)
        => new(key, Quote(value));

    public static TerminalAppearanceChange SetBool(TerminalAppearanceKey key, bool value)
        => new(key, value ? "true" : "false");

    public static TerminalAppearanceChange SetNumber(TerminalAppearanceKey key, double value)
        => new(key, value.ToString("0.############", CultureInfo.InvariantCulture));

    /// <summary>删除属性，让该项回到 Windows Terminal 自己的默认值。</summary>
    public static TerminalAppearanceChange Remove(TerminalAppearanceKey key) => new(key, null);

    public static string Quote(string value)
        => "\"" + JsonEncodedText.Encode(value).ToString() + "\"";
}

/// <summary>从 settings.json 读回的外观当前值；null = 用户没有显式设置（跟随 WT 默认）。</summary>
public sealed record TerminalAppearance(
    string? FontFace,
    bool? RetroTerminalEffect,
    string? CursorShape,
    string? CursorColor,
    string? BackgroundImage,
    double? BackgroundImageOpacity,
    int? Opacity,
    bool? UseAcrylic)
{
    public static TerminalAppearance Empty { get; } = new(null, null, null, null, null, null, null, null);
}

/// <summary>外观写入结果。</summary>
public sealed record TerminalAppearanceResult(bool Success, string Message);

/// <summary>光标形状下拉项（Id 为空表示「终端默认」，即删除属性）。</summary>
public sealed record TerminalCursorShapeOption(string Id, string Name);

/// <summary>外观项到 settings.json 属性路径的映射，以及合法值校验，供服务与测试共用。</summary>
public static class TerminalAppearanceSchema
{
    public static readonly IReadOnlyList<TerminalCursorShapeOption> CursorShapes =
    [
        new("", "终端默认"),
        new("bar", "竖线 (bar)"),
        new("vintage", "复古粗块 (vintage)"),
        new("underscore", "下划线 (underscore)"),
        new("filledBox", "实心框 (filledBox)"),
        new("emptyBox", "空心框 (emptyBox)"),
        new("doubleUnderscore", "双下划线 (doubleUnderscore)"),
    ];

    private static readonly Dictionary<TerminalAppearanceKey, string[]> Paths = new()
    {
        [TerminalAppearanceKey.FontFace] = ["profiles", "defaults", "font", "face"],
        [TerminalAppearanceKey.RetroTerminalEffect] = ["profiles", "defaults", "experimental.retroTerminalEffect"],
        [TerminalAppearanceKey.CursorShape] = ["profiles", "defaults", "cursorShape"],
        [TerminalAppearanceKey.CursorColor] = ["profiles", "defaults", "cursorColor"],
        [TerminalAppearanceKey.BackgroundImage] = ["profiles", "defaults", "backgroundImage"],
        [TerminalAppearanceKey.BackgroundImageOpacity] = ["profiles", "defaults", "backgroundImageOpacity"],
        [TerminalAppearanceKey.Opacity] = ["profiles", "defaults", "opacity"],
        [TerminalAppearanceKey.UseAcrylic] = ["profiles", "defaults", "useAcrylic"],
    };

    public static string[] GetPath(TerminalAppearanceKey key) => Paths[key];

    /// <summary>写入前的合法性检查；返回 null 表示通过，否则给出可直接展示给用户的原因。</summary>
    public static string? Validate(TerminalAppearanceChange change)
    {
        if (!Paths.ContainsKey(change.Key))
        {
            return "未知的外观设置项。";
        }

        if (change.JsonValue is null)
        {
            return null;
        }

        JsonElement literal;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(change.JsonValue);
            literal = document.RootElement;
        }
        catch (JsonException)
        {
            return "外观设置的值不是合法的 JSON 字面量。";
        }

        using (document)
        {
            switch (change.Key)
            {
                case TerminalAppearanceKey.FontFace:
                    return literal.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(literal.GetString())
                        ? null
                        : "字体名称不能为空。";

                case TerminalAppearanceKey.RetroTerminalEffect:
                case TerminalAppearanceKey.UseAcrylic:
                    return literal.ValueKind is JsonValueKind.True or JsonValueKind.False
                        ? null
                        : "开关值只能是 true / false。";

                case TerminalAppearanceKey.CursorShape:
                    if (literal.ValueKind != JsonValueKind.String)
                    {
                        return "光标形状不合法。";
                    }

                    return System.Linq.Enumerable.Any(CursorShapes, option =>
                        option.Id.Length > 0 && option.Id == literal.GetString())
                        ? null
                        : "不支持的光标形状。";

                case TerminalAppearanceKey.CursorColor:
                    return literal.ValueKind == JsonValueKind.String &&
                           System.Text.RegularExpressions.Regex.IsMatch(
                               literal.GetString() ?? string.Empty,
                               "^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")
                        ? null
                        : "光标颜色要写成 #RGB 或 #RRGGBB。";

                case TerminalAppearanceKey.BackgroundImage:
                    return literal.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(literal.GetString())
                        ? null
                        : "背景图路径不能为空。";

                case TerminalAppearanceKey.BackgroundImageOpacity:
                    return literal.ValueKind == JsonValueKind.Number &&
                           literal.GetDouble() is >= 0 and <= 1
                        ? null
                        : "背景图透明度必须在 0 ~ 1 之间。";

                case TerminalAppearanceKey.Opacity:
                    return literal.ValueKind == JsonValueKind.Number &&
                           literal.GetDouble() is >= 0 and <= 100 &&
                           literal.GetDouble() == System.Math.Floor(literal.GetDouble())
                        ? null
                        : "窗口透明度必须是 0 ~ 100 的整数。";

                default:
                    return "未知的外观设置项。";
            }
        }
    }
}
