using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>片段内容下拉的第一项：用户可自由输入的自定义内容（任意文字或 $变量）。</summary>
public sealed class CustomOption : ISegmentChoice
{
    public PromptSegmentKind Kind => PromptSegmentKind.Custom;
    public string Value => string.Empty;
    public string DisplayName => "自定义…";
    public string ShortName => "自定义";
    public string ToolTipText => "选此项后可自由输入任意文字或 $变量";
}
