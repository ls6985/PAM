using System.Text.Json.Serialization;

namespace PAM.Core;

/// <summary>
/// 应用级设置模型，持久化在 <c>%AppData%\PAM\settings.json</c>。
/// PAM.App（设置页）与 PAM.Tray（后台热键）共用同一份文件。
/// </summary>
public sealed class AppSettings
{
    /// <summary>默认热键组合。</summary>
    public const string DefaultHotkey = "Ctrl+Alt+T";

    public const string ThemeSystem = "System";
    public const string ThemeLight = "Light";
    public const string ThemeDark = "Dark";

    public const string LanguageZhCN = "zh-CN";
    public const string LanguageEnUS = "en-US";

    /// <summary>PowerShell 7（pwsh.exe）。</summary>
    public const string TerminalTypePwsh = "pwsh";

    /// <summary>Windows PowerShell 5.1（powershell.exe）。</summary>
    public const string TerminalTypePowerShell = "powershell";

    /// <summary>自动选择：装了 PowerShell 7 就用它，否则回退 5.1。</summary>
    public const string TerminalTypeAuto = "auto";

    /// <summary>唤醒终端的全局热键组合，例如 <c>Ctrl+Alt+T</c>。</summary>
    public string Hotkey { get; set; } = DefaultHotkey;

    /// <summary>唤醒终端时使用的 shell。</summary>
    public string TerminalType { get; set; } = TerminalTypePwsh;

    /// <summary>保存别名后是否自动启动新的 PowerShell 窗口。</summary>
    public bool AutoLaunchTerminal { get; set; } = true;

    /// <summary>界面主题：<see cref="ThemeSystem"/> / <see cref="ThemeLight"/> / <see cref="ThemeDark"/>。</summary>
    public string Theme { get; set; } = ThemeSystem;

    /// <summary>界面语言：<see cref="LanguageZhCN"/> / <see cref="LanguageEnUS"/>。</summary>
    public string Language { get; set; } = LanguageZhCN;

    /// <summary>关闭管理器窗口时是否保留后台 PAM.Tray 继续运行。</summary>
    public bool KeepTrayOnClose { get; set; } = true;

    /// <summary>登录 Windows 后是否自动启动后台托盘；由 PAM.App 同步到当前用户的启动项。</summary>
    public bool StartTrayWithWindows { get; set; } = true;

    /// <summary>后台托盘检测到新打开的终端窗口时，是否把该窗口的输入法切成英文。</summary>
    public bool SwitchImeToEnglish { get; set; } = true;

    /// <summary>别名启动软件时是否用独立进程：静默程序日志，且关闭终端不会连带关闭程序。</summary>
    public bool AliasLaunchEnhanced { get; set; } = true;

    /// <summary>别名启动软件时，是否先在终端打印一行别名备注。</summary>
    public bool ShowAliasRemarkOnLaunch { get; set; } = true;

    /// <summary>终端中输入未找到的命令时，是否用一行友好提示替代 PowerShell 的默认报错。</summary>
    public bool FriendlyCommandNotFound { get; set; } = true;

    /// <summary>后台托盘是否在启动后延迟创建那个长期复用的隐藏终端窗口，省去首次按热键的冷启动等待。</summary>
    public bool WarmPoolEnabled { get; set; }

    /// <summary>是否优先使用 PowerShell 7。</summary>
    [JsonIgnore]
    public bool UsesPowerShell7 =>
        string.Equals(TerminalType, TerminalTypePwsh, StringComparison.OrdinalIgnoreCase);

    /// <summary>浅拷贝，用于设置页比较"是否有改动"与回滚。</summary>
    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    /// <summary>
    /// 修正被手工改坏的配置值，保证下游代码拿到的都是可识别的枚举值。
    /// </summary>
    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(Hotkey) || !HotkeyChord.TryParse(Hotkey, out _, out _))
        {
            Hotkey = DefaultHotkey;
        }
        else
        {
            Hotkey = HotkeyChord.Parse(Hotkey)!.Display;
        }

        if (!IsOneOf(Theme, ThemeSystem, ThemeLight, ThemeDark)) Theme = ThemeSystem;
        if (!IsOneOf(Language, LanguageZhCN, LanguageEnUS)) Language = LanguageZhCN;
        if (!IsOneOf(TerminalType, TerminalTypePwsh, TerminalTypePowerShell, TerminalTypeAuto))
        {
            TerminalType = TerminalTypePwsh;
        }
    }

    private static bool IsOneOf(string value, params string[] candidates)
        => candidates.Any(candidate => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
}
