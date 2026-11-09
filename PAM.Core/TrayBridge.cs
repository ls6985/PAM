using System.Diagnostics;

namespace PAM.Core;

/// <summary>
/// PAM.App 与 PAM.Tray 之间的轻量进程间协作：
/// 两个手动重置的命名事件（设置变更 / 请求退出）+ 托盘进程的探测与启动。
/// 不使用 Socket / 管道，也不需要管理员权限；同一用户会话内即可工作。
/// </summary>
public static class TrayBridge
{
    public const string TrayProcessName = "PAM.Tray";
    public const string TrayFileName = "PAM.Tray.exe";

    /// <summary>与当前进程同目录的托盘 exe 路径，启动探测与登录启动项共用同一份解析结果。</summary>
    public static string TrayExePath => Path.Combine(AppContext.BaseDirectory, TrayFileName);

    private const string SettingsChangedEventName = @"Local\PAM.SettingsChanged";
    private const string ExitRequestedEventName = @"Local\PAM.Tray.ExitRequested";

    /// <summary>通知后台托盘重新读取 settings.json（热键、终端、语言）。</summary>
    public static void SignalSettingsChanged() => Signal(SettingsChangedEventName);

    /// <summary>通知后台托盘退出（"关闭管理器时不保留后台运行"）。</summary>
    public static void SignalExit() => Signal(ExitRequestedEventName);

    /// <summary>管理器退出时调用：按当前设置决定是否结束托盘。</summary>
    public static void HandleManagerClosing(AppSettings settings)
    {
        if (!settings.KeepTrayOnClose) SignalExit();
    }

    /// <summary>后台托盘是否正在运行。</summary>
    public static bool IsTrayRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName(TrayProcessName);
            var running = processes.Length > 0;
            foreach (var process in processes) process.Dispose();
            return running;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 确保后台托盘在运行。托盘 exe 不存在（例如仅单独运行 PAM.App 的调试环境）时返回 false。
    /// </summary>
    public static bool EnsureTrayRunning()
    {
        if (IsTrayRunning()) return true;

        var exePath = TrayExePath;
        if (!File.Exists(exePath)) return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                WorkingDirectory = AppContext.BaseDirectory,
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void Signal(string name)
    {
        try
        {
            using var handle = new EventWaitHandle(false, EventResetMode.ManualReset, name, out _);
            handle.Set();
        }
        catch
        {
            // 信号只是加速同步；托盘还会轮询 settings.json，发送失败不影响设置保存。
        }
    }

    /// <summary>托盘侧的监听器：由 UI 线程定时轮询，避免跨线程操作 WinForms 控件。</summary>
    public sealed class Listener : IDisposable
    {
        private readonly EventWaitHandle _settingsChanged;
        private readonly EventWaitHandle _exitRequested;

        public Listener()
        {
            _settingsChanged = new EventWaitHandle(false, EventResetMode.ManualReset,
                SettingsChangedEventName, out _);
            _exitRequested = new EventWaitHandle(false, EventResetMode.ManualReset,
                ExitRequestedEventName, out _);
        }

        public bool ConsumeSettingsChanged() => Consume(_settingsChanged);

        public bool ConsumeExitRequested() => Consume(_exitRequested);

        private static bool Consume(EventWaitHandle handle)
        {
            if (!handle.WaitOne(0)) return false;
            handle.Reset();
            return true;
        }

        public void Dispose()
        {
            _settingsChanged.Dispose();
            _exitRequested.Dispose();
        }
    }
}
