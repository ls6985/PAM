# PAM.Tray (后台监听程序)

> 只是一个无界面的 WinForms 常驻程序：注册全局热键，按下后拉起 Windows Terminal。
> 它**只读** `%AppData%\PAM\settings.json`，从不写配置，也没有业务数据。
> 改界面、格式或配置字段都用不到这里；只有改热键注册 / 托盘行为时才需要看。

## 行为

- `Mutex` 单实例；`NotifyIcon` 右键菜单：重新加载设置 / 退出，托盘提示显示当前热键。
- 按 `settings.json` 的 `hotkey` 注册 Win32 全局热键；系统保留组合直接拒绝，被其他软件占用时弹气泡提示去设置页更换。`HotkeyManager.TryRegister` 会先注销旧组合，支持运行中改注册。
- 触发后 `wt.exe {pwsh|powershell} -NoExit -Command ". $PROFILE"`，shell 由 `ShellResolver.Resolve(settings.TerminalType)` 决定（未装 PowerShell 7 自动回退 5.1）。
- UI 线程 `Timer`（700ms）消费两个命名事件并比对文件时间戳兜底：
  - `Local\PAM.SettingsChanged` → 重载设置、改注册热键、刷新语言与菜单文案；
  - `Local\PAM.Tray.ExitRequested` → 管理器关闭且 `KeepTrayOnClose = false` 时退出。
- 菜单与提示文案按 `settings.Language` 由 `PAM.Core.LocalizationService` 本地化。

设置从哪来、谁负责写、两个进程怎么协作，见[根 README](../README.md) 与 [PAM.App/README.md](../PAM.App/README.md)。
