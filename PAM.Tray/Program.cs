using System.Diagnostics;
using PAM.Core;

namespace PAM.Tray;

/// <summary>
/// 托盘程序入口：单实例保护、全局热键注册与托盘菜单。
/// 热键 / 默认终端 / 界面语言全部来自 <c>%AppData%\PAM\settings.json</c>，
/// 由 PAM.App 的设置页写入；这里通过命名事件 + 文件时间戳轮询实时同步。
/// </summary>
internal static class Program
{
    /// <summary>互斥体名称，用于限制同一会话内只运行一个实例。</summary>
    private const string MutexName = @"Local\PAM.Tray.SingleInstance";

    /// <summary>主界面进程名（不含扩展名）与可执行文件名。</summary>
    private const string AppProcessName = "PAM.App";
    private const string AppFileName = "PAM.App.exe";

    private static readonly SettingsService SettingsService = new();

    private static AppSettings _settings = new();
    private static DateTime _lastSettingsWrite = DateTime.MinValue;
    private static HotkeyManager? _hotkeyManager;
    private static TrayBridge.Listener? _listener;
    private static NotifyIcon? _notifyIcon;
    private static ToolStripMenuItem? _showMainItem;
    private static ToolStripMenuItem? _reloadItem;
    private static ToolStripMenuItem? _exitItem;
    private static bool _hotkeyRegistered;
    private static string? _pendingBalloon;

    private static string AppName => L("PowerShell 管理器");

    [STAThread]
    private static void Main()
    {
        // 单实例：已存在实例时直接退出，避免重复的托盘图标与热键注册冲突。
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(L("PowerShell 管理器已在运行，无需重复启动。"), AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        _settings = SettingsService.Load();
        _lastSettingsWrite = SettingsService.GetLastWriteTimeUtc();
        LocalizationService.Instance.SetLanguage(_settings.Language);

        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.Pressed += (_, _) => LaunchTerminal();
        ApplyHotkey(showBalloon: true);

        _showMainItem = new ToolStripMenuItem(L("显示主界面"));
        _showMainItem.Click += (_, _) => ShowMainWindow();
        _reloadItem = new ToolStripMenuItem(L("重新加载设置"));
        _reloadItem.Click += (_, _) => ReloadSettings(showFeedback: true);
        _exitItem = new ToolStripMenuItem(L("退出"));
        _exitItem.Click += (_, _) => ConfirmExit();

        using var menu = new ContextMenuStrip();
        menu.Items.Add(_showMainItem);
        menu.Items.Add(_reloadItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = AppName,
            ContextMenuStrip = menu,
            Visible = true,
        };
        UpdateNotifyText();
        if (_pendingBalloon is not null)
        {
            ShowBalloon(_pendingBalloon);
            _pendingBalloon = null;
        }

        _listener = new TrayBridge.Listener();

        // UI 线程上的定时器：消费跨进程信号，并在用户直接编辑 settings.json 时兜底重载。
        using var monitor = new System.Windows.Forms.Timer { Interval = 700 };
        monitor.Tick += Monitor_Tick;
        monitor.Start();

        // 无主窗体的后台常驻消息循环；由“退出”菜单项或 PAM.App 的退出信号触发 Application.Exit()。
        Application.Run(new ApplicationContext());

        monitor.Stop();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _listener.Dispose();
        _hotkeyManager.Dispose();
    }

    private static string L(string source) => LocalizationService.Instance.T(source);

    /// <summary>按当前设置重新注册全局热键；系统保留组合不允许注册。</summary>
    private static void ApplyHotkey(bool showBalloon)
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
    private static void ReloadSettings(bool showFeedback)
    {
        _settings = SettingsService.Load();
        _lastSettingsWrite = SettingsService.GetLastWriteTimeUtc();
        LocalizationService.Instance.SetLanguage(_settings.Language);

        ApplyHotkey(showBalloon: true);
        RefreshMenuTexts();
        UpdateNotifyText();

        if (showFeedback && _hotkeyRegistered)
        {
            ShowBalloon(LocalizationService.Instance.F("设置已重新加载：热键 {0}", _settings.Hotkey));
        }
    }

    private static void Monitor_Tick(object? sender, EventArgs e)
    {
        if (_listener is null) return;

        if (_listener.ConsumeExitRequested())
        {
            Application.Exit();
            return;
        }

        var signaled = _listener.ConsumeSettingsChanged();
        var writeTime = SettingsService.GetLastWriteTimeUtc();
        if (signaled || writeTime != _lastSettingsWrite)
        {
            ReloadSettings(showFeedback: false);
        }
    }

    private static void RefreshMenuTexts()
    {
        if (_showMainItem is not null) _showMainItem.Text = L("显示主界面");
        if (_reloadItem is not null) _reloadItem.Text = L("重新加载设置");
        if (_exitItem is not null) _exitItem.Text = L("退出");
    }

    private static void UpdateNotifyText()
    {
        if (_notifyIcon is null) return;

        var hotkey = _hotkeyRegistered ? _settings.Hotkey : L("热键被占用");
        var text = AppName + "\n" + LocalizationService.Instance.F("热键：{0}", hotkey);

        // NotifyIcon.Text 上限 63 字符，超长会被系统截断并抛异常。
        _notifyIcon.Text = text.Length > 63 ? text[..63] : text;
    }

    private static void ShowBalloon(string message)
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
    private static void ShowMainWindow()
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
    private static void StartMainApp()
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
    /// 注册的热键触发时启动 Windows Terminal。
    /// 通过 <c>-d</c> 显式指定工作目录为当前用户目录，避免默认落在 <c>C:\Windows\System32</c>。
    /// Shell 由设置里的默认终端决定（<see cref="ShellResolver"/>），未安装 PowerShell 7 时自动回退。
    /// </summary>
    private static void LaunchTerminal()
    {
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var shell = ShellResolver.Resolve(_settings.TerminalType, out _);
            var psi = new ProcessStartInfo
            {
                FileName = ShellResolver.WindowsTerminalFileName,
                // -d 指定终端启动的初始目录为 ~
                Arguments = $"-d \"{userProfile}\" {shell} -NoExit -Command \". $PROFILE\"",
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
    private static void ConfirmExit()
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
}
