using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using Wpf.Ui.Controls;

namespace PAM.App.Views;

/// <summary>
/// 许可证窗口：显示随程序集内嵌的 LICENSE 全文，并提供 GitHub 上的原件入口。
/// 全文来自仓库根目录的同一份文件，不在代码里另存副本。
/// </summary>
public partial class LicenseWindow : FluentWindow
{
    private const string LicenseResourceName = "PAM.App.LICENSE.txt";
    private const string LoadFailureText = "许可证全文未能读取，可点击下方按钮在 GitHub 上查看原件。";

    public LicenseWindow()
    {
        InitializeComponent();
        LicenseTextBox.Text = LoadLicenseText();
    }

    private static string LoadLicenseText()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(LicenseResourceName);
            if (stream is null) return LoadFailureText;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch
        {
            return LoadFailureText;
        }
    }

    private void ViewOnGitHubButton_Click(object sender, RoutedEventArgs e)
    {
        if (GitHubLinks.TryOpen(GitHubLinks.License)) return;

        var dialog = new InfoDialogWindow("查看许可证", "未能打开浏览器，请手动复制下面的地址访问。", GitHubLinks.License)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
