using System.Windows;
using PAM.Core;
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
            LocKey.CommandGuideGitStatus,
            "gst",
            "git status -sb"),
        new(
            LocKey.CommandGuideListLong,
            "ll",
            "Get-ChildItem -Force"),
        new(
            LocKey.CommandGuidePublicIp,
            "ip",
            "Invoke-RestMethod http://ipinfo.io/json | Select-Object ip, city, region, country, org"),
        new(
            LocKey.CommandGuideCleanTemp,
            "clean",
            """
            Remove-Item "$env:TEMP\*" -Recurse -Force -ErrorAction SilentlyContinue
            Clear-RecycleBin -Force -ErrorAction SilentlyContinue
            """),
        new(
            LocKey.CommandGuideWifiPassword,
            "wifi",
            """
            netsh wlan show profiles | Select-String '所有用户配置文件|All User Profile' | ForEach-Object {
                $name = ($_ -split ':')[-1].Trim()
                $key = netsh wlan show profiles name="$name" key=clear | Select-String '关键内容|Key Content'
                "{0} : {1}" -f $name, $key
            }
            """),
        new(
            LocKey.CommandGuidePing,
            "ping8",
            "ping -t 8.8.8.8"),
    ];

    private GuideExample? _lastExample;
    private bool _lastCopySucceeded;
    private string? _lastErrorMessage;

    public CommandGuideWindow()
    {
        InitializeComponent();
        ExampleList.ItemsSource = Examples;
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (_lastExample is not null)
        {
            var status = _lastCopySucceeded
                ? LocalizationService.Instance.F(LocKey.Copied0, _lastExample.Description)
                : LocalizationService.Instance.F(LocKey.CopyFailed0, _lastErrorMessage ?? string.Empty);
            UiLocalizer.SetTranslated(CopyStatusText, TextBlock.TextProperty, status);
        }

        ExampleList.Items.Refresh();
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
            _lastExample = example;
            _lastCopySucceeded = true;
            UiLocalizer.SetTranslated(CopyStatusText, TextBlock.TextProperty,
                LocalizationService.Instance.F(LocKey.Copied0, example.Description));
        }
        catch (Exception ex)
        {
            _lastExample = example;
            _lastCopySucceeded = false;
            _lastErrorMessage = ex.Message;
            UiLocalizer.SetTranslated(CopyStatusText, TextBlock.TextProperty,
                LocalizationService.Instance.F(LocKey.CopyFailed0, ex.Message));
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>示例条目：功能说明、建议别名与命令代码（可多行）。功能说明以 LocKey 持有，按当前语言解析。</summary>
    public sealed record GuideExample(LocKey DescriptionKey, string Alias, string Code)
    {
        public string Description => LocalizationService.Instance.T(DescriptionKey);

        public string AliasHint => LocalizationService.Instance.F(LocKey.AliasExampleHint, Alias);
    }
}
