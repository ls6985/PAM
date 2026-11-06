# PAM.Tray (后台托盘与热键)

无界面的 WinForms 后台常驻程序。开机自启，仅负责监听全局热键。

## 功能

- 使用 `NotifyIcon` 常驻托盘，提供右键菜单（显示主界面、退出）。
- `Mutex` 单实例限制。
- 注册 `Ctrl+Alt+T` 全局热键（Win32 `RegisterHotKey`）。
- 触发后启动 Windows Terminal 并加载 `$PROFILE`：`wt.exe powershell.exe -NoExit -Command ". $PROFILE"`。