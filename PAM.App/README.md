# PAM.App (WPF 主界面)

WPF-UI (`FluentWindow`) 主界面，**用户唯一入口**，也是 `settings.json` 的唯一写入方。MVVM 解耦，状态与校验在 ViewModel，窗口只做 UI 交互。

`App.xaml.cs` 的 `OnStartup` 在完成本地化与主窗口显示后调用 `TrayBridge.EnsureTrayRunning()`，静默拉起同目录的 `PAM.Tray.exe`；找不到托盘 exe 或启动失败都不影响主界面打开。csproj 只引用 `PAM.Core`，**不引用 PAM.Tray**；同目录那个托盘 exe 由两个项目各自的构建后推送目标互拷而来，见[根 README](../README.md)。

## 四个页签

顶部 `TabView`（样式 `Styles.xaml` 的 `NavTabItemStyle`），状态栏各页共用：

- **别名管理**（`MainWindow.xaml` 的 `AliasTab`）：搜索、新建 / 编辑（别名也可改名）/ 删除（二次确认）、路径浏览、文件不存在提示、长路径中间省略、复制 `$PROFILE` 路径、执行策略受限时警告并可一键修复。
- **自定义命令**（`Views/CustomCommandsView`）：受控区块内的函数 / 片段，列表列含状态胶囊（点击切换启用）、代码预览；内联编辑条用多行代码框，`Ctrl+Enter` 确认。数据格式见 [PAM.Core/README.md](../PAM.Core/README.md)。
- **美化**（`Views/BeautifyView` + `ViewModels/BeautifyViewModel`）：见下节。
- **设置**（`Views/SettingsView` + `ViewModels/SettingsViewModel`）：见下节。

快捷键：`Ctrl+N` 新建、`Ctrl+S` 保存、`Enter` 确认、`Esc` 取消（自定义命令页为 `Ctrl+Enter`）。设置页录制热键时窗口级快捷键会让路，避免 `Ctrl+S` 被"保存并生效"抢走。

## 美化页

终端 Starship 与字体的配置入口，数据源 `BeautifyViewModel`，五个卡片：

| 卡片 | 内容 |
| --- | --- |
| Starship | 探测已装版本与 Scoop；只有一个"一键安装（含 Scoop）"按钮，Scoop 缺失时自动走官方引导脚本（需联网），再用 Scoop 装 Starship。安装输出实时显示在页脚反馈区 |
| 终端字体 | 下拉列出注册表里的等宽 / Nerd Font 候选，默认选中 Windows Terminal 当前字体；内置 `Assets/Fonts/CascadiaCodeNF.ttf` 未装时提供按当前用户安装（免管理员）；"应用到 Windows Terminal" 写入 `profiles.defaults.font.face`，写前自动备份 |
| 主题 | 六个官方预设（EmbeddedResource 打包在 PAM.Core），应用 = 备份 → 写 `starship.toml` → 确保 `$PROFILE` init 行；标注"需 Nerd Font"的预设会在字体未装时给出警告。下拉下方状态行通过比对当前 toml 原文识别已应用的主题并显示名称（重启后也保持选中），应用自定义提示符后改为提示"自定义提示符已接管顶层 format" |
| 自定义提示符 | **点选片段组合**编辑器：点「添加片段」加入提示符元素，每个片段下拉选变量（精选 20 个中文变量，按身份 / Git / 运行状态 / 开发环境分组）或固定符号（`@ : ~ $ 空格`），单独选 12 色色板颜色（选「自定义…」后右侧直接填 `#` 后面的 6 位色号），变量不再手填、不可直接编辑；上方是带真实颜色的深色预览（变量显示示意值）。默认 format 是 `[$username](fg:#BBC4B9)[$hostname](fg:#BBC4B9)[:](fg:#BBC4B9)[$directory](fg:#BBC4B9)[$character](fg:#BFA8BF)`（即作者当前提示符的组合：用户名 / 主机名 / 冒号 / 路径同色，结束符另一色），**用户名 / 主机名 / 当前路径不需要也不允许手填**，路径会随 `cd` 变化。"应用到 Starship" 写入顶层 `format` 并接管 format 里引用到的模块段落（段选色内嵌进模块自身 format，未被引用的段落原样保留）；首次应用单独备份为 `starship.toml.pam-prompt.bak`，"恢复应用前配置" 一键还原（恢复即删除备份，按钮回到禁用，再次应用会重新备份）；"重置为默认" 只改编辑框、不写文件。配置本身存 toml，不进 `settings.json`。打开新终端窗口即可看到效果 |
| 配置维护 | 展示 `starship.toml` 路径 / 是否创建 / init 行状态 / 备份存在；打开 `starship.toml`、恢复 toml 备份、恢复 Windows Terminal 备份 |

进入页签（`Loaded` / 页签切换）自动刷新全部状态；所有耗时操作期间 `IsBusy` 禁用按钮。状态文案在 ViewModel 保存中文原文、读取时翻译，语言切换零成本。

## 设置页

配置存于 `%AppData%\PAM\settings.json`。除热键外所有改动即时落盘并 `TrayBridge.SignalSettingsChanged()` 通知托盘；热键须先通过冲突检测再点"应用热键"。

| 配置项 | 说明 |
| --- | --- |
| 主题 | 亮 / 暗 / 跟随系统。`ThemeService` 在跟随系统时挂 `SystemThemeWatcher.Watch`，固定亮/暗时 `UnWatch` 后 `ApplicationThemeManager.Apply` |
| 语言 | 简体中文 / English，立即生效无需重启。`PAM.Core.LocalizationService` 提供词表，`Services/UiLocalizer.cs` 在元素 `Loaded` 时替换 XAML 静态中文并在切换语言时重新遍历窗口；绑定文本由 ViewModel 保存中文原文后翻译 |
| 默认终端 | 默认 `PowerShell 7 (pwsh.exe)`，另有 `Windows PowerShell 5.1` 与 `自动探测`。决定热键唤醒的 shell、保存后启动的窗口，以及"打开配置文件"打开哪一个 `$PROFILE`；切换会触发 `AppServices.SettingsChanged`，`MainWindow` 重新载入别名与自定义命令（有未保存改动时在状态栏提示） |
| 打开配置文件 | `ProfileService.OpenProfileFile(terminalType)`：按当前选中的终端解析 `$PROFILE`，不存在则创建（UTF-8 BOM）后用系统默认编辑器打开，无 `.ps1` 关联时回退记事本 |
| 快捷键设置 | `SettingsView.xaml.cs` 用 `KeyInterop` 把 WPF 按键转成 Win32 VK，交给 `HotkeyChord` 规范化；`HotkeyConflictDetector` 依次检查系统保留组合 → Win32 `RegisterHotKey` 真实占用探测 → 常见软件通用快捷键。系统保留 / 已占用会阻止应用并说明原因，软冲突弹窗二次确认；托盘持有当前组合时视为自己占用。应用时若托盘未运行会自动拉起 |
| 终端输入 | "打开终端时把中文输入法切到英文模式"（默认开启）：后台托盘的 700ms 定时器检测前台窗口，发现新打开的 PowerShell / Windows Terminal 窗口就用 UI Automation 读任务栏「托盘输入指示器」，**仅在中文模式时模拟一次单独的 Shift** 切到英文输入状态并复查；输入法本身与键盘布局都不变，本来已是英文模式时一个键都不发。每个窗口只处理一次，不写 `$PROFILE`、不注册全局钩子。最近一次切换结果记录在 `%AppData%\PAM\ime-switch.log`。开关关闭时托盘完全不碰前台窗口 |
| 无托盘行为 | "关闭管理器时保留后台托盘"开启（默认）时，关闭窗口后热键与托盘菜单继续可用；关闭则发退出信号让 Tray 一并退出。设置页可查看 / 启动 / 结束后台托盘；结束后台托盘后全局热键立即失效，重新打开管理器或点"立即启动"才会恢复 |
| 别名启动 | "独立进程启动（静默日志）"（默认开启）：别名改由 `$PROFILE` 里的 `Invoke-PamAlias` 公共函数**借 `explorer.exe` 代启动**——无参数直接把路径交给 explorer，带参数先生成临时 `.lnk` 携带参数再交给 explorer。新进程父进程是 explorer，完全脱离终端进程树与控制台，所以关 PowerShell 不会连带关掉软件、Electron 等程序的日志也刷不回终端；不再需要 NUL 重定向，回到 ShellExecute 路径，UAC 提升正常（实测启动耗时反而略快）。关闭则恢复原来的直接启动方式。另有"启动时显示别名备注"（默认开启），启动前 `Write-Host` 一行青色备注（备注为空则不打印）。两个开关由公共函数运行时读 `settings.json`，改设置即时生效，不用重写 `$PROFILE`；别名保存后才会生成 / 更新启动器区块，且 **`$PROFILE` 只在终端启动时加载一次，必须新开终端窗口才能生效** |
| 恢复默认 | 二次确认后整份设置恢复默认值 |

`settings.json` 字段（camelCase，定义在 `PAM.Core/Settings/AppSettings.cs`）：

```json
{
  "hotkey": "Ctrl+Alt+T",
  "terminalType": "pwsh",
  "autoLaunchTerminal": true,
  "theme": "System",
  "language": "zh-CN",
  "keepTrayOnClose": true,
  "switchImeToEnglish": true,
  "aliasLaunchEnhanced": true,
  "showAliasRemarkOnLaunch": true
}
```

- `hotkey`：`Ctrl/Alt/Shift/Win` 至少一个修饰键 + 一个普通键，支持字母、数字、F1~F24、方向键、小键盘等；非法值读取时自动重置。
- `terminalType`：`pwsh` / `powershell` / `auto`；`theme`：`System` / `Light` / `Dark`；`language`：`zh-CN` / `en-US`。
- `switchImeToEnglish`：默认 `true`。托盘检测到新打开的终端窗口时，用 UI Automation 读任务栏输入指示器，仅在中文模式时模拟一次 Shift 把该输入法切到英文模式（不更换输入法或键盘布局，已是英文则不按键）；关闭后托盘完全不做窗口检测。只影响该终端窗口，每个句柄只切一次。
- `aliasLaunchEnhanced` / `showAliasRemarkOnLaunch`：默认均为 `true`。由 `$PROFILE` 里的 `Invoke-PamAlias` 运行时读取，控制是否借 explorer 脱离终端进程树启动，以及是否打印备注；见 [PAM.Core/README.md](../PAM.Core/README.md) 的别名启动器区块。
- 托盘监听 `Local\PAM.SettingsChanged` 与 `Local\PAM.Tray.ExitRequested` 两个命名事件，并兜底轮询文件时间戳，因此手工编辑 `settings.json` 也会生效。
- PowerShell 7 与 5.1 的 `$PROFILE` 是两个不同文件（`Documents\PowerShell\` 与 `Documents\WindowsPowerShell\`）。切换默认终端即切换管理器维护的那一份，托盘与保存后启动的窗口用同一个 shell，别名 / 自定义命令会正常加载。

## 应用服务（`Services/`）

- `AppServices.cs`：设置读写唯一入口，广播"哪些字段变了"（主题 / 语言 / 终端 / 热键）。
- `ThemeService.cs` · `UiLocalizer.cs` · `HotkeyConflictDetector.cs` · `Loc.cs`：主题、本地化遍历、热键检测、本地化快捷访问。

## 加载状态与分层

`MainViewModel.IsLoading` 初始为 `true`，`InitializeAsync()` 的 `finally` 置回 `false`；`EmptyStateVisibilityConverter`（`IsLoading` + `IsListEmpty`）控制空状态，列表在加载中隐藏，避免闪现"还没有别名"。

- `MainWindow.xaml.cs`：焦点、状态栏、空状态文案、窗口关闭时按 `KeepTrayOnClose` 处理托盘。
- `ViewModels/`：绑定属性、数据集合与校验，调用 `PAM.Core` 读写。
- `Converters/` · `Controls/`：值转换器与中间省略文本控件。

## 程序图标

`Assets/App.ico` 是代码绘制生成的多尺寸图标（16/24/32/48/64/128/256），由 csproj 的 `ApplicationIcon` 编进 exe：任务栏、Alt+Tab、窗口预览缩略图左上角、资源管理器都取它，**不需要任何图片素材**。改配色 / 符号编辑 `Assets/generate_icon.py` 顶部的 `BG`、`FG` 与 `round_line` 坐标后重跑 `python Assets/generate_icon.py`（需 Pillow），再重新构建即可；Windows 会缓存旧图标，必要时重启资源管理器或清图标缓存。

`PAM.Tray.exe` 通过 `..\PAM.App\Assets\App.ico` 复用同一份，两个 exe 图标一致（仅资源文件复用，不是项目引用）。
