# PAM.App (WPF 主界面)

WPF-UI (`FluentWindow`) 主界面，**用户唯一入口**，也是 `settings.json` 与 `pam-data.json` 的唯一写入方。MVVM 解耦，状态与校验在 ViewModel。

`OnStartup` 完成本地化与主窗口显示后调用 `TrayBridge.EnsureTrayRunning()` 静默拉起同目录的 `PAM.Tray.exe`，失败不影响主界面打开。csproj 只引用 `PAM.Core`，**不引用 PAM.Tray**；bin 里的托盘 exe 由两个项目的构建后推送目标互拷而来，见[根 AGENT 总览](../AGENT-总览.md)。

## 四个页签

顶部 `TabView`（样式 `Styles.xaml` 的 `NavTabItemStyle`），状态栏各页共用：

- **别名启动**（`MainWindow.xaml` 的 `AliasTab`）：搜索、新建 / 编辑（可改名）/ 删除（二次确认）、路径浏览、长路径中间省略、复制 `$PROFILE` 路径、执行策略受限一键修复。数据来自 `AppServices.Data.Aliases`，保存时克隆快照、改写别名字段后由 `ProfileRenderer` 重建托管区；同名冲突在状态栏提示。
- **自定义命令**（`Views/CustomCommandsView`）：函数 / 片段，状态胶囊点击切换启用；内联编辑条 `Ctrl+Enter` 确认；内置命令 `pls` / `ongit` / `sudo` 只读展示（「内置」徽章，不可编辑 / 删除 / 切换），随每次保存自动维护，无独立开关。数据来自 `AppServices.Data.CustomCommands`，内置条目由 `CustomCommandService.GetBuiltinEntries()` 提供。数据格式见 [PAM.Core/AGENT-Core.md](../PAM.Core/AGENT-Core.md)。
- **别名启动** 与自定义命令页的命名校验都会拒绝 `pls` / `ongit` / `sudo`（`CustomCommandService.IsBuiltinName`），避免同名包装函数被区块末尾的内置函数覆盖。
- **美化**（`Views/BeautifyView` + `ViewModels/Beautify/` 按功能拆分的 partial）：见下节。
- **设置**（`Views/SettingsView` + `ViewModels/SettingsViewModel`）：见下节。

快捷键：`Ctrl+N` 新建、`Ctrl+S` 保存、`Enter` 确认、`Esc` 取消（自定义命令页为 `Ctrl+Enter`）；录制热键时窗口级快捷键让路。

## 美化页

数据源 `BeautifyViewModel`（壳 + partial），五个卡片：

| 卡片 | 内容 |
| --- | --- |
| Starship | 探测已装版本与 Scoop；一键安装（含 Scoop），Scoop 缺失时先走官方引导脚本 |
| 外观 | 对齐 WT「默认值 → 外观」分组：字体（下拉列等宽 / Nerd Font，含 Cascadia Code NF 一键安装）、复古效果、光标形状 / 颜色、背景图及透明度、窗口透明度 / 亚克力，对应 `profiles.defaults` 的 8 个属性。保存只提交与读盘快照不同的项，留空 / 选「终端默认」= 删除属性交还 WT 默认；首次修改前自动保存 `settings.json.bak`（永不覆盖） |
| 主题 | 六个官方预设（EmbeddedResource 在 Core）：备份 → 写 `starship.toml` → 确保 init 行；状态行比对 toml 原文识别已应用主题；需 Nerd Font 的预设未装字体时警告 |
| 自定义提示符 | 点选片段组合编辑器：片段下拉选变量（20 个中文变量，分四组）或固定符号，单独选 12 色色板（每项显示 `#RRGGBB`，可自定义色号）；变量不可手填，用户名 / 主机名 / 路径由 Starship 运行时读取。写顶层 `format` 并接管被引用的模块段落；首次应用备份 `starship.toml.pam-prompt.bak`，"恢复应用前配置"还原即删备份。另有「提示符前空一行」「路径分隔符用 /」两个开关，随托管块写入并从块读回 |
| 配置维护 | 展示 toml / init 行 / 各备份状态；打开 toml、恢复 toml 备份、恢复 Windows Terminal 备份 |

备份语义别混用：toml 的 `.bak` 是**单槽**（最近一次应用主题前，恢复后保留）；WT 的 `settings.json.bak` 是**本软件首次修改前**的原始快照，永不覆盖、恢复不删除，可反复恢复。

进入页签自动刷新状态；耗时操作 `IsBusy` 禁用按钮；状态文案在 ViewModel 存中文原文、读取时翻译。

## 设置页

配置存 `%AppData%\PAM\settings.json`；除热键外改动即时落盘并 `SignalSettingsChanged()` 通知托盘，热键须通过冲突检测后点"应用热键"。

| 配置项 | 说明 |
| --- | --- |
| 主题 | 亮 / 暗 / 跟随系统（`ThemeService`） |
| 语言 | 简体中文 / English，立即生效（`UiLocalizer` 遍历 XAML，绑定文本由 ViewModel 翻译） |
| 默认终端 | `pwsh` / `powershell` / `auto`，决定热键唤醒的 shell 与生成哪一份 `$PROFILE`；数据文件始终同一份，切换后主界面重新载入并按新目标重建托管区 |
| 打开配置文件 | 按当前终端解析 `$PROFILE`，不存在则创建（UTF-8 BOM）后打开 |
| 快捷键设置 | `KeyInterop` 转 Win32 VK → `HotkeyChord`；`HotkeyConflictDetector` 依次查系统保留 → `RegisterHotKey` 真实占用 → 常见软件软冲突（弹窗二次确认） |
| 终端输入 | `switchImeToEnglish`（默认开）：托盘对新终端窗口切英文输入法，详见 [PAM.Tray/AGENT-Tray.md](../PAM.Tray/AGENT-Tray.md)；日志 `%AppData%\PAM\ime-switch.log` |
| 无托盘行为 | `KeepTrayOnClose`（默认开）：关窗口后托盘与热键保留，关闭则发退出信号；设置页可查看 / 启动 / 结束托盘 |
| 别名启动 | `aliasLaunchEnhanced`（默认开）：借 explorer 脱离终端进程树启动；`showAliasRemarkOnLaunch`：启动前打印一行备注。开关运行时读取、即时生效，但 **`$PROFILE` 要新开终端窗口才生效** |
| 恢复默认 | 二次确认后整份恢复默认值 |

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

- `hotkey` 非法值读取时自动重置；枚举取值见 `AppSettings`。
- 托盘对文件时间戳有轮询兜底，手工编辑 `settings.json` 也会生效。
- PowerShell 7 与 5.1 的 `$PROFILE` 是两个不同文件（`Documents\PowerShell\` 与 `Documents\WindowsPowerShell\`），切换默认终端即切换生成目标。`MainViewModel.InitializeAsync()` 比对 `managedHash`，托管区缺失或被外部修改时按 `pam-data.json` 重建一次；`Inspect()` 只读取边界标记之后的内容。

## 其它

- `Services/AppServices.cs`：设置读写唯一入口，广播变更字段；同时持有 `PamData` 内存快照，`SaveData()` 落盘并替换快照，别名页与自定义命令页都基于 `Clone()` 改写各自字段，避免互相覆盖；`ThemeService` / `UiLocalizer` / `HotkeyConflictDetector` / `Loc` 同目录。
- 分层：`MainWindow.xaml.cs`（焦点 / 状态栏 / 关闭时按 `KeepTrayOnClose` 处理托盘）、`ViewModels/`、`Converters/`、`Controls/`。
- `MainViewModel.IsLoading` 控制空状态显示，避免加载中闪现"还没有别名"。
- 图标 `Assets/App.ico` 由 `Assets/generate_icon.py` 生成（需 Pillow），改配色后重跑脚本再构建；`PAM.Tray.exe` 复用同一份。
