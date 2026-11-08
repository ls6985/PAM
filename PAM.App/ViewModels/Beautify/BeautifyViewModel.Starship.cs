using System.ComponentModel;
using System.Windows.Threading;
using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>
/// Starship 安装功能：一键安装（Scoop 缺失时先装 Scoop，再装 Starship），安装进度回投到 UI 线程。
/// 修改安装逻辑只看本文件。
/// </summary>
public sealed partial class BeautifyViewModel
{
    // Starship 官方建议先装好 Nerd Font 再启用，因此未选字体时不允许安装。
    // 只保留一键安装：Scoop 缺失时由 Core 自动先装 Scoop，不需要用户自己挑安装方式。
    public bool CanOneClickInstall => !IsBusy && !_isStarshipInstalled && HasFontSelection;

    /// <summary>一键安装：Scoop 不存在时先装 Scoop（官方引导脚本），再装 Starship。</summary>
    public async Task InstallStarshipWithScoopAsync()
    {
        await RunBusyAsync(
            "正在安装 Scoop 与 Starship，可能需要几分钟…",
            () => _starshipService.InstallScoopAndStarship(ReportProgress),
            static result => new[]
            {
                (result.Success ? StatusType.Saved : StatusType.Error, result.Message),
            });
    }

    private void ReportProgress(string line)
    {
        // 安装输出从后台线程到达，统一投递到 UI 线程展示。
        void Update() => FeedbackMessage = Loc.T(line.Trim());

        if (_dispatcher.CheckAccess())
        {
            Update();
        }
        else
        {
            _ = _dispatcher.BeginInvoke(Update);
        }
    }
}
