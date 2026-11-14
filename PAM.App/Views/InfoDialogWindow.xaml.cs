using System.Windows;
using Wpf.Ui.Controls;

namespace PAM.App.Views;

/// <summary>
/// 二级弹窗统一信息窗口（与 CommandGuideWindow「示例 / 指南」同款 FluentWindow + Mica 风格）。
/// 替代系统 MessageBox，并作为以后新增二级弹窗的模板。
/// 支持可选的次要提示行，排版节奏与指南的 intro / hint 一致。
/// </summary>
public partial class InfoDialogWindow : FluentWindow
{
    public InfoDialogWindow(string title, string message, string? hint = null)
    {
        InitializeComponent();
        Title = title;
        DialogTitleBar.Title = title;
        MessageText.Text = message;

        if (!string.IsNullOrWhiteSpace(hint))
        {
            HintText.Text = hint;
            HintText.Visibility = Visibility.Visible;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
