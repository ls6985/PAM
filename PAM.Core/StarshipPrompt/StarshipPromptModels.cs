namespace PAM.Core;

/// <summary>预览中的一段文字及其颜色（null = 跟随默认前景色）。</summary>
public sealed record PromptPreviewPart(string Text, string? Color);

/// <summary>一个提示符片段的类别：动态变量（如 $username）、固定文字（如 @ : ~ $）或用户自由输入的自定义内容。</summary>
public enum PromptSegmentKind
{
    Variable,
    Literal,
    Custom,
}

/// <summary>解析「一整行 format」得到的结构化片段：类别 + 取值 + 颜色。</summary>
public sealed record PromptFormatSegment(PromptSegmentKind Kind, string Value, string Color);

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
