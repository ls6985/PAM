using System.Windows;
using Wpf.Ui.Controls;

namespace PAM.App.Views;

/// <summary>
/// 赞助支持窗口：与「示例 / 指南」同款二级弹窗。
/// 两张收款码是 Assets/Sponsor/WeChatPay.png 与 AliPay.png（csproj 里注册为 Resource），
/// 换码时直接替换同名图片即可，不用改 XAML / 代码。
/// TODO: 这些中文文案尚未注册 LocKey，做英文切换时需同步 PAM.Core/LocKey.cs。
/// </summary>
public partial class SponsorWindow : FluentWindow
{
    public SponsorWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
