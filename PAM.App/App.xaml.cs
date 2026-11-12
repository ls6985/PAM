using System.Windows;
using PAM.Core;

namespace PAM.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = AppServices.Settings;

        // 语言要在任何窗口构造之前设好；界面遍历器负责把 XAML 里的中文替换成当前语言。
        LocalizationService.Instance.SetLanguage(settings.Language);
        UiLocalizer.Initialize();

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;

        ThemeService.Apply(settings.Theme, mainWindow);
        AppServices.SettingsChanged += OnSettingsChanged;

        mainWindow.Show();
        UiLocalizer.RefreshAll();

        // 主窗口启动后拉起后台托盘，使全局热键在本次会话内可用（托盘自身有单实例互斥）。
        TrayBridge.EnsureTrayRunning();

        // 每次启动按当前 exe 路径校准登录启动项，覆盖首次安装与程序目录移动后的情况。
        AutoStartService.Sync(settings.StartTrayWithWindows, TrayBridge.TrayExePath);
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.LanguageChanged)
        {
            // 即时切换语言：LocExtension / UiLocalizer 刷新静态文本，各 ViewModel 的
            // LanguageChanged 处理程序刷新动态文本，状态栏同步显示切换结果。
            LocalizationService.Instance.SetLanguage(e.Current.Language);
        }

        if (e.ThemeChanged && MainWindow is not null)
        {
            ThemeService.Apply(e.Current.Theme, MainWindow);
        }
    }
}
