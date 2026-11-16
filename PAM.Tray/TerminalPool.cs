using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PAM.Core;

namespace PAM.Tray;

/// <summary>
/// 单窗口终端通道：托盘长期持有一个 Windows Terminal 窗口，热键只负责显示与激活，
/// 不再每次新开窗口。预热窗口以初始隐藏状态创建；托盘退出不关闭用户正在使用的窗口，
/// 重启时按跨进程窗口属性找回。配置变化只在窗口尚未显示过时触发后台重建。
/// </summary>
internal sealed class TerminalPool : IDisposable
{
    /// <summary>跨进程窗口属性名，用于托盘重启后找回同一个窗口。</summary>
    private const string WindowPropName = "PAM.Tray.TerminalWindow";

    /// <summary>登录后延迟预热的时长，避开登录瞬间的资源竞争。</summary>
    private static readonly TimeSpan WarmupDelay = TimeSpan.FromSeconds(4);

    /// <summary>等待新窗口句柄出现的最长时间。</summary>
    private static readonly TimeSpan WindowTimeout = TimeSpan.FromSeconds(6);

    private readonly string[] _watchedPaths;

    private IntPtr _windowHandle;
    private bool _creating;
    private bool _showOnReady;
    private bool _orphansScanned;
    private bool _disposed;
    private bool _hasBeenShown;
    private DateTime _warmupAfterUtc;
    private FileStamp[] _snapshot = [];

    public TerminalPool()
    {
        _warmupAfterUtc = DateTime.UtcNow + WarmupDelay;
        _watchedPaths = [new SettingsService().SettingsFilePath, new PamDataService().DataFilePath];
    }

    /// <summary>
    /// 由托盘 700ms 定时器调用：找回已存在的窗口、校验存活与配置新鲜度，
    /// 并在尚未持有窗口时按开关安排后台预热。
    /// </summary>
    public void Tick(AppSettings settings)
    {
        if (_disposed) return;

        if (!_orphansScanned)
        {
            _orphansScanned = true;
            _windowHandle = NativeMethods.FindMarkedWindow(WindowPropName);
        }

        if (_windowHandle != IntPtr.Zero && !NativeMethods.IsWindow(_windowHandle))
        {
            _windowHandle = IntPtr.Zero;
            _hasBeenShown = false;
            _snapshot = [];
        }

        // 只有从未显示过的预热窗口才允许按配置变化替换；显示过之后被 Windows Terminal
        // 自动隐藏的窗口不能当成预热态，否则会被误关重建。
        if (_windowHandle != IntPtr.Zero
            && !_hasBeenShown
            && !NativeMethods.IsWindowVisible(_windowHandle)
            && IsStale())
        {
            CloseWindow(_windowHandle);
            _windowHandle = IntPtr.Zero;
            _hasBeenShown = false;
            _snapshot = [];
        }

        if (_windowHandle != IntPtr.Zero || _creating) return;
        if (!settings.WarmPoolEnabled) return;
        if (DateTime.UtcNow < _warmupAfterUtc) return;

        var shell = ShellResolver.Resolve(settings.TerminalType, out _);
        var workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var before = NativeMethods.FindTerminalWindows().ToHashSet();
        BeginCreate(() => CreateHiddenWindow(shell, workingDirectory, before));
    }

    /// <summary>
    /// 热键触发时激活持有的窗口。返回 false 表示当前没有窗口，由托盘冷启动一个；
    /// 此时已安排后台纳管，该窗口随后会成为长期持有的同一个窗口。
    /// </summary>
    public bool Activate()
    {
        if (_disposed) return false;

        if (_windowHandle != IntPtr.Zero && NativeMethods.IsWindow(_windowHandle))
        {
            ShowAndFocus(_windowHandle);
            return true;
        }

        if (_creating)
        {
            // 预热窗口正在创建，等它就绪后直接显示，避免同时再冷启动一个窗口。
            _showOnReady = true;
            return true;
        }

        _windowHandle = IntPtr.Zero;
        var before = NativeMethods.FindTerminalWindows().ToHashSet();
        BeginCreate(() => WaitForNewWindow(before));
        return false;
    }

    /// <summary>
    /// 在后台线程创建或定位窗口。调用方都在 UI 线程，await 的延续回到 UI 线程，
    /// 句柄与状态字段因此不需要额外的同步结构。
    /// </summary>
    private void BeginCreate(Func<IntPtr> create)
    {
        if (_creating || _disposed) return;
        _creating = true;

        // 快照在窗口创建之前记录，创建期间发生的配置变化会在下一次 tick 触发重建。
        _snapshot = CaptureStamps();
        _ = CreateAsync(create);
    }

    private async Task CreateAsync(Func<IntPtr> create)
    {
        var handle = await Task.Run(() =>
        {
            try
            {
                return create();
            }
            catch
            {
                return IntPtr.Zero;
            }
        });

        ApplyResult(handle);
    }

    /// <summary>后台创建一个初始隐藏的 Windows Terminal 窗口。</summary>
    private static IntPtr CreateHiddenWindow(string shell, string workingDirectory, HashSet<IntPtr> before)
    {
        // -NoProfile 让 $PROFILE 只由 . 显式加载一次，省去自动加载与 dot-source 的重复执行。
        var commandLine = new StringBuilder(
            $"wt.exe -d \"{workingDirectory}\" {shell} -NoProfile -NoExit -Command \". $PROFILE\"");

        var startupInfo = new NativeMethods.StartupInfo
        {
            cb = Marshal.SizeOf<NativeMethods.StartupInfo>(),
            dwFlags = NativeMethods.StartfUseShowWindow,
            wShowWindow = (short)NativeMethods.SwHide,
        };

        if (!NativeMethods.CreateProcess(null, commandLine, IntPtr.Zero, IntPtr.Zero,
                false, 0, IntPtr.Zero, workingDirectory,
                ref startupInfo, out var processInfo))
        {
            return IntPtr.Zero;
        }

        NativeMethods.CloseHandle(processInfo.hProcess);
        NativeMethods.CloseHandle(processInfo.hThread);
        return WaitForNewWindow(before);
    }

    private static IntPtr WaitForNewWindow(HashSet<IntPtr> before)
    {
        var deadline = DateTime.UtcNow + WindowTimeout;
        while (DateTime.UtcNow < deadline)
        {
            foreach (var handle in NativeMethods.FindTerminalWindows())
            {
                if (!before.Contains(handle)) return handle;
            }

            Thread.Sleep(20);
        }

        return IntPtr.Zero;
    }

    private void ApplyResult(IntPtr handle)
    {
        _creating = false;

        if (_disposed)
        {
            if (handle != IntPtr.Zero && !NativeMethods.IsWindowVisible(handle))
            {
                CloseWindow(handle);
            }

            _showOnReady = false;
            return;
        }

        if (handle == IntPtr.Zero)
        {
            _showOnReady = false;
            return;
        }

        if (_windowHandle != IntPtr.Zero)
        {
            // 已持有窗口时，多余的隐藏窗口直接关闭，保证始终只有一个托盘窗口。
            if (!NativeMethods.IsWindowVisible(handle)) CloseWindow(handle);
            if (_showOnReady)
            {
                _showOnReady = false;
                ShowAndFocus(_windowHandle);
            }

            return;
        }

        _windowHandle = handle;
        _hasBeenShown = NativeMethods.IsWindowVisible(handle);
        NativeMethods.MarkWindow(handle, WindowPropName);

        if (_showOnReady)
        {
            _showOnReady = false;
            ShowAndFocus(handle);
        }
    }

    /// <summary>显示并激活窗口；已在前台时调用是幂等的，不会隐藏或闪烁。</summary>
    private void ShowAndFocus(IntPtr handle)
    {
        _hasBeenShown = true;
        if (NativeMethods.IsIconic(handle))
        {
            NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
        }
        else if (!NativeMethods.IsWindowVisible(handle))
        {
            NativeMethods.ShowWindow(handle, NativeMethods.SwShow);
        }

        if (!NativeMethods.IsWindowVisible(handle))
        {
            NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
        }

        // 前台窗口限制可能导致置顶失败，尽力而为即可。
        NativeMethods.SetForegroundWindow(handle);
    }

    private static void CloseWindow(IntPtr handle)
    {
        if (handle != IntPtr.Zero && NativeMethods.IsWindow(handle))
        {
            NativeMethods.PostMessage(handle, NativeMethods.WmClose, IntPtr.Zero, IntPtr.Zero);
        }
    }

    /// <summary>配置快照比对：任一受监视文件的写入时间或长度变化即视为过期。</summary>
    private bool IsStale()
    {
        return _snapshot.Length == 0 || !_snapshot.SequenceEqual(CaptureStamps());
    }

    private FileStamp[] CaptureStamps()
    {
        var stamps = new FileStamp[_watchedPaths.Length];
        for (var i = 0; i < _watchedPaths.Length; i++)
        {
            try
            {
                var info = new FileInfo(_watchedPaths[i]);
                stamps[i] = new FileStamp(
                    info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue,
                    info.Exists ? info.Length : -1);
            }
            catch
            {
                stamps[i] = new FileStamp(DateTime.MinValue, -1);
            }
        }

        return stamps;
    }

    /// <summary>窗口可能正被用户使用，托盘退出时保留它，下次启动按窗口属性找回。</summary>
    public void Dispose()
    {
        _disposed = true;
    }

    private sealed record FileStamp(DateTime LastWriteUtc, long Length);
}
