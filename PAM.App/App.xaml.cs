using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace PAM.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var theme = ApplicationThemeManager.IsSystemHighContrast()
            ? ApplicationTheme.HighContrast
            : ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;

        ApplicationThemeManager.Apply(theme);

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;

        // 跟随系统深浅色和强调色变化。
        SystemThemeWatcher.Watch(mainWindow, WindowBackdropType.Mica, updateAccents: true);

        mainWindow.Show();
    }
}
