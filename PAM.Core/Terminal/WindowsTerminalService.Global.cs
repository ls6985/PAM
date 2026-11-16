using System.IO;
using System.Text.Json;

namespace PAM.Core;

/// <summary>窗口行为与标签栏的四个根级布尔属性；null 表示未显式设置（跟随 Windows Terminal 默认值）。</summary>
public sealed record WindowsTerminalGlobalFlags(
    bool? AutoHideWindow,
    bool? MinimizeToNotificationArea,
    bool? AlwaysShowTabs,
    bool? ShowTabsInTitlebar)
{
    public static WindowsTerminalGlobalFlags Empty { get; } = new(null, null, null, null);
}

/// <summary>
/// settings.json 根级属性的读回。写入复用 <c>ApplyAppearance()</c>：四项都在
/// <c>TerminalAppearanceSchema</c> 里登记为单段路径，与外观项共用同一套 JSONC 拼接、
/// 首次备份、逐项校验和原子落盘，因此多个入口写同一个文件不会互相覆盖。
/// </summary>
public sealed partial class WindowsTerminalService
{
    /// <summary>读取根级窗口属性；文件不存在、结构异常或解析失败时全部返回 null。</summary>
    public WindowsTerminalGlobalFlags ReadGlobalFlags(string? settingsPath = null)
    {
        var path = settingsPath ?? DiscoverSettingsPath();
        if (path is null || !File.Exists(path))
        {
            return WindowsTerminalGlobalFlags.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path), ParseOptions);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return WindowsTerminalGlobalFlags.Empty;
            }

            return new WindowsTerminalGlobalFlags(
                Bool(root, "autoHideWindow"),
                Bool(root, "minimizeToNotificationArea"),
                Bool(root, "alwaysShowTabs"),
                Bool(root, "showTabsInTitlebar"));
        }
        catch (JsonException)
        {
            return WindowsTerminalGlobalFlags.Empty;
        }
        catch (IOException)
        {
            return WindowsTerminalGlobalFlags.Empty;
        }

        static bool? Bool(JsonElement obj, string key)
            => obj.TryGetProperty(key, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? value.GetBoolean()
                : null;
    }
}
