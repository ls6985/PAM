# PAM.Tray (后台托盘组件)

> 由 `PAM.App` 自动拉起的无界面常驻进程：托盘图标 + 全局热键 → 拉起 Windows Terminal。
> 登录时的自动启动由 `PAM.App` 写入的 HKCU 启动项完成，Tray 自身不写注册表、也不负责自己的启动。
> 它**只读** `%AppData%\PAM\settings.json`，从不写配置；正常流程不需要手动运行它，只有改热键注册 / 托盘行为时才需要看这里。

## 结构

- `Program.cs`：入口壳，无逻辑。
- `TrayApplication.cs`：单实例 `Mutex`、`NotifyIcon`、托盘菜单、热键注册、设置热重载与消息循环。
- `HotkeyManager.cs`：隐藏消息窗口 + Win32 `RegisterHotKey` / `WM_HOTKEY`。
- `ImeSwitcher.cs`：UI Automation 读任务栏输入指示器，把新终端窗口切到英文输入。
- 只引用 `PAM.Core`，不引用 PAM.App；找主界面窗口靠进程名与 Win32 调用。exe 图标复用 `..\PAM.App\Assets\App.ico`（资源文件，非项目引用），托盘图标由 `TrayIconRenderer` 运行时自绘，两者不是一回事。

## 行为

- 单实例 `Mutex` 抢不到时静默退出——App 每次启动都会重复拉起，属正常场景。
- 登录启动项指向本 exe 的绝对路径；程序路径一律用 `AppContext.BaseDirectory` 解析，因此从启动项唤起时工作目录是系统目录也不影响找配置、找主界面与注册热键。
- 右键菜单：显示主界面（激活已运行窗口，没开则启动 `PAM.App.exe`）/ 重新加载设置 / 退出；托盘提示显示当前热键，文案按语言本地化。
- 按 `hotkey` 注册全局热键：系统保留组合直接拒绝，被占用弹气泡提示；`TryRegister` 先注销旧组合，支持运行中改注册。
- 触发后 `wt.exe {pwsh|powershell} -NoExit -Command ". $PROFILE"`，shell 由 `ShellResolver.Resolve` 决定（未装 PS7 回退 5.1）。
- 700ms `Timer` 消费两个命名事件并比对文件时间戳兜底：`Local\PAM.SettingsChanged` → 重载设置、改注册热键、刷新文案；`Local\PAM.Tray.ExitRequested` → `KeepTrayOnClose = false` 时退出。
- `switchImeToEnglish` 开启时顺带检测前台新终端窗口（类名含 `CASCADIA` 或 `ConsoleWindowClass`）：Windows Terminal 走 TSF，跨进程改不了输入状态，因此用 UI Automation 读任务栏「托盘输入指示器」判断中/英，仅中文模式时 `keybd_event` 模拟一次 Shift（主流输入法的中/英切换键），**不换输入法与键盘布局**；每个窗口只处理一次，结果记 `%AppData%\PAM\ime-switch.log`。

设置从哪来、谁负责写、两个进程怎么协作，见[根 AGENT 总览](../AGENT-总览.md) 与 [PAM.App/AGENT-App.md](../PAM.App/AGENT-App.md)。
