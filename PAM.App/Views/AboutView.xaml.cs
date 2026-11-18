using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace PAM.App.Views;

/// <summary>
/// 关于页：应用名片 + 更新检测 + 帮助与支持 + 开源与支持。
/// 更新检测只在点击「检查更新」时读取最新发布信息，不下载、不安装、不写配置；
/// 仓库地址、网络请求与版本比对集中在 GitHubLinks / UpdateCheckService。
/// </summary>
public partial class AboutView : UserControl
{
    private bool _checking;
    private bool _statusInitialized;
    private string _versionText = "v1.0.0";

    public AboutView()
    {
        InitializeComponent();
    }

    /// <summary>打开一个二级弹窗并居中于主窗口。</summary>
    private void ShowDialog(FluentWindow dialog)
    {
        dialog.Owner = Window.GetWindow(this);
        dialog.ShowDialog();
    }

    /// <summary>简短信息提示（与「示例 / 指南」同款外壳）。</summary>
    private void ShowInfo(string title, string message, string? hint = null)
        => ShowDialog(new InfoDialogWindow(title, message, hint));

    private void View_Loaded(object sender, RoutedEventArgs e)
    {
        // 动态读取程序集版本，避免硬编码。
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        _versionText = version is null ? "v1.0.0" : $"v{version.Major}.{version.Minor}.{version.Build}";
        AppNameText.Text = "PowerShell 管理器";
        VersionBadge.Text = _versionText;

        // 页签切换会再次触发 Loaded，检查结果不重置。
        if (!_statusInitialized)
        {
            _statusInitialized = true;
            SetStatus($"当前版本 {_versionText}");
        }
    }

    /// <summary>状态文案按中文原文登记，语言切换时不会退回初始占位文本。</summary>
    private void SetStatus(string text)
        => UiLocalizer.SetTranslated(UpdateStatusText, System.Windows.Controls.TextBlock.TextProperty, text);

    /// <summary>打开外部地址；浏览器未能启动时给出可复制的地址。</summary>
    private void OpenUrl(string url, string title)
    {
        if (GitHubLinks.TryOpen(url)) return;
        ShowInfo(title, "未能打开浏览器，请手动复制下面的地址访问。", url);
    }

    // 模块二：检查更新
    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_checking) return;
        _checking = true;
        CheckUpdateButton.IsEnabled = false;
        SetStatus("正在检查更新…");

        try
        {
            var result = await UpdateCheckService.CheckAsync();
            switch (result.Status)
            {
                case UpdateCheckStatus.UpdateAvailable:
                    SetStatus($"发现新版本 {UpdateCheckService.Format(result.LatestVersion)}，可到 GitHub 仓库下载。");
                    break;

                // 仓库还没有发布版本时，与「已是最新」同义：没有可升级的目标。
                case UpdateCheckStatus.UpToDate:
                case UpdateCheckStatus.NoRelease:
                    SetStatus($"当前已是最新版本 {_versionText}。");
                    break;

                default:
                    SetStatus($"检查失败：{result.Error ?? "未知原因"}，请稍后重试。");
                    break;
            }
        }
        catch
        {
            SetStatus("检查失败，请稍后重试。");
        }
        finally
        {
            _checking = false;
            CheckUpdateButton.IsEnabled = true;
        }
    }

    // 模块三：帮助文档
    private void HelpDocButton_Click(object sender, RoutedEventArgs e)
        => ShowDialog(new HelpDocWindow());

    // 模块三：提交反馈
    private void SubmitFeedbackButton_Click(object sender, RoutedEventArgs e)
        => ShowDialog(new FeedbackWindow());

    // 模块四：GitHub 仓库
    private void RepoButton_Click(object sender, RoutedEventArgs e)
        => OpenUrl(GitHubLinks.Repository, "GitHub 仓库");

    // 模块四：查看许可证（说明文字里的链接）
    private void LicenseLink_Click(object sender, RoutedEventArgs e)
        => ShowDialog(new LicenseWindow());

    // 模块四：赞助支持
    private void SponsorButton_Click(object sender, RoutedEventArgs e)
        => ShowDialog(new SponsorWindow());
}
