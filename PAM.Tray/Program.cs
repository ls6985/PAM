using System.Diagnostics;

namespace PAM.Tray;

/// <summary>
/// 托盘程序入口：单实例保护、全局热键注册与托盘菜单。
/// </summary>
internal static class Program
{
    /// <summary>互斥体名称，用于限制同一会话内只运行一个实例。</summary>
    private const string MutexName = @"Local\PAM.Tray.SingleInstance";

    private const string AppName = "PowerShell 管理器";

    /// <summary>Windows Terminal 可执行文件名。</summary>
    private const string TerminalFileName = "wt.exe";

    /// <summary>优先使用的 PowerShell 7 可执行文件名。</summary>
    private const string PwshFileName = "pwsh.exe";

    /// <summary>回退使用的 Windows PowerShell 5.1 可执行文件名。</summary>
    private const string PowerShellFileName = "powershell.exe";

    /// <summary>主界面进程名（不含扩展名）与可执行文件名。</summary>
    private const string AppProcessName = "PAM.App";
    private const string AppFileName = "PAM.App.exe";

    [STAThread]
    private static void Main()
    {
        // 单实例：已存在实例时直接退出，避免重复的托盘图标与热键注册冲突。
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("PowerShell 管理器已在运行，无需重复启动。", AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        using var hotkeyManager = new HotkeyManager();
        if (hotkeyManager.TryRegister(HotkeyModifiers.Control | HotkeyModifiers.Alt, Keys.T))
        {
            hotkeyManager.Pressed += (_, _) => LaunchTerminal();
        }
        else
        {
            MessageBox.Show("热键被占用", AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        using var menu = new ContextMenuStrip();
        var showMainItem = new ToolStripMenuItem("显示主界面");
        showMainItem.Click += (_, _) => ShowMainWindow();
        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => ConfirmExit();

        menu.Items.Add(showMainItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        using var notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = AppName,
            ContextMenuStrip = menu,
            Visible = true,
        };

        // 无主窗体的后台常驻消息循环；由“退出”菜单项触发 Application.Exit() 结束。
        Application.Run(new ApplicationContext());

        notifyIcon.Visible = false;
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

            if (NativeMethods.IsIconic(handle))
            {
                NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
            }
            else
            {
                NativeMethods.ShowWindow(handle, NativeMethods.SwShow);
            }

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
                MessageBox.Show($"未找到主界面程序：{exePath}", AppName,
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
            MessageBox.Show($"启动主界面失败：{ex.Message}", AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 注册的热键触发时启动 Windows Terminal。
    /// 通过 <c>-d</c> 显式指定工作目录为当前用户目录，避免默认落在 <c>C:\Windows\System32</c>。
    /// Shell 由 <see cref="ResolveShellFileName"/> 动态探测：装了 PowerShell 7 就用 pwsh.exe，否则回退 powershell.exe。
    /// </summary>
    private static void LaunchTerminal()
    {
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var shell = ResolveShellFileName();
            var psi = new ProcessStartInfo
            {
                FileName = TerminalFileName,
                // -d 指定终端启动的初始目录为 ~
                Arguments = $"-d \"{userProfile}\" {shell} -NoExit -Command \". $PROFILE\"",
                UseShellExecute = true,
                WorkingDirectory = userProfile,
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"启动 Windows Terminal 失败：{ex.Message}", AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 探测可用的 shell：优先 PowerShell 7（<c>pwsh.exe</c>），未安装则回退 Windows PowerShell（<c>powershell.exe</c>）。
    /// </summary>
    private static string ResolveShellFileName()
    {
        return IsPowerShell7Installed() ? PwshFileName : PowerShellFileName;
    }

    /// <summary>
    /// 判断系统是否安装了 PowerShell 7：依次检查常见安装目录与 PATH（等价于 <c>where pwsh</c>，但不额外拉起进程）。
    /// </summary>
    private static bool IsPowerShell7Installed()
    {
        foreach (var directory in GetPwshSearchDirectories())
        {
            try
            {
                if (File.Exists(Path.Combine(directory, PwshFileName)))
                {
                    return true;
                }
            }
            catch
            {
                // PATH 中可能存在无法解析的条目，跳过即可。
            }
        }

        return false;
    }

    /// <summary>
    /// 返回 pwsh.exe 的候选目录：默认安装位置、Store 安装的执行别名目录，以及 PATH 中的所有目录。
    /// </summary>
    private static IEnumerable<string> GetPwshSearchDirectories()
    {
        var directories = new List<string>();

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (programFiles.Length > 0)
        {
            directories.Add(Path.Combine(programFiles, "PowerShell", "7"));
            directories.Add(Path.Combine(programFiles, "PowerShell", "7-preview"));
        }

        var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrEmpty(localAppData))
        {
            // Microsoft Store 版 PowerShell 在这里留下 pwsh.exe 执行别名。
            directories.Add(Path.Combine(localAppData, "Microsoft", "WindowsApps"));
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(path))
        {
            foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var directory = entry.Trim().Trim('"');
                if (directory.Length > 0)
                {
                    directories.Add(directory);
                }
            }
        }

        return directories;
    }

    /// <summary>
    /// 二次确认后退出程序。
    /// </summary>
    private static void ConfirmExit()
    {
        var result = MessageBox.Show($"确定要退出 {AppName} 吗？", AppName,
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);

        if (result == DialogResult.Yes)
        {
            Application.Exit();
        }
    }
}
