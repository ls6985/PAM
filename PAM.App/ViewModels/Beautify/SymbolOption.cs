using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>固定符号（@ : ~ $ 空格）作为片段内容时的可选项；语言无关，无需刷新。</summary>
public sealed class SymbolOption : ISegmentChoice
{
    public SymbolOption(string value, string displayName, string toolTip)
    {
        Value = value;
        DisplayName = displayName;
        ToolTipText = toolTip;
    }

    public PromptSegmentKind Kind => PromptSegmentKind.Literal;
    public string Value { get; }
    public string DisplayName { get; }
    public string ToolTipText { get; }

    /// <summary>收起时显示符号本身（空格显示为“空格”）。</summary>
    public string ShortName => string.Equals(Value, " ", StringComparison.Ordinal) ? "空格" : Value;
}
