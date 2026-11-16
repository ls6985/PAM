using System.Diagnostics;
using PAM.Core;

namespace PAM.Tray;

/// <summary>
/// 托盘后台组件：单实例保护、<see cref="NotifyIcon" /> 托盘图标、全局热键注册与设置热重载。
/// 由 <c>PAM.App</c> 拉起，也可单独运行；除菜单里的"显示主界面"外，不主动联系主界面进程。
/// </summary>
internal sealed class TrayApplication : IDisposable
{
    /// <summary>互斥体名称，用于限制同一会话内只运行一个实例。</summary>
    private const string MutexName = @"Local\PAM.Tray.SingleInstance";

    /// <summary>主界面进程名（不含扩展名）与可执行文件名。</summary>
    private const string AppProcessName = "PAM.App";
    private const string AppFileName = "PAM.App.exe";

    private readonly SettingsService _settingsService = new();

    private AppSettings _settings = new();
    private DateTime _lastSettingsWrite = DateTime.MinValue;
    private HotkeyManager? _hotkeyManager;
    private TerminalPool? _terminalPool;
    private TrayBridge.Listener? _listener;
    private NotifyIcon? _notifyIcon;
    private Icon? _trayIcon;
    private ToolStripMenuItem? _showMainItem;
    private ToolStripMenuItem? _reloadItem;
    private ToolStripMenuItem? _exitItem;
    private ContextMenuStrip? _menu;
    private System.Windows.Forms.Timer? _monitor;
    private bool _hotkeyRegistered;
    private bool _running;
    private string? _pendingBalloon;

    private static string AppName => L("PowerShell 管理器");

    /// <summary>
    /// 启动托盘消息循环。
    /// 已有实例时静默返回：本组件只是后台进程，重复拉起没有提示价值，提示由 <c>PAM.App</c> 负责。
    /// </summary>
    public void Run()
    {
        Mutex? mutex;
        try
        {
            mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            if (!createdNew)
            {
                mutex.Dispose();
                return;
            }
        }
        catch
        {
            // 互斥体异常（例如上一个实例崩溃后无法接管）时同样不作为第二实例运行，避免重复图标与热键。
            return;
        }

        try
        {
            Start();
        }
        finally
        {
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }

    private void Start()
    {
        _settings = _settingsService.Load();
        _lastSettingsWrite = _settingsService.GetLastWriteTimeUtc();
        LocalizationService.Instance.SetLanguage(_settings.Language);

        _hotkeyManager = new HotkeyManager();
        _terminalPool = new TerminalPool();
        _hotkeyManager.Pressed += (_, _) => LaunchTerminal();
        ApplyHotkey(showBalloon: true);

        _showMainItem = new ToolStripMenuItem(L("显示主界面"));
        _showMainItem.Click += (_, _) => ShowMainWindow();
        _reloadItem = new ToolStripMenuItem(L("重新加载设置"));
        _reloadItem.Click += (_, _) => ReloadSettings(showFeedback: true);
        _exitItem = new ToolStripMenuItem(L("退出"));
        _exitItem.Click += (_, _) => ConfirmExit();

        _menu = new ContextMenuStrip();
        _menu.Items.Add(_showMainItem);
        _menu.Items.Add(_reloadItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Text = AppName,
            ContextMenuStrip = _menu,
            Visible = true,
        };
        UpdateNotifyText();
        if (_pendingBalloon is not null)
        {
            ShowBalloon(_pendingBalloon);
            _pendingBalloon = null;
        }

        _listener = new TrayBridge.Listener();

        // UI 线程定时器：消费跨进程信号，并在用户直接编辑 settings.json 时重载配置。
        _monitor = new System.Windows.Forms.Timer { Interval = 700 };
        _monitor.Tick += Monitor_Tick;
        _monitor.Start();

        _running = true;

        // 无主窗体的后台常驻消息循环；由“退出”菜单项或 PAM.App 的退出信号触发 Application.Exit()。
        Application.Run(new ApplicationContext());
    }

    private static string L(string source) => LocalizationService.Instance.T(source);

    /// <summary>
    /// 渲染与主界面标题栏一致的 &lt;/&gt; 代码符号图标（GDI+ 自绘，不依赖字体）；绘制异常时回退系统默认图标。
    /// </summary>
    private Icon CreateTrayIcon()
    {
        var icon = TrayIconRenderer.Create(TrayIconRenderer.ShouldUseLightGlyph(_settings.Theme));
        _trayIcon = icon;
        return icon ?? SystemIcons.Application;
    }

    /// <summary>主题可能变化时重绘托盘图标（重新加载设置后调用）。</summary>
    private void RefreshTrayIcon()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        var previous = _trayIcon;
        _notifyIcon.Icon = CreateTrayIcon();
        previous?.Dispose();
    }

    /// <summary>按当前设置重新注册全局热键；系统保留组合不允许注册。</summary>
    private void ApplyHotkey(bool showBalloon)
    {
        if (_hotkeyManager is null) return;

        var chord = HotkeyChord.Parse(_settings.Hotkey)
                    ?? HotkeyChord.Parse(AppSettings.DefaultHotkey)!;

        var reservation = HotkeyChord.GetSystemReservation(chord);
        if (reservation is not null)
        {
            _hotkeyManager.Unregister();
            _hotkeyRegistered = false;
            if (showBalloon) ShowBalloon(L(reservation));
            return;
        }

        _hotkeyRegistered = _hotkeyManager.TryRegister((uint)chord.Modifiers, (uint)chord.VirtualKey);
        UpdateNotifyText();

        if (!_hotkeyRegistered && showBalloon)
        {
            ShowBalloon(LocalizationService.Instance.F(
                "热键 {0} 注册失败，可能已被系统或其他软件占用。请到“设置”中更换。",
                chord.Display));
        }
    }

    /// <summary>重新读取设置并同步热键、终端与语言。</summary>
    private void ReloadSettings(bool showFeedback)
    {
        _settings = _settingsService.Load();
        _lastSettingsWrite = _settingsService.GetLastWriteTimeUtc();
        LocalizationService.Instance.SetLanguage(_settings.Language);

        ApplyHotkey(showBalloon: true);
        RefreshMenuTexts();
        RefreshTrayIcon();
        UpdateNotifyText();

        if (showFeedback && _hotkeyRegistered)
        {
            ShowBalloon(LocalizationService.Instance.F("设置已重新加载：热键 {0}", _settings.Hotkey));
        }
    }

    private void Monitor_Tick(object? sender, EventArgs e)
    {
        if (_listener is null) return;

        if (_listener.ConsumeExitRequested())
        {
            Application.Exit();
            return;
        }

        var signaled = _listener.ConsumeSettingsChanged();
        var writeTime = _settingsService.GetLastWriteTimeUtc();
        if (signaled || writeTime != _lastSettingsWrite)
        {
            ReloadSettings(showFeedback: false);
        }

        // 打开终端后自动切英文，由托盘在已有的定时器里完成；开关关闭时完全不碰前台窗口。
        if (_settings.SwitchImeToEnglish)
        {
            ImeSwitcher.CheckForegroundWindow();
        }

        // 单窗口通道的找回、存活校验与配置新鲜度校验共用这一个定时器，不新增计时器。
        _terminalPool?.Tick(_settings);
    }

    private void RefreshMenuTexts()
    {
        if (_showMainItem is not null) _showMainItem.Text = L("显示主界面");
        if (_reloadItem is not null) _reloadItem.Text = L("重新加载设置");
        if (_exitItem is not null) _exitItem.Text = L("退出");
    }

    private void UpdateNotifyText()
    {
        if (_notifyIcon is null) return;

        var hotkey = _hotkeyRegistered ? _settings.Hotkey : L("热键被占用");
        var text = AppName + "\n" + LocalizationService.Instance.F("热键：{0}", hotkey);

        // NotifyIcon.Text 上限 63 字符，超长会被系统截断并抛异常。
        _notifyIcon.Text = text.Length > 63 ? text[..63] : text;
    }

    private void ShowBalloon(string message)
    {
        if (_notifyIcon is null)
        {
            // 启动阶段图标还没建好，等托盘就绪后再补显示。
            _pendingBalloon = message;
            return;
        }

        _notifyIcon.ShowBalloonTip(5000, AppName, message, ToolTipIcon.Warning);
    }

    /// <summary>
    /// 托盘菜单“显示主界面”：未运行则启动 PAM.App，已运行则激活其主窗口。
    /// </summary>
    private void ShowMainWindow()
    {
        var app = FindAppProcess();
        if (app is null)
        {
            StartMainApp();
            return;
        }

        using (app)
        {
            app.Refresh();
            var handle = app.MainWindowHandle;
            if (handle == IntPtr.Zero)
            {
                // 进程已存在但主窗口尚未创建（例如刚启动），不重复拉起。
                return;
            }

            NativeMethods.ShowWindow(handle,
                NativeMethods.IsIconic(handle) ? NativeMethods.SwRestore : NativeMethods.SwShow);

            // 系统的前台窗口限制可能导致置顶失败，尽力而为即可。
            NativeMethods.SetForegroundWindow(handle);
        }
    }

    /// <summary>
    /// 查找已运行的 PAM.App 进程，优先返回已创建主窗口的实例。
    /// </summary>
    private static Process? FindAppProcess()
    {
        Process? chosen = null;

        foreach (var process in Process.GetProcessesByName(AppProcessName))
        {
            process.Refresh();
            bool hasWindow = process.MainWindowHandle != IntPtr.Zero;

            if (chosen is null || (hasWindow && chosen.MainWindowHandle == IntPtr.Zero))
            {
                // 后续遇到“有窗口”的实例时，替换掉先前“无窗口”的候选。
                chosen?.Dispose();
                chosen = process;
            }
            else
            {
                process.Dispose();
            }
        }

        return chosen;
    }

    /// <summary>
    /// 启动与 PAM.Tray.exe 同目录的 PAM.App.exe。
    /// </summary>
    private void StartMainApp()
    {
        var exePath = Path.Combine(AppContext.BaseDirectory, AppFileName);

        try
        {
            if (!File.Exists(exePath))
            {
                MessageBox.Show(LocalizationService.Instance.F("未找到主界面程序：{0}", exePath), AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exePath),
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(LocalizationService.Instance.F("启动主界面失败：{0}", ex.Message), AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 热键触发时激活长期持有的终端窗口；窗口不存在时冷启动一个，
    /// 随后由单窗口通道纳管，之后的按键都复用同一个窗口。两条路径都使用 Windows Terminal。
    /// </summary>
    private void LaunchTerminal()
    {
        if (_terminalPool is not null && _terminalPool.Activate())
        {
            return;
        }

        LaunchTerminalCold();
    }

    /// <summary>
    /// 通过 <c>wt.exe</c> 冷启动默认终端。
    /// 通过 <c>-d</c> 显式指定工作目录为当前用户目录，避免默认落在 <c>C:\Windows\System32</c>。
    /// Shell 由设置里的默认终端决定（<see cref="ShellResolver"/>），未安装 PowerShell 7 时自动回退。
    /// </summary>
    private void LaunchTerminalCold()
    {
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var shell = ShellResolver.Resolve(_settings.TerminalType, out _);
            var psi = new ProcessStartInfo
            {
                FileName = ShellResolver.WindowsTerminalFileName,
                // -d 指定终端启动的初始目录为 ~；-NoProfile 与预热窗口共用同一份命令行形状
                Arguments = $"-d \"{userProfile}\" {shell} -NoProfile -NoExit -Command \". $PROFILE\"",
                UseShellExecute = true,
                WorkingDirectory = userProfile,
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show(LocalizationService.Instance.F("启动 Windows Terminal 失败：{0}", ex.Message), AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 二次确认后退出程序。
    /// </summary>
    private void ConfirmExit()
    {
        var result = MessageBox.Show(
            LocalizationService.Instance.F("确定要退出 {0} 吗？", AppName),
            AppName,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (result == DialogResult.Yes)
        {
            Application.Exit();
        }
    }

    /// <summary>释放托盘资源：<see cref="Application.Run"/> 返回后由 <c>using</c> 调用。</summary>
    public void Dispose()
    {
        if (!_running) return;
        _running = false;

        _monitor?.Stop();
        _monitor?.Dispose();
        _terminalPool?.Dispose();
        _terminalPool = null;
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        _trayIcon?.Dispose();
        _trayIcon = null;
        _menu?.Dispose();
        _listener?.Dispose();
        _hotkeyManager?.Dispose();

        _monitor = null;
        _notifyIcon = null;
        _menu = null;
        _listener = null;
        _hotkeyManager = null;
    }
}
