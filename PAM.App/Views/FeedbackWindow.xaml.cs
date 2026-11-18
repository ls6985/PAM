using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Wpf.Ui.Controls;

namespace PAM.App.Views;

/// <summary>
/// 提交反馈窗口：与「示例 / 指南」同款二级弹窗。
/// 提交时把「类型 / 内容 / 联系方式 / 版本 / 系统信息」整理成文本复制到剪贴板，
/// 并打开 GitHub 新建 Issue 页预填标题与正文，由用户确认后发布；程序不自动发送、上传或保存输入。
/// 地址统一取 GitHubLinks.IssuesNew。
/// </summary>
public partial class FeedbackWindow : FluentWindow
{
    public FeedbackWindow()
    {
        InitializeComponent();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

    private void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        // 最简校验：内容为空时给出提示，不关闭窗口。
        if (string.IsNullOrWhiteSpace(ContentBox.Text))
        {
            ValidationText.Visibility = Visibility.Visible;
            ContentBox.Focus();
            return;
        }

        ValidationText.Visibility = Visibility.Collapsed;

        var type = (TypeBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "其他";
        var text = BuildFeedbackText(type, ContentBox.Text.Trim(), ContactBox.Text.Trim());

        // 剪贴板是永远可用的兜底；被其他程序占用时也不影响后续流程。
        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            // 剪贴板偶发被占用，忽略即可。
        }

        var channel = TryOpenChannel(type, text);

        ShowInfo("提交反馈",
            "反馈内容已整理并复制到剪贴板。" + channel,
            "新建 Issue 页需要你在浏览器里确认后才会发布；程序不会自动发送、上传或保存你的输入。内容也在剪贴板里，可粘贴到任意渠道。");

        Close();
    }

    /// <summary>拼一段可直接粘贴的反馈文本，附带排障常用的环境信息。</summary>
    private static string BuildFeedbackText(string type, string content, string contact)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        var sb = new StringBuilder();
        sb.AppendLine($"反馈类型：{type}");
        sb.AppendLine($"软件版本：{version}");
        sb.AppendLine($"系统：{RuntimeInformation.OSDescription} / {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($"运行时：.NET {Environment.Version}");
        sb.AppendLine($"联系方式：{(string.IsNullOrWhiteSpace(contact) ? "（未填写）" : contact)}");
        sb.AppendLine();
        sb.AppendLine("【反馈内容】");
        sb.AppendLine(content);
        return sb.ToString();
    }

    /// <summary>
    /// 打开 GitHub「新建 Issue」页并预填标题与正文；任何异常都不抛出，剪贴板始终保留完整内容。
    /// </summary>
    private static string TryOpenChannel(string type, string text)
    {
        // URL 有长度限制，长文本截断，完整内容始终在剪贴板里。
        var body = text.Length > 1200 ? text[..1200] + "\n\n（完整内容已复制到剪贴板）" : text;
        var title = Uri.EscapeDataString($"[反馈] {type}");
        var url = $"{GitHubLinks.IssuesNew}?title={title}&body={Uri.EscapeDataString(body)}";

        return GitHubLinks.TryOpen(url)
            ? "已打开浏览器的新建 Issue 页面。"
            : "未能打开浏览器，内容仍在剪贴板里，可手动粘贴到仓库 Issues。";
    }

    /// <summary>复用与「示例 / 指南」同款的统一信息弹窗。</summary>
    private void ShowInfo(string title, string message, string? hint = null)
    {
        var dialog = new InfoDialogWindow(title, message, hint)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }
}
