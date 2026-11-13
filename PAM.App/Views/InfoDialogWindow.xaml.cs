using System.Windows;
using Wpf.Ui.Controls;

namespace PAM.App.Views;

/// <summary>
/// 统一风格的信息提示对话框（与 CommandGuideWindow 同款 FluentWindow + Mica）。
/// 用来替代生硬的系统 MessageBox，保持与主界面视觉一致。
/// </summary>
public partial class InfoDialogWindow : FluentWindow
{
    public InfoDialogWindow(string title, string message)
    {
        InitializeComponent();
        Title = title;
        DialogTitleBar.Title = title;
        MessageText.Text = message;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) => Close();
}
