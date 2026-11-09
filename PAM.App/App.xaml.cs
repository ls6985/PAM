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
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.LanguageChanged)
        {
            LocalizationService.Instance.SetLanguage(e.Current.Language);
        }

        if (e.ThemeChanged && MainWindow is not null)
        {
            ThemeService.Apply(e.Current.Theme, MainWindow);
        }
    }
}
