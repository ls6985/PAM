using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PAM.Core;

/// <summary>安装字体的结果。</summary>
public sealed record FontInstallResult(bool Success, string Message);

/// <summary>
/// 字体工具：枚举系统已安装字体（注册表，不引 System.Drawing）、判断内置字体是否已装、
/// 按"当前用户"安装字体（免管理员权限）。
/// <para>
/// 当前用户安装 = 复制 ttf 到 <c>%LOCALAPPDATA%\Microsoft\Windows\Fonts</c> +
/// 在 HKCU 的 Fonts 键登记 + 广播 WM_FONTCHANGE。
/// </para>
/// </summary>
public static class FontInstaller
{
    /// <summary>内置字体（PAM.App/Assets/Fonts 里打包的 Cascadia Code NF）的家族名。</summary>
    public const string BundledFontFamily = "Cascadia Code NF";

    private const string FontsKeyPath = @"Software\Microsoft\Windows NT\CurrentVersion\Fonts";
    private const string TrueTypeSuffix = " (TrueType)";

    private const int HwndBroadcast = 0xFFFF;
    private const int WmFontChange = 0x001D;
    private const int SmtoAbortIfHung = 0x0002;

    private static string UserFontsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft", "Windows", "Fonts");

    // --- 枚举 ---

    /// <summary>HKLM + HKCU Fonts 键里登记的全部字体名（剥掉 " (TrueType)" 后缀）。</summary>
    public static IReadOnlyList<string> GetInstalledFontNames()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        CollectFrom(Registry.LocalMachine, names);
        CollectFrom(Registry.CurrentUser, names);

        return [.. names];
    }

    /// <summary>适合终端的字体候选：按 Nerd Font / 等宽启发式过滤已安装字体。</summary>
    public static IReadOnlyList<string> GetMonospaceCandidates()
        => GetInstalledFontNames().Where(IsMonospaceOrNerdCandidate).ToList();

    /// <summary>内置的 Cascadia Code NF 是否已经安装（含 CaskaydiaCove NF 这类社区补丁名）。</summary>
    public static bool IsBundledFontInstalled()
    {
        if (File.Exists(Path.Combine(UserFontsDirectory, "CascadiaCodeNF.ttf")))
        {
            return true;
        }

        foreach (var name in GetInstalledFontNames())
        {
            var normalized = Normalize(name);
            if (normalized.Contains("cascadiacodenf", StringComparison.Ordinal) ||
                normalized.Contains("caskaydiacovenf", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 终端字体候选启发式：Nerd Font 家族名、常见等宽字体名，或以 NF 结尾的补丁字体。
    /// 只影响下拉列表的过滤，装好的字体一定出现在列表里。
    /// </summary>
    public static bool IsMonospaceOrNerdCandidate(string fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName))
        {
            return false;
        }

        var lower = fontName.ToLowerInvariant();

        if (lower.EndsWith(" nf") || lower.EndsWith("nf"))
        {
            return true;
        }

        return lower.Contains("nerd") ||
               lower.Contains("mono") ||
               lower.Contains("cascadia") ||
               lower.Contains("caskaydia") ||
               lower.Contains("consolas") ||
               lower.Contains("console") ||
               lower.Contains("source code") ||
               lower.Contains("courier") ||
               lower.Contains("jetbrains") ||
               lower.Contains("fira") ||
               lower.Contains("hack") ||
               lower.Contains("iosevka") ||
               lower.Contains("sarasa") ||
               lower.Contains("更纱") ||
               lower.Contains("inconsolat") ||
               lower.Contains("menlo") ||
               lower.Contains("dejavu") ||
               lower.Contains("liberation") ||
               lower.Contains("maple") ||
               lower.Contains("victor") ||
               lower.Contains("fantasque") ||
               lower.Contains("powerline");
    }

    // --- 安装 ---

    /// <summary>按当前用户安装 ttf 字体文件，免管理员权限。同名文件会被覆盖。</summary>
    public static FontInstallResult InstallFont(string ttfPath)
    {
        if (string.IsNullOrWhiteSpace(ttfPath) || !File.Exists(ttfPath))
        {
            return new FontInstallResult(false, $"找不到字体文件：{ttfPath}");
        }

        if (!string.Equals(Path.GetExtension(ttfPath), ".ttf", StringComparison.OrdinalIgnoreCase))
        {
            return new FontInstallResult(false, "只支持 .ttf 字体文件。");
        }

        try
        {
            Directory.CreateDirectory(UserFontsDirectory);
            var destination = Path.Combine(UserFontsDirectory, Path.GetFileName(ttfPath));
            File.Copy(ttfPath, destination, overwrite: true);

            using (var key = Registry.CurrentUser.CreateSubKey(FontsKeyPath, writable: true))
            {
                var valueName = Path.GetFileNameWithoutExtension(ttfPath) + TrueTypeSuffix;
                key?.SetValue(valueName, destination);
            }

            // 通知正在运行的程序字体已变化（资源管理器、终端等）。
            SendMessageTimeout(
                (IntPtr)HwndBroadcast,
                WmFontChange,
                IntPtr.Zero,
                IntPtr.Zero,
                SmtoAbortIfHung,
                1000,
                out _);

            return new FontInstallResult(true, $"已为当前用户安装 {Path.GetFileNameWithoutExtension(ttfPath)}。");
        }
        catch (Exception ex)
        {
            return new FontInstallResult(false, $"安装字体失败：{ex.Message}");
        }
    }

    private static void CollectFrom(RegistryKey root, SortedSet<string> names)
    {
        try
        {
            using var key = root.OpenSubKey(FontsKeyPath);
            if (key?.GetValueNames() is null)
            {
                return;
            }

            foreach (var valueName in key.GetValueNames())
            {
                if (valueName.Length == 0)
                {
                    continue;
                }

                var name = valueName.EndsWith(TrueTypeSuffix, StringComparison.OrdinalIgnoreCase)
                    ? valueName[..^TrueTypeSuffix.Length]
                    : valueName;

                if (name.Length > 0)
                {
                    names.Add(name);
                }
            }
        }
        catch
        {
            // 注册表读取失败（如策略限制）只影响候选列表的完整度，不让调用方崩溃。
        }
    }

    private static string Normalize(string fontName)
        => fontName.ToLowerInvariant().Replace(" ", string.Empty, StringComparison.Ordinal);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        int flags,
        int timeout,
        out IntPtr result);
}
