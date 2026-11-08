using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>片段「内容」可选项的统一接口：变量与固定符号共用同一个下拉。</summary>
public interface ISegmentChoice
{
    PromptSegmentKind Kind { get; }
    string Value { get; }
    /// <summary>下拉收起时显示的短名（变量取名称、符号取符号本身）。</summary>
    string ShortName { get; }
    /// <summary>下拉展开时显示的完整名（分类 · 名称）。</summary>
    string DisplayName { get; }
    string ToolTipText { get; }
}
