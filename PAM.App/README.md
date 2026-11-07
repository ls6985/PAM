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

终端 Starship 与字体的配置入口，数据源 `BeautifyViewModel`，四个卡片：

| 卡片 | 内容 |
| --- | --- |
| Starship | 探测已装版本与 Scoop；只有一个"一键安装（含 Scoop）"按钮，Scoop 缺失时自动走官方引导脚本（需联网），再用 Scoop 装 Starship。安装输出实时显示在页脚反馈区 |
| 终端字体 | 下拉列出注册表里的等宽 / Nerd Font 候选，默认选中 Windows Terminal 当前字体；内置 `Assets/Fonts/CascadiaCodeNF.ttf` 未装时提供按当前用户安装（免管理员）；"应用到 Windows Terminal" 写入 `profiles.defaults.font.face`，写前自动备份 |
| 主题 | 六个官方预设（EmbeddedResource 打包在 PAM.Core），应用 = 备份 → 写 `starship.toml` → 确保 `$PROFILE` init 行；标注"需 Nerd Font"的预设会在字体未装时给出警告 |
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
| 无托盘行为 | "关闭管理器时保留后台托盘"开启（默认）时，关闭窗口后热键与托盘菜单继续可用；关闭则发退出信号让 Tray 一并退出。设置页可查看 / 启动 / 结束后台托盘；结束后台托盘后全局热键立即失效，重新打开管理器或点"立即启动"才会恢复 |
| 恢复默认 | 二次确认后整份设置恢复默认值 |

`settings.json` 字段（camelCase，定义在 `PAM.Core/AppSettings.cs`）：

```json
{
  "hotkey": "Ctrl+Alt+T",
  "terminalType": "pwsh",
  "autoLaunchTerminal": true,
  "theme": "System",
  "language": "zh-CN",
  "keepTrayOnClose": true
}
```

- `hotkey`：`Ctrl/Alt/Shift/Win` 至少一个修饰键 + 一个普通键，支持字母、数字、F1~F24、方向键、小键盘等；非法值读取时自动重置。
- `terminalType`：`pwsh` / `powershell` / `auto`；`theme`：`System` / `Light` / `Dark`；`language`：`zh-CN` / `en-US`。
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
