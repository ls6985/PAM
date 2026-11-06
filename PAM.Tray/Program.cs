using System.Diagnostics;

namespace PAM.Tray;

/// <summary>
/// 托盘程序入口：单实例保护、全局热键注册与托盘菜单。
/// </summary>
internal static class Program
{
    /// <summary>互斥体名称，用于限制同一会话内只运行一个实例。</summary>
    private const string MutexName = @"Local\PAM.Tray.SingleInstance";

    private const string AppName = "PAM.Tray";

    /// <summary>Windows Terminal 可执行文件名。</summary>
    private const string TerminalFileName = "wt.exe";

    [STAThread]
    private static void Main()
    {
        // 单实例：已存在实例时直接退出，避免重复的托盘图标与热键注册冲突。
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("PAM.Tray 已在运行，无需重复启动。", AppName,
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
        showMainItem.Click += (_, _) =>
        {
            // TODO: 待 PAM.App 主界面接入后实现窗口唤起。
        };
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
    /// 注册的热键触发时启动 Windows Terminal。
    /// 通过 <c>-d</c> 显式指定工作目录为当前用户目录，避免默认落在 <c>C:\Windows\System32</c>。
    /// </summary>
    private static void LaunchTerminal()
    {
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var psi = new ProcessStartInfo
            {
                FileName = TerminalFileName,
                // -d 指定终端启动的初始目录为 ~
                Arguments = $"-d \"{userProfile}\" powershell.exe -NoExit -Command \". $PROFILE\"",
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
    /// 二次确认后退出程序。
    /// </summary>
    private static void ConfirmExit()
    {
        var result = MessageBox.Show("确定要退出 PAM.Tray 吗？", AppName,
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);

        if (result == DialogResult.Yes)
        {
            Application.Exit();
        }
    }
}
