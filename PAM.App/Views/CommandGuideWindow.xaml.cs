using System.Windows;
using Wpf.Ui.Controls;

namespace PAM.App.Views;

/// <summary>
/// 「示例 / 指南」窗口：列出常用命令的功能说明与写法，支持一键复制到剪贴板。
/// 只做展示与复制，不读写配置。
/// </summary>
public partial class CommandGuideWindow : FluentWindow
{
    private static readonly GuideExample[] Examples =
    [
        new(
            "查看 Git 工作区状态",
            "gst",
            "git status -sb"),
        new(
            "长格式列出当前目录文件（含隐藏项）",
            "ll",
            "Get-ChildItem -Force"),
        new(
            "查询公网 IP 及归属地",
            "ip",
            "Invoke-RestMethod http://ipinfo.io/json | Select-Object ip, city, region, country, org"),
        new(
            "清理临时文件与回收站",
            "clean",
            """
            Remove-Item "$env:TEMP\*" -Recurse -Force -ErrorAction SilentlyContinue
            Clear-RecycleBin -Force -ErrorAction SilentlyContinue
            """),
        new(
            "导出已保存的 WiFi 密码",
            "wifi",
            """
            netsh wlan show profiles | Select-String '所有用户配置文件|All User Profile' | ForEach-Object {
                $name = ($_ -split ':')[-1].Trim()
                $key = netsh wlan show profiles name="$name" key=clear | Select-String '关键内容|Key Content'
                "{0} : {1}" -f $name, $key
            }
            """),
        new(
            "持续 ping 指定地址直到手动停止",
            "ping8",
            "ping -t 8.8.8.8"),
    ];

    public CommandGuideWindow()
    {
        InitializeComponent();
        ExampleList.ItemsSource = Examples;
    }

    private void CopyExampleButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not GuideExample example)
        {
            return;
        }

        try
        {
            // copy: true 让内容在窗口关闭后仍留在剪贴板。
            Clipboard.SetDataObject(example.Code, copy: true);
            UiLocalizer.SetTranslated(CopyStatusText, TextBlock.TextProperty, $"已复制：{example.Description}");
        }
        catch (Exception ex)
        {
            UiLocalizer.SetTranslated(CopyStatusText, TextBlock.TextProperty, $"复制失败：{ex.Message}");
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>示例条目：功能说明、建议别名与命令代码（可多行）。</summary>
    public sealed record GuideExample(string Description, string Alias, string Code)
    {
        public string AliasHint => $"别名示例：{Alias}";
    }
}
