using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>
/// 美化页「通用设置」里的单窗口开关。状态以 Windows Terminal 根级属性的实际值为准，
/// 草稿与其它外观项共用一份快照，点「保存设置」时合并成一次落盘。
/// </summary>
public sealed partial class BeautifyViewModel
{
    private bool _singleWindowMode;

    public bool SingleWindowMode
    {
        get => _singleWindowMode;
        set
        {
            if (SetField(ref _singleWindowMode, value)) MarkAppearanceDirty();
        }
    }

    /// <summary>
    /// 单标签时隐藏整条标签栏要两项同时为 false；showTabsInTitlebar 为 true 时
    /// Windows Terminal 会忽略 alwaysShowTabs，因此两项都由 PAM 一起维护。
    /// </summary>
    private static bool IsSingleWindow(WindowsTerminalGlobalFlags flags)
        => flags.AlwaysShowTabs == false && flags.ShowTabsInTitlebar == false;

    /// <summary>关掉开关时显式写回 WT 的默认取值，不留半配置状态。</summary>
    private static IReadOnlyList<TerminalAppearanceChange> BuildSingleWindowChanges(bool enabled, bool before)
        => enabled == before
            ? []
            :
            [
                TerminalAppearanceChange.SetBool(TerminalAppearanceKey.AlwaysShowTabs, !enabled),
                TerminalAppearanceChange.SetBool(TerminalAppearanceKey.ShowTabsInTitlebar, !enabled),
            ];
}
