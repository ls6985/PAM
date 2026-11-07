using PAM.Core;

namespace PAM.App;

public sealed class SettingsChangedEventArgs(AppSettings previous, AppSettings current) : EventArgs
{
    public AppSettings Previous { get; } = previous;
    public AppSettings Current { get; } = current;

    public bool ThemeChanged => !string.Equals(Previous.Theme, Current.Theme, StringComparison.OrdinalIgnoreCase);
    public bool LanguageChanged => !string.Equals(Previous.Language, Current.Language, StringComparison.OrdinalIgnoreCase);
    public bool TerminalChanged => !string.Equals(Previous.TerminalType, Current.TerminalType, StringComparison.OrdinalIgnoreCase);
    public bool HotkeyChanged => !string.Equals(Previous.Hotkey, Current.Hotkey, StringComparison.OrdinalIgnoreCase);
    public bool KeepTrayOnCloseChanged => Previous.KeepTrayOnClose != Current.KeepTrayOnClose;
    public bool SwitchImeToEnglishChanged => Previous.SwitchImeToEnglish != Current.SwitchImeToEnglish;
}

/// <summary>
/// 应用内唯一的设置入口。负责读写 settings.json、通知后台托盘刷新，
/// 并把"哪些字段变了"广播给窗口 / 视图模型（例如切换终端后重新加载 $PROFILE）。
/// </summary>
public static class AppServices
{
    public static SettingsService SettingsService { get; } = new();

    public static AppSettings Settings { get; private set; }

    public static event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

    static AppServices()
    {
        Settings = SettingsService.Load();
    }

    /// <summary>写入设置并同步托盘。保存失败时抛出异常，由设置页显示错误。</summary>
    public static void Apply(AppSettings updated)
    {
        ArgumentNullException.ThrowIfNull(updated);
        updated.Normalize();

        // 先落盘：写入失败时内存状态保持不变，界面可以回滚。
        SettingsService.Save(updated);

        var previous = Settings;
        Settings = updated;

        TrayBridge.SignalSettingsChanged();
        SettingsChanged?.Invoke(null, new SettingsChangedEventArgs(previous, updated));
    }

    /// <summary>从磁盘重新读取（托盘或用户手工改过 settings.json 时）。</summary>
    public static void Reload()
    {
        var previous = Settings;
        Settings = SettingsService.Load();
        SettingsChanged?.Invoke(null, new SettingsChangedEventArgs(previous, Settings));
    }

    public static string SettingsFilePath => SettingsService.SettingsFilePath;
}
