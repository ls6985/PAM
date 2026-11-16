# PAM.Tray（后台托盘组件）

由 `PAM.App` 自动拉起的无界面常驻进程：托盘图标 + 全局热键 → 激活同一个 Windows Terminal 窗口。登录时的自动启动由 App 写入的 HKCU 启动项完成，Tray 自身不写注册表、也不负责自己的启动。它只读 `%AppData%\PAM\settings.json`，从不写配置。

## 结构

| 文件 | 职责 |
| --- | --- |
| `Program.cs` | 入口壳，无逻辑 |
| `TrayApplication.cs` | 单实例 `Mutex`、`NotifyIcon`、托盘菜单、热键注册、设置热重载与消息循环 |
| `HotkeyManager.cs` | 隐藏消息窗口 + Win32 `RegisterHotKey` / `WM_HOTKEY` |
| `ImeSwitcher.cs` | UI Automation 读任务栏输入指示器，把新终端窗口切到英文输入 |
| `TerminalPool.cs` | 单窗口终端通道：窗口的创建、纳管、显示、配置新鲜度与托盘重启后的找回；托盘只调用它 |

只引用 PAM.Core，不引用 PAM.App；找主界面窗口靠进程名与 Win32 调用。exe 图标复用 `..\PAM.App\Assets\App.ico`（资源文件，非项目引用），托盘图标由 `TrayIconRenderer` 运行时自绘，两者不是一回事。

## 行为

- 单实例 `Mutex` 抢不到时静默退出（App 每次启动都会重复拉起，属正常场景）。程序路径一律用 `AppContext.BaseDirectory` 解析，因此从启动项唤起时工作目录是系统目录也不影响找配置、找主界面与注册热键。
- 右键菜单：显示主界面（激活已运行窗口，没开则启动 `PAM.App.exe`）/ 重新加载设置 / 退出；托盘提示显示当前热键，文案按语言本地化。
- 按 `hotkey` 注册全局热键：系统保留组合直接拒绝，被占用弹气泡提示；`TryRegister` 先注销旧组合，支持运行中改注册。
- 触发后统一走 `TerminalPool.Activate()`：已持有窗口则显示 + 置前并返回，不新开窗口；窗口不存在（例如被用户关闭）时才由 `LaunchTerminalCold()` 冷启动一个，随后纳管为长期持有的窗口。两条路径共用 `wt.exe -d "<用户目录>" {pwsh|powershell} -NoProfile -NoExit -Command ". $PROFILE"`，shell 由 `ShellResolver.Resolve` 决定（未装 PS7 回退 5.1）。无论 `warmPoolEnabled` 是否开启、预热是否完成，通道都是 Windows Terminal。
- 700ms `Timer` 消费两个命名事件并比对文件时间戳兜底：`Local\PAM.SettingsChanged` → 重载设置、改注册热键、刷新文案；`Local\PAM.Tray.ExitRequested` → `KeepTrayOnClose = false` 时退出。同一 tick 还驱动单窗口通道：找回已存在的窗口、校验存活与配置新鲜度，并在未持有窗口时按开关安排后台创建，不新增计时器。
- `switchImeToEnglish` 开启时顺带检测前台新终端窗口（类名含 `CASCADIA` 或 `ConsoleWindowClass`）：Windows Terminal 走 TSF，跨进程改不了输入状态，因此用 UI Automation 读任务栏「托盘输入指示器」判断中 / 英，仅中文模式时 `keybd_event` 模拟一次 Shift（主流输入法的中 / 英切换键），**不换输入法与键盘布局**；每个窗口只处理一次，结果记 `%AppData%\PAM\ime-switch.log`。

## 单窗口终端通道（`warmPoolEnabled`，默认关）

- 托盘长期持有一个 `CASCADIA_HOSTING_WINDOW_CLASS` 窗口句柄。按热键只做 `ShowWindow` + `SetForegroundWindow`，不创建、不排队、不等待 PowerShell 启动；连续按键的响应时间恒定。窗口已在前台时调用是幂等的，不会隐藏或闪烁。`ShowAndFocus()` 对不可见窗口执行 `SW_SHOW`，因此 `$PROFILE` 用 `SW_HIDE` 隐藏该窗口后，按热键可直接唤回同一个会话。
- `warmPoolEnabled` 只决定是否在启动后延迟几秒提前把这个窗口准备好：开启时用 `CreateProcess` + `STARTF_USESHOWWINDOW/SW_HIDE` 创建初始隐藏的 WT 窗口（不先显示再隐藏，不会闪），按热键时直接显示；关闭时不提前准备，第一次按热键才打开，之后同样复用。开关不决定窗口类型。
- 窗口归属用 `SetProp`/`GetProp` 的跨进程窗口属性 `PAM.Tray.TerminalWindow` 标记，不依赖会随标签切换变化的窗口标题。托盘启动时按该属性枚举找回上一个实例持有的窗口并纳管；托盘退出不关闭窗口，因为它可能正被用户使用。多余的隐藏窗口会关闭，保证始终只有一个托盘窗口。
- 单窗口不等于单会话：用户可在该 WT 窗口内用原生快捷键开多个标签页。窗口被用户关闭后，托盘在下一个 tick 发现句柄失效并按开关后台重建（或等下一次热键冷启动）。
- 配置新鲜度采用粗粒度元数据比对：`settings.json` 与 `pam-data.json` 的写入时间和大小，任一变化即视为过期。设置页与别名／命令保存都落到这两个文件，因此不需要解析 `$PROFILE` 路径、也不需要在后台启动 PowerShell 进程。快照在窗口创建之前记录，创建期间发生的变化会在下一次 tick 触发重建。**只在从未显示过的预热隐藏窗口上**关闭并后台重建；`_hasBeenShown` 把「预热时创建为隐藏」与「显示过后被隐藏」区分开——WT 失焦自动隐藏与别名启动后的托管区隐藏都会让窗口不可见，不能误判成预热窗口而重建。已显示过的窗口不主动关闭，配置在窗口下次重建时生效。
- 创建与纳管在后台线程执行，`await` 延续回到 UI 线程后立即提交句柄，不等 700ms 定时器；`-NoProfile` 让 `$PROFILE` 只显式加载一次，省去自动加载与 dot-source 的重复执行。焦点沿用 `ShowWindow` / `SetForegroundWindow` 的尽力而为，不加 `AttachThreadInput`、不模拟 ALT。

设置从哪来、谁负责写、两个进程怎么协作，见[总览](../AGENT-总览.md) 与 [App](../PAM.App/AGENT-App.md)。
