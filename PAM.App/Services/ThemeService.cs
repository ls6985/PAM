using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using PAM.Core;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace PAM.App;

/// <summary>
/// 主题应用：亮 / 暗为固定主题；"跟随系统"时挂上 WPF-UI 的系统主题监视器。
/// 每次应用主题后同步替换 PAM 的画刷字典（Themes/PamTheme.*.xaml）。
/// </summary>
public static class ThemeService
{
    private static readonly Uri LightBrushesUri = new("Themes/PamTheme.Light.xaml", UriKind.Relative);
    private static readonly Uri DarkBrushesUri = new("Themes/PamTheme.Dark.xaml", UriKind.Relative);

    private static bool _watching;
    private static bool _brushesSubscribed;

    public static void Apply(string? theme, Window window)
    {
        if (window is null) return;

        EnsureBrushesFollowTheme();

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
            ApplyBrushes();
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
        ApplyBrushes();
    }

    private static ApplicationTheme ResolveSystemTheme()
    {
        if (ApplicationThemeManager.IsSystemHighContrast()) return ApplicationTheme.HighContrast;

        return ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark
            ? ApplicationTheme.Dark
            : ApplicationTheme.Light;
    }

    private static void EnsureBrushesFollowTheme()
    {
        if (_brushesSubscribed) return;

        // SystemThemeWatcher 跟随系统切换主题时绕过本类，Changed 事件是两条路径的共同出口。
        ApplicationThemeManager.Changed += static (_, _) => ApplyBrushes();
        _brushesSubscribed = true;
    }

    /// <summary>
    /// 按当前明暗把 PAM 画刷字典换成浅色 / 深色一份。
    /// WPF-UI 应用主题时会替换自己的主题字典，因此这里始终把 PAM 字典放到合并列表末尾，
    /// 保证同名键（强调色、开关、按钮描边等）以 PAM 的值为准。
    /// </summary>
    private static void ApplyBrushes()
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var target = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark ? DarkBrushesUri : LightBrushesUri;
        var targetName = Path.GetFileName(target.OriginalString);

        var existing = dictionaries.FirstOrDefault(static dictionary =>
            dictionary.Source?.OriginalString.Contains("PamTheme.", StringComparison.OrdinalIgnoreCase) == true);

        var isCurrent = existing?.Source?.OriginalString.Contains(targetName, StringComparison.OrdinalIgnoreCase) == true;
        var isLast = existing is not null && dictionaries.Count > 0 && ReferenceEquals(existing, dictionaries[^1]);
        if (isCurrent && isLast) return;

        if (existing is not null) dictionaries.Remove(existing);
        dictionaries.Add(new ResourceDictionary { Source = target });
    }
}
