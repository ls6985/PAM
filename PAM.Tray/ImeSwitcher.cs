using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace PAM.Tray;

/// <summary>
/// 由常驻托盘在检测到新打开的终端窗口时，把当前中文输入法切到「英文输入模式」。
///
/// 约束：保持原输入法与键盘布局不变（微软拼音 / 搜狗等仅切换中英状态），
/// 因此不使用 WM_INPUTLANGCHANGEREQUEST / LoadKeyboardLayout / ActivateKeyboardLayout 等切换布局的 API。
///
/// Windows Terminal（CASCADIA_HOSTING_WINDOW_CLASS）基于 TSF：跨进程调用 ImmGetContext 返回 0（himc=0x0），
/// WM_IME_CONTROL 亦不生效；IMM32 / TSF compartment 具有线程局部性，托盘进程无法直接修改其他进程的输入状态。
/// 实现采用输入法可响应的系统级方式：
///   1) 通过 UI Automation 读取任务栏输入指示器名称，判定当前中 / 英模式；
///   2) 仅当处于中文模式时，向前台窗口模拟一次 Shift（主流中文输入法的中英切换键）；
///   3) 短延迟后复查，仍为中文则重试，最多 3 轮。
/// 该流程幂等，不更换输入法或键盘布局，且不会将已处于英文模式的窗口切回中文。
/// 不写入 $PROFILE、不注册全局钩子，复用 TrayApplication 的 700ms 定时器轮询前台窗口。
/// </summary>
internal static class ImeSwitcher
{
    private const int MaxClassName = 256;
    private const byte VkShift = 0x10;
    private const uint KeyEventFKeyUp = 0x0002;

    private enum ImeMode
    {
        Unknown,
        Chinese,
        English,
    }

    /// <summary>已经成功处理（或达到重试上限）的窗口，不再打扰。</summary>
    private static readonly HashSet<IntPtr> HandledWindows = [];

    /// <summary>读不到指示器时允许后续 tick 重试，避免任务栏尚未刷新就永久放弃。</summary>
    private static readonly Dictionary<IntPtr, int> Attempts = [];

    /// <summary>检查当前前台窗口；若是新出现的终端窗口则尝试切换一次输入法模式。</summary>
    public static void CheckForegroundWindow()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero || HandledWindows.Contains(hwnd)) return;

        var className = GetWindowClass(hwnd);
        if (!IsTerminalWindow(className)) return;

        if (HandledWindows.Count > 64)
        {
            // 清掉已关闭窗口的句柄，避免集合随使用时间无限增长。
            HandledWindows.RemoveWhere(handle => !IsWindow(handle));
            foreach (var stale in Attempts.Keys.Where(handle => !IsWindow(handle)).ToList())
            {
                Attempts.Remove(stale);
            }
        }

        var before = ReadImeMode();
        var result = before;
        var taps = 0;

        if (before == ImeMode.Chinese)
        {
            // 每按一次都要重新读状态：只在仍为「中文模式」时才继续，绝不盲按第二次。
            for (var i = 0; i < 2; i++)
            {
                TapShift();
                taps++;
                Thread.Sleep(180);
                result = ReadImeMode();
                if (result != ImeMode.Chinese) break;
            }
        }

        WriteLog(className, hwnd, before, taps, result);

        // 成功切到英文、本来就是英文、或读不到指示器但已试满 3 轮，就标记为已处理。
        var attempts = Attempts.GetValueOrDefault(hwnd) + 1;
        if (result != ImeMode.Chinese || before != ImeMode.Chinese || attempts >= 3)
        {
            HandledWindows.Add(hwnd);
            Attempts.Remove(hwnd);
        }
        else
        {
            Attempts[hwnd] = attempts;
        }
    }

    /// <summary>读任务栏「托盘输入指示器」的名称，判断当前前台窗口的 IME 中/英模式。</summary>
    private static ImeMode ReadImeMode()
    {
        try
        {
            var root = AutomationElement.RootElement;
            var condition = new PropertyCondition(
                AutomationElement.ClassNameProperty, "Shell_TrayWnd");
            var tray = root.FindFirst(TreeScope.Children, condition);
            if (tray is null) return ImeMode.Unknown;

            var descendants = tray.FindAll(TreeScope.Descendants, Condition.TrueCondition);
            foreach (AutomationElement element in descendants)
            {
                if (element.Current.ClassName != "SystemTray.NormalButton") continue;

                var name = element.Current.Name;
                if (string.IsNullOrEmpty(name) || !IsInputIndicator(name)) continue;

                // 中文系统：中文模式 / 英文(英语)模式；英文系统：Chinese / English。
                if (name.Contains("中文", StringComparison.Ordinal)
                    || name.Contains("Chinese", StringComparison.OrdinalIgnoreCase))
                {
                    return ImeMode.Chinese;
                }

                if (name.Contains("英文", StringComparison.Ordinal)
                    || name.Contains("英语", StringComparison.Ordinal)
                    || name.Contains("English", StringComparison.OrdinalIgnoreCase))
                {
                    return ImeMode.English;
                }

                return ImeMode.Unknown;
            }
        }
        catch
        {
            // UIA 可能因任务栏重启 / 权限 / 会话状态抛异常，诊断日志里记为 Unknown 即可。
        }

        return ImeMode.Unknown;
    }

    private static bool IsInputIndicator(string name)
        => name.Contains("指示器", StringComparison.Ordinal)
           || name.Contains("Input Indicator", StringComparison.OrdinalIgnoreCase)
           || name.Contains("IME", StringComparison.OrdinalIgnoreCase);

    /// <summary>模拟单独按一次 Shift；不带字符，输入法会把它当作中/英切换键。</summary>
    private static void TapShift()
    {
        keybd_event(VkShift, 0, 0, UIntPtr.Zero);
        keybd_event(VkShift, 0, KeyEventFKeyUp, UIntPtr.Zero);
    }

    /// <summary>仅保留最近一次诊断记录，用于定位切换未生效的场景，避免日志持续增长。</summary>
    private static void WriteLog(string className, IntPtr hwnd, ImeMode before, int taps, ImeMode after)
    {
        try
        {
            var line = string.Concat(
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                " class=", className,
                " hwnd=0x", hwnd.ToString("X"),
                " before=", before.ToString(),
                " taps=", taps.ToString(),
                " after=", after.ToString());

            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PAM");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "ime-switch.log"), line + Environment.NewLine);
        }
        catch
        {
            // 诊断日志写失败绝不能影响托盘与主流程。
        }
    }

    private static string GetWindowClass(IntPtr hwnd)
    {
        var buffer = new StringBuilder(MaxClassName);
        return GetClassName(hwnd, buffer, buffer.Capacity) > 0 ? buffer.ToString() : string.Empty;
    }

    private static bool IsTerminalWindow(string className)
        => className.Contains("CASCADIA", StringComparison.OrdinalIgnoreCase)
           || string.Equals(className, "ConsoleWindowClass", StringComparison.OrdinalIgnoreCase);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
