using Microsoft.Win32;

namespace PAM.Core;

/// <summary>
/// 维护当前用户登录时自动启动 PAM.Tray.exe 的 HKCU 启动项。
/// 值固定为带引号的绝对路径；程序目录移动后由 PAM.App 在启动时按当前路径重新同步。
/// </summary>
public static class AutoStartService
{
    /// <summary>启动项名称，同时用于 Explorer 的启动应用启用状态记录。</summary>
    public const string RunValueName = "PAM.Tray";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupApprovedPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    /// <summary>启动应用被任务管理器禁用时，启用状态记录的首字节为 3。</summary>
    private const byte DisabledByStartupApproved = 3;

    /// <summary>把 exe 路径包成 Run 项的值，路径含空格时也能被 Windows 正确解析。</summary>
    public static string BuildRunValue(string exePath)
        => $"\"{exePath.Trim().Trim('"')}\"";

    /// <summary>从 Run 项的值里取出 exe 路径，兼容带引号、带参数与无引号三种写法。</summary>
    internal static string? ExtractExecutablePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var text = value.Trim();
        if (text[0] == '"')
        {
            var closingQuote = text.IndexOf('"', 1);
            return closingQuote < 0 ? null : text[1..closingQuote];
        }

        // 无引号的值用第一个空格分隔程序与启动参数。
        var separator = text.IndexOf(' ');
        return separator < 0 ? text : text[..separator];
    }

    /// <summary>判断已有启动项是否仍指向目标 exe，路径按完整路径规范化后忽略大小写比较。</summary>
    internal static bool PointsTo(string? value, string exePath)
    {
        var current = ExtractExecutablePath(value);
        if (current is null) return false;

        try
        {
            return string.Equals(
                Path.GetFullPath(current),
                Path.GetFullPath(exePath),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>当前用户的启动项是否已启用并指向目标 exe。</summary>
    public static bool IsEnabled(string exePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var value = key?.GetValue(RunValueName) as string;
            return value is not null
                   && PointsTo(value, exePath)
                   && !IsDisabledByStartupApproved();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 按设置值同步启动项，写入失败返回 false。目标 exe 不存在时不创建启动项，
    /// 避免单独运行调试版主界面时留下指向无效路径的记录。
    /// </summary>
    public static bool Sync(bool enabled, string exePath)
    {
        if (!enabled) return Disable();

        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) return false;

        return Enable(exePath);
    }

    private static bool Enable(string exePath)
    {
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true))
            {
                if (key is null) return false;

                var expected = BuildRunValue(exePath);
                var current = key.GetValue(RunValueName) as string;
                if (!string.Equals(current, expected, StringComparison.OrdinalIgnoreCase))
                {
                    key.SetValue(RunValueName, expected, RegistryValueKind.String);
                }
            }

            // 任务管理器把禁用状态记在 StartupApproved；同名旧记录会让 Run 项存在却仍不启动。
            using (var approved = Registry.CurrentUser.OpenSubKey(StartupApprovedPath, writable: true))
            {
                approved?.DeleteValue(RunValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool Disable()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
            {
                key?.DeleteValue(RunValueName, throwOnMissingValue: false);
            }

            using (var approved = Registry.CurrentUser.OpenSubKey(StartupApprovedPath, writable: true))
            {
                approved?.DeleteValue(RunValueName, throwOnMissingValue: false);
            }

            // 启动项原本不存在也视为同步成功，保持操作幂等。
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDisabledByStartupApproved()
    {
        using var approved = Registry.CurrentUser.OpenSubKey(StartupApprovedPath, writable: false);
        return approved?.GetValue(RunValueName) is byte[] binary
               && binary.Length > 0
               && binary[0] == DisabledByStartupApproved;
    }
}
