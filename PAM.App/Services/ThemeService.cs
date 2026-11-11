using System.Windows;
using System.Windows.Media;
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
    private static bool _brushesSubscribed;

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
            UpdateNavTabBrushes();
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
        UpdateNavTabBrushes();
    }

    private static ApplicationTheme ResolveSystemTheme()
    {
        if (ApplicationThemeManager.IsSystemHighContrast()) return ApplicationTheme.HighContrast;

        return ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark
            ? ApplicationTheme.Dark
            : ApplicationTheme.Light;
    }

    /// <summary>
    /// 按当前主题用系统强调色重算顶部导航页签的选中底色与描边（见 Styles.xaml 的 NavTabItemStyle）。
    /// WPF-UI 不会自动切换应用内自定义画刷，主题应用后须手动写入应用资源。
    /// </summary>
    private static void UpdateNavTabBrushes()
    {
        if (!_brushesSubscribed)
        {
            // SystemThemeWatcher 跟随系统切换主题时绕过本类，Changed 事件是两条路径的共同出口。
            ApplicationThemeManager.Changed += static (_, _) => UpdateNavTabBrushes();
            _brushesSubscribed = true;
        }

        var resources = Application.Current.Resources;
        var isDark = ApplicationThemeManager.GetAppTheme() is ApplicationTheme.Dark or ApplicationTheme.HighContrast;

        Color accent;
        if (resources["SystemAccentColorPrimary"] is Color color)
        {
            accent = color;
        }
        else if (resources["SystemAccentColorPrimary"] is SolidColorBrush brush)
        {
            accent = brush.Color;
        }
        else
        {
            return;
        }

        // 浅色主题用低透明度铺出浅强调底，配合主文字色保证对比；深色主题提高透明度保持原有的选中感。
        var backgroundAlpha = (byte)(isDark ? 0x5A : 0x26);
        var borderAlpha = (byte)(isDark ? 0x8C : 0x73);

        resources["NavTabSelectedBackgroundBrush"] =
            new SolidColorBrush(Color.FromArgb(backgroundAlpha, accent.R, accent.G, accent.B));
        resources["NavTabSelectedBorderBrush"] =
            new SolidColorBrush(Color.FromArgb(borderAlpha, accent.R, accent.G, accent.B));
    }
}
