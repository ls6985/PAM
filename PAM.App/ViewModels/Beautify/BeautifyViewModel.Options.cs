using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>
/// 通用提示符选项：主题预设与自定义提示符共用「提示符前空一行」与「路径分隔符用 /」。
/// 主题预设把这两个键写入预设文本，自定义提示符写入自有托管块；读回统一按 TOML 键解析。
/// </summary>
public sealed partial class BeautifyViewModel
{
    private bool _commonOptionsLoaded;

    /// <summary>从磁盘回填通用选项；日常刷新不覆盖用户尚未保存的选择。</summary>
    private void RefreshCommonOptions(bool forceReload = false)
    {
        if (_commonOptionsLoaded && !forceReload) return;

        var (addNewline, useForwardSlash) = _starshipService.ReadCommonOptions();
        _promptAddNewline = addNewline;
        _promptUseForwardSlash = useForwardSlash;
        _commonOptionsLoaded = true;
        OnPropertyChanged(nameof(PromptAddNewline));
        OnPropertyChanged(nameof(PromptUseForwardSlash));
    }

    /// <summary>把两个通用选项恢复成程序默认值，不写盘。</summary>
    private void ResetCommonOptions()
    {
        var (addNewline, useForwardSlash) = StarshipService.CreateDefaultCommonOptions();
        PromptAddNewline = addNewline;
        PromptUseForwardSlash = useForwardSlash;
    }
}
