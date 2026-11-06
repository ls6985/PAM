using System.Windows;
using PAM.Core;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace PAM.App;

/// <summary>
/// 主题应用：亮 / 暗为固定主题；"跟随系统"时挂上 WPF-UI 的系统主题监视器。
/// </summary>
public static class ThemeService
{
    private static bool _watching;

    public static void Apply(string? theme, Window window)
    {
        if (window is null) return;

        var followSystem = !string.Equals(theme, AppSettings.ThemeLight, StringComparison.OrdinalIgnoreCase) &&
                           !string.Equals(theme, AppSettings.ThemeDark, StringComparison.OrdinalIgnoreCase);

        if (followSystem)
        {
            if (!_watching)
            {
                SystemThemeWatcher.Watch(window, WindowBackdropType.Mica, updateAccents: true);
                _watching = true;
            }

            ApplicationThemeManager.Apply(ResolveSystemTheme());
            return;
        }

        if (_watching)
        {
            // 固定主题时必须断开监视器，否则系统主题一变化就会把用户的选择覆盖掉。
            SystemThemeWatcher.UnWatch(window);
            _watching = false;
        }

        ApplicationThemeManager.Apply(
            string.Equals(theme, AppSettings.ThemeDark, StringComparison.OrdinalIgnoreCase)
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light);
    }

    private static ApplicationTheme ResolveSystemTheme()
    {
        if (ApplicationThemeManager.IsSystemHighContrast()) return ApplicationTheme.HighContrast;

        return ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark
            ? ApplicationTheme.Dark
            : ApplicationTheme.Light;
    }
}
