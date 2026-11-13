using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace PAM.App.Views;

/// <summary>
/// 关于页（初版 / 占位实现）。
/// 当前为信息展示 + 按钮占位，方便后续接入真实逻辑。
/// 需替换的地方都标了 // TODO: 接入真实功能。
/// </summary>
public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
    }

    /// <summary>以统一风格的 Fluent 对话框替代系统 MessageBox。</summary>
    private void ShowInfo(string title, string message)
    {
        var dialog = new InfoDialogWindow(title, message)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();
    }

    private void View_Loaded(object sender, RoutedEventArgs e)
    {
        // 动态读取程序集版本，避免硬编码。
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var verText = version is null ? "v1.0.0" : $"v{version.Major}.{version.Minor}.{version.Build}";
        AppNameText.Text = "PowerShell 管理器";
        VersionBadge.Text = verText;
    }

    // 模块二：帮助文档
    private void HelpDocButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: 接入真实功能——可改为打开使用文档（本地文件或网页）。
        ShowInfo("帮助文档", "（帮助文档 待接入：可在此打开使用说明或跳转在线文档）");
    }

    // 模块二：提交反馈
    private void SubmitFeedbackButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: 接入真实功能——可改为打开反馈表单窗口，或拉起邮件/issue 链接。
        ShowInfo("提交反馈",
            "感谢你的反馈！\n（提交反馈 入口待接入：可在此打开反馈表单或邮件/工单链接）");
    }

    // 模块三：GitHub 仓库
    private void RepoButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: 接入真实功能——可改为用浏览器打开仓库地址（替换为真实 URL）。
        ShowInfo("GitHub 仓库", "（GitHub 仓库 待接入：可在此用浏览器打开仓库地址）");
    }

    // 模块三：查看许可证
    private void LicenseButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: 接入真实功能——可改为打开许可证全文（本地文件或网页）。
        ShowInfo("查看许可证", "（查看许可证 待接入：可在此打开许可证全文）");
    }

    // 模块三：赞助支持
    private void SponsorButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: 接入真实功能——可改为打开赞助/捐赠链接或二维码窗口。
        ShowInfo("赞助支持",
            "如果这个工具帮到了你，欢迎支持作者继续维护。\n（赞助入口待接入：可在此打开放置/捐赠链接）");
    }
}
