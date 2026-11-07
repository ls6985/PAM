# PAM.Tray (后台托盘组件)

> 由 `PAM.App` 自动拉起的无界面常驻进程：托盘图标 + 全局热键 → 拉起 Windows Terminal。
> 它**只读** `%AppData%\PAM\settings.json`，从不写配置，也没有业务数据。
> 正常流程不需要手动运行它，也不需要关心它的存在；只有改热键注册 / 托盘行为时才需要看这里。

## 结构

- `Program.cs`：入口壳，只有 `ApplicationConfiguration.Initialize()` + `new TrayApplication().Run()`，没有任何逻辑。
- `TrayApplication.cs`：单实例 `Mutex`、`NotifyIcon`、托盘菜单、热键注册、设置热重载与消息循环。
- `HotkeyManager.cs`：隐藏消息窗口 + Win32 `RegisterHotKey` / `WM_HOTKEY`。
- `ImeSwitcher.cs`：UI Automation 读任务栏「托盘输入指示器」的中/英状态，仅在中文模式时模拟一次 Shift，把新打开的终端窗口切到英文输入模式；不碰输入法与键盘布局。
- 只引用 `PAM.Core`，不引用 PAM.App 项目；跨进程找主界面窗口靠进程名与 Win32 调用，编译期两者互不相干。
- exe 图标通过 `..\PAM.App\Assets\App.ico` 复用主界面那份（只是资源文件，不是项目引用）；托盘里的 `NotifyIcon` 图标则由 `TrayIconRenderer` 在运行时自绘 `</>`，两者不是一回事。

## 行为

- `Mutex`（`Local\PAM.Tray.SingleInstance`）单实例；已有实例时静默退出，不弹任何窗口——重复启动是正常场景（App 每次启动都会顺手拉一次），提示由 PAM.App 负责。
- `NotifyIcon` 右键菜单：显示主界面 / 重新加载设置 / 退出，托盘提示显示当前热键。`显示主界面` 会激活已运行的 `PAM.App` 窗口，主界面没开时则启动同目录的 `PAM.App.exe`。
- 按 `settings.json` 的 `hotkey` 注册 Win32 全局热键；系统保留组合直接拒绝，被其他软件占用时弹气泡提示去设置页更换。`HotkeyManager.TryRegister` 会先注销旧组合，支持运行中改注册。
- 触发后 `wt.exe {pwsh|powershell} -NoExit -Command ". $PROFILE"`，shell 由 `ShellResolver.Resolve(settings.TerminalType)` 决定（未装 PowerShell 7 自动回退 5.1）。
- UI 线程 `Timer`（700ms）消费两个命名事件并比对文件时间戳兜底：
  - `Local\PAM.SettingsChanged` → 重载设置、改注册热键、刷新语言与菜单文案；
  - `Local\PAM.Tray.ExitRequested` → 管理器关闭且 `KeepTrayOnClose = false` 时退出。
- 菜单与提示文案按 `settings.Language` 由 `PAM.Core.LocalizationService` 本地化。
- `switchImeToEnglish` 开启时，`Monitor_Tick` 顺带检查前台窗口：类名含 `CASCADIA`（Windows Terminal）或为 `ConsoleWindowClass`、且句柄未处理过，就由 `ImeSwitcher` 处理。Windows Terminal 走 TSF，实测跨进程 `ImmGetContext` 一律返回 0、`WM_IME_CONTROL` 也不被响应，IMM32 / TSF compartment 又是线程局部的，托盘进程改不了别的进程的输入状态；因此改成两条系统级路径：**UI Automation 读任务栏「托盘输入指示器」的名称判断当前是「中文模式」还是「英文/英语模式」；只在中文模式时用 `keybd_event` 模拟单独按一次 Shift**（主流中文输入法的中/英切换键），短延迟后复查，仍为中文最多再试一次。**不换键盘布局 / 输入语言**，输入法仍是微软拼音、搜狗等；本来就是英文模式时一个键都不发，不会翻回中文。读不到指示器时最多重试 3 轮后放弃。每个窗口只处理一次，不写 `$PROFILE`、不注册全局钩子。最近一次结果写在 `%AppData%\PAM\ime-switch.log`（`before` / `taps` / `after`）。

设置从哪来、谁负责写、两个进程怎么协作，见[根 README](../README.md) 与 [PAM.App/README.md](../PAM.App/README.md)。
