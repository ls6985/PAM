using System.Text.Json.Serialization;

namespace PAM.Core;

/// <summary>
/// 应用级设置模型，持久化在 <c>%AppData%\PAM\settings.json</c>。
/// 为后续的自定义命令与设置页预留。
/// </summary>
public sealed class AppSettings
{
    /// <summary>唤醒终端的全局热键组合，例如 <c>Ctrl+Alt+T</c>。</summary>
    public string Hotkey { get; set; } = "Ctrl+Alt+T";

    /// <summary>唤醒终端时使用的 shell：<see cref="TerminalTypePwsh"/> 或 <see cref="TerminalTypePowerShell"/>。</summary>
    public string TerminalType { get; set; } = TerminalTypePwsh;

    /// <summary>保存别名后是否自动启动新的 PowerShell 窗口。</summary>
    public bool AutoLaunchTerminal { get; set; } = true;

    /// <summary>PowerShell 7（pwsh.exe）。</summary>
    public const string TerminalTypePwsh = "pwsh";

    /// <summary>Windows PowerShell 5.1（powershell.exe）。</summary>
    public const string TerminalTypePowerShell = "powershell";

    /// <summary>是否优先使用 PowerShell 7。</summary>
    [JsonIgnore]
    public bool UsesPowerShell7 =>
        string.Equals(TerminalType, TerminalTypePwsh, StringComparison.OrdinalIgnoreCase);
}
