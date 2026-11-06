using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using PAM.Core;

namespace PAM.App;

public enum HotkeyCheckLevel { Idle, Ok, Warning, Error }

public sealed record HotkeyCheckResult(
    HotkeyCheckLevel Level,
    string Message,
    bool CanApply,
    bool NeedsConfirmation);

/// <summary>
/// 热键冲突检测：先查系统保留组合，再用 Win32 RegisterHotKey 做真实占用探测，
/// 最后对"常见软件快捷键"给出软性提醒。探测窗口直接借用主窗口句柄，注册后立即注销。
/// </summary>
public static class HotkeyConflictDetector
{
    /// <summary>探测用的热键 ID，取值需在 0x0000~0xBFFF 之间且不与托盘的 0x1000 冲突。</summary>
    private const int ProbeHotkeyId = 0xBFFE;

    public static HotkeyCheckResult Idle() =>
        new(HotkeyCheckLevel.Idle, Loc.T("尚未检测，点击下方“检测冲突”。"), false, false);

    public static HotkeyCheckResult Check(HotkeyChord? chord, string appliedHotkey, bool trayRunning)
    {
        if (chord is null)
        {
            return new HotkeyCheckResult(HotkeyCheckLevel.Error, Loc.T("热键不能为空。"), false, false);
        }

        if (!HotkeyChord.IsValid(chord, out var validationError))
        {
            return new HotkeyCheckResult(HotkeyCheckLevel.Error, Loc.T(validationError), false, false);
        }

        var reserved = HotkeyChord.GetSystemReservation(chord);
        if (reserved is not null)
        {
            return new HotkeyCheckResult(
                HotkeyCheckLevel.Error,
                Loc.F("系统保留：{0}", Loc.T(reserved)),
                false,
                false);
        }

        // 托盘正持有这个组合时，从管理器进程再注册一次必然失败，属于"自己占用"而非冲突。
        if (trayRunning && string.Equals(chord.Display, appliedHotkey, StringComparison.OrdinalIgnoreCase))
        {
            return new HotkeyCheckResult(
                HotkeyCheckLevel.Ok,
                Loc.T("当前由 PAM 托盘占用，可继续使用。"),
                true,
                false);
        }

        if (!Probe(chord))
        {
            var hint = HotkeyChord.GetSoftwareHint(chord);
            var reason = Loc.T("该组合已被系统或其他常驻软件占用，请换一个。");
            if (hint is not null) reason += " " + Loc.T(hint);

            return new HotkeyCheckResult(
                HotkeyCheckLevel.Error,
                Loc.F("已被占用：{0}", reason),
                false,
                false);
        }

        var common = HotkeyChord.GetCommonAppShortcutWarning(chord);
        if (common is not null)
        {
            return new HotkeyCheckResult(
                HotkeyCheckLevel.Warning,
                Loc.F("常见软件快捷键提醒：{0}", Loc.T(common)),
                true,
                true);
        }

        return new HotkeyCheckResult(
            HotkeyCheckLevel.Ok,
            Loc.T("可用：该组合当前没有被系统或其他软件占用。"),
            true,
            false);
    }

    /// <summary>尝试在主窗口上临时注册该组合；失败即代表已被系统或其他进程占用。</summary>
    private static bool Probe(HotkeyChord chord)
    {
        var window = Application.Current?.MainWindow;
        if (window is null) return true; // 没有窗口时无法探测，交由托盘启动时再校验。

        try
        {
            var helper = new WindowInteropHelper(window);
            var handle = helper.Handle;
            if (handle == IntPtr.Zero) handle = helper.EnsureHandle();
            if (handle == IntPtr.Zero) return true;

            if (!RegisterHotKey(handle, ProbeHotkeyId, (uint)chord.Modifiers, (uint)chord.VirtualKey))
            {
                return false;
            }

            UnregisterHotKey(handle, ProbeHotkeyId);
            return true;
        }
        catch
        {
            return true;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
