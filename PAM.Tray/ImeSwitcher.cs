using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace PAM.Tray;

/// <summary>
/// 由常驻托盘在检测到新打开的终端窗口时，把当前中文输入法切到「英文输入模式」。
///
/// 用户要的是「输入法还是微软拼音 / 搜狗等，只是处于英文状态」，不是把整个输入法换成
/// 英文（美国）键盘，所以绝不能用 WM_INPUTLANGCHANGEREQUEST / LoadKeyboardLayout /
/// ActivateKeyboardLayout 这类换布局的 API。
///
/// Windows Terminal（CASCADIA_HOSTING_WINDOW_CLASS）走 TSF，跨进程调用 ImmGetContext 一律
/// 返回 0（实测 himc=0x0），WM_IME_CONTROL 也不被处理；IMM32 / TSF compartment 都是线程局部的，
/// 托盘进程无法直接改另一个进程的输入状态。因此这里采用系统级、输入法自己会响应的方式：
///   1) 用 UI Automation 读任务栏「托盘输入指示器」的名称判断当前是中/英模式；
///   2) 仅当是中文模式时，向前台窗口模拟一次单独的 Shift（主流中文输入法的中/英切换键）；
///   3) 短延迟后复查状态，仍是中文就再试一次，最多 3 轮；已是英文则完全不按键。
/// 这样幂等、不换输入法、不换键盘布局，也不会把已经处于英文模式的窗口翻回中文。
/// 不写 $PROFILE、不注册全局钩子：复用 TrayApplication 的 700ms 定时器轮询前台窗口。
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

    /// <summary>只保留最近一次诊断记录，方便排查「切了但没生效」而不用翻大日志。</summary>
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
