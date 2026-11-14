using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Wpf.Ui.Controls;

namespace PAM.App.Views;

/// <summary>
/// 提交反馈窗口：与「示例 / 指南」同款二级弹窗。
///
/// 【低成本方案：mailto】零后端、零依赖、零隐私风险：
///   1. 提交时把「类型 / 内容 / 联系方式 / 版本 / 系统信息」整理成一段文本，复制到剪贴板（永远可用）；
///   2. 用 mailto 打开默认邮件客户端，自动填好收件人、主题与正文，由用户确认后发送；
///   3. 程序不会自动发送、上传或保存任何输入；没装邮件客户端时，剪贴板内容仍可手动粘贴。
///
/// 以后要换通道，只改下面两个常量，不用动界面：
///   - FeedbackEmail：收件邮箱（当前 mailto 通道，已配置）
///   - RepoIssuesUrl：填了就优先跳转 GitHub「新建 Issue」页，形如 https://github.com/owner/repo/issues/new
/// TODO: 这些中文文案尚未注册 LocKey，做英文切换时需同步 PAM.Core/LocKey.cs。
/// </summary>
public partial class FeedbackWindow : FluentWindow
{
    private const string RepoIssuesUrl = "";
    private const string FeedbackEmail = "1074418532@qq.com";

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
            "邮件需要你在邮件客户端里确认后才会发出；程序不会自动发送、上传或保存你的输入。内容也在剪贴板里，可粘贴到任意渠道。");

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
    /// 尝试打开最省事的外部通道：优先 GitHub Issue，其次邮件，都没有就只留剪贴板。
    /// 返回给用户的中文结果说明，任何异常都不抛出。
    /// </summary>
    private static string TryOpenChannel(string type, string text)
    {
        // URL 有长度限制，长文本截断，完整内容始终在剪贴板里。
        var body = text.Length > 1200 ? text[..1200] + "\n\n（完整内容已复制到剪贴板）" : text;

        try
        {
            var encodedBody = Uri.EscapeDataString(body);

            if (!string.IsNullOrWhiteSpace(RepoIssuesUrl))
            {
                var title = Uri.EscapeDataString($"[反馈] {type}");
                var url = $"{RepoIssuesUrl}?title={title}&body={encodedBody}";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return "已打开浏览器的新建 Issue 页面。";
            }

            if (!string.IsNullOrWhiteSpace(FeedbackEmail))
            {
                var subject = Uri.EscapeDataString($"[PowerShell 管理器] {type}");
                var url = $"mailto:{FeedbackEmail}?subject={subject}&body={encodedBody}";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return "已尝试打开邮件客户端，确认后发送即可。";
            }
        }
        catch
        {
            return "未能打开外部程序，内容仍在剪贴板里。";
        }

        return "当前未配置仓库或邮箱，可直接粘贴到任意反馈渠道。";
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
