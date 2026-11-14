using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace PAM.App.Views;

/// <summary>
/// 关于页：应用名片 + 帮助与反馈 + 开源与支持。
/// 三个入口各自打开与「示例 / 指南」同款的二级弹窗；GitHub 仓库与查看许可证仍为占位，
/// 待拿到真实链接 / 许可证全文后接入（见下方 TODO）。
/// </summary>
public partial class AboutView : UserControl
{
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
        var verText = version is null ? "v1.0.0" : $"v{version.Major}.{version.Minor}.{version.Build}";
        AppNameText.Text = "PowerShell 管理器";
        VersionBadge.Text = verText;
    }

    // 模块二：帮助文档 —— 已填充内容
    private void HelpDocButton_Click(object sender, RoutedEventArgs e)
        => ShowDialog(new HelpDocWindow());

    // 模块二：提交反馈 —— 仅界面版
    private void SubmitFeedbackButton_Click(object sender, RoutedEventArgs e)
        => ShowDialog(new FeedbackWindow());

    // 模块三：GitHub 仓库 —— TODO: 拿到真实 URL 后改为打开浏览器链接
    private void RepoButton_Click(object sender, RoutedEventArgs e)
    {
        ShowInfo("GitHub 仓库",
            "本项目基于 MIT 协议开源，欢迎查看源码、提 Issue 或参与贡献。",
            "仓库入口待接入：拿到真实 URL 后改为点击直接用浏览器打开。");
    }

    // 模块三：查看许可证 —— TODO: 拿到许可证全文后改为展示完整文本
    private void LicenseButton_Click(object sender, RoutedEventArgs e)
    {
        ShowInfo("查看许可证",
            "本软件基于 MIT 协议开源，可自由使用、修改与分发。",
            "许可证全文待接入：后续可在此打开完整许可证文本。");
    }

    // 模块三：赞助支持 —— 初版
    private void SponsorButton_Click(object sender, RoutedEventArgs e)
        => ShowDialog(new SponsorWindow());
}
