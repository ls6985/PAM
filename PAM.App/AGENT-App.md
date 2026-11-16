# PAM.App（WPF 主界面）

WPF-UI (`FluentWindow`) 主界面，**用户唯一入口**，也是 `settings.json` 与 `pam-data.json` 的唯一写入方。MVVM 解耦，状态与校验在 ViewModel。

`OnStartup` 完成本地化与主窗口显示后经 `TrayBridge.EnsureTrayRunning()` 静默拉起同目录的 `PAM.Tray.exe`，失败不影响主界面打开；随后按 `startTrayWithWindows` 调用 `AutoStartService.Sync()` 校准登录启动项。csproj 只引用 PAM.Core，不引用 PAM.Tray；交付目录里成对的两个 exe 由根目录 `publish.ps1` 一次产出，见[总览](../AGENT-总览.md)。

## 四个页签

顶部 `TabView` 用 `Styles.xaml` 的隐式模板排版（`TabPanel` + 分隔线 + `PART_SelectedContentHost`），页签样式 `NavTabItemStyle`，两个选中态画刷定义在 `Themes/PamTheme.*.xaml`；状态栏各页共用。四个页签页头右侧统一放一枚托管区徽章（`PAM-Alias` / `PAM-Custom` / `PAM-Theme` / `PAM-Settings`）。

- **别名启动**（`MainWindow.xaml` 的 `AliasTab`）：搜索、新建 / 编辑（可改名）/ 删除（二次确认）、路径浏览、长路径中间省略、执行策略受限一键修复。编辑条底部默认收起的「高级选项：启动参数」填写每次启动固定附带的参数，不填时生成的包装函数与原来一致；每次打开编辑条都恢复为收起状态。数据来自 `AppServices.Data.Aliases`，保存时克隆快照、改写别名字段后由 `ProfileRenderer` 重建托管区；同名冲突在状态栏提示。
- **自定义命令**（`Views/CustomCommandsView`）：命令名即终端里的调用入口，代码只写要执行的命令（`CustomCommandCode` 包成同名函数）；状态胶囊点击切换启用；内联编辑条 `Ctrl+Enter` 确认；内置命令 `pls` / `ongit` / `sudo` 只读展示（「内置」徽章，不可编辑 / 删除 / 切换），随每次保存自动维护。列表按名称 / 状态 / 备注 / 操作四列展示，名称列只显示名称，备注列占满剩余宽度。数据格式见 [Core](../PAM.Core/AGENT-Core.md)。
- 自定义命令编辑条底部有与别名页同款的「高级选项：命令参数」折叠入口（默认收起，打开已配置参数的命令时自动展开）：一个「允许在命令名后传入参数」开关 + 一个「固定参数」输入框，对应 `allowsArguments` / `arguments`；关闭开关时函数体里仍可直接用 `$args`。
- 工具栏「新建命令」左侧的「示例 / 指南」打开 `Views/CommandGuideWindow` 模态对话框：使用说明 + 六条常用命令示例，每条可一键复制；示例是窗口内的静态表，不读配置。
- 两页的命名校验都拒绝 `pls` / `ongit` / `sudo`（`CustomCommandService.IsBuiltinName`），避免同名包装函数被区块末尾的内置函数覆盖。
- **美化**（`Views/BeautifyView` + `ViewModels/Beautify/` 按功能拆分的 partial）与 **设置**（`Views/SettingsView` + `ViewModels/SettingsViewModel`）见下两节。别名启动页与自定义命令页按功能拆分为独立视图，交互代码同构也各自保留。

## 主题与全局控件画刷

`App.xaml` 在合并 `Styles.xaml` 之后追加 `Themes/PamTheme.Light.xaml`；`ThemeService.ApplyBrushes()` 每次主题应用后按当前明暗把整册换成 Light / Dark 一份，并始终放在合并列表末尾，使同名键优先于 WPF-UI 的主题字典（WPF 没有按主题自动切换的 `ThemeDictionaries`，这里用「键名覆盖 + 运行时整册替换」实现同样效果；`ApplicationThemeManager.Changed` 覆盖跟随系统切换的路径）。

画刷字典只覆盖必要的键，其余沿用 WPF-UI：强调色板与各控件模板引用的成品画刷、控件描边（渐变改半透明细线）、开关关闭态浅灰、底部状态栏与信息面板的低透明度信息蓝。强调色固定为浅色 `#005FB8` / 深色 `#60CDFF`，不跟随系统强调色（系统色可能被配成接近黑的深色）。深色下 `TextOnAccentFillColorPrimaryBrush` 为黑色，主按钮文字自动可读。

快捷键：`Ctrl+N` 新建、`Ctrl+S` 保存、`Enter` 确认、`Esc` 取消（自定义命令页为 `Ctrl+Enter`）；录制热键时窗口级快捷键让路。

## 美化页

数据源 `BeautifyViewModel`（壳 + 按功能拆分的 partial）。自上而下三张卡片：Starship、主题、配置维护。页头右侧按钮组与左侧说明文字底部对齐，依次是「启用主题 / 关闭主题」「刷新状态」与 `PAM-Theme` 徽章。主题与自定义提示符是显式二选一，界面不会同时出现两套设置。

| 卡片 | 内容 |
| --- | --- |
| Starship | 探测已装版本与 Scoop；按钮自上而下是「安装内置NF字体」「一键安装（含 Scoop）」「重新检测」。字体下拉放在高级选项的外观子卡里，装字体按钮留在首屏 |
| 主题 | 单选上方单独一行显示当前生效方案（`PresetStatusText`，自定义提示符时显示「当前主题：自定义提示符」），紧接一行 12px 二选一提示；两行都在单选上方。下面是「主题预设 / 自定义提示符」单选（`UseCustomPrompt`，初始按磁盘上是否存在 PAM 托管块推断，切换只在内存、不写 `settings.json`，刷新状态不覆盖用户选择）。选主题时显示六个官方预设下拉，选自定义时显示片段编辑器（只以一条分隔线与上方内容分开）。再往下是「通用设置」与「高级选项」两个折叠入口，都默认收起、都用 `ToggleButton.IsChecked` 控制显隐、都不写 `settings.json`：通用设置是三个 `ui:ToggleSwitch`——前两个「提示符前空一行」「路径分隔符用 /」对两种方案都生效；第三个「单窗口（隐藏标签栏）」写 Windows Terminal 根级属性，WT 配置不存在时禁用；高级选项是「外观」子卡（字体、复古效果、光标、背景图、透明度，对应 `profiles.defaults` 的 8 个属性，只提交与读盘快照不同的项）。高级选项下方是两种方案共用的「恢复默认」「放弃更改」「保存设置」 |
| 配置维护 | 展示 `starship.toml` 完整路径；按钮是「打开 starship.toml」「恢复上次主题设置」「恢复终端备份」；备份是否可用由按钮的 `CanRestore*` 体现 |

通用选项在 `BeautifyViewModel.Options.cs`：前两个开关的状态由两种方案共用，读回统一走 `StarshipService.ReadCommonOptions()`（按 TOML 键解析，键不存在时用程序默认值），日常刷新不覆盖未保存的草稿；`DiscardChanges` 与 `RestoreTomlAsync` 强制从磁盘重读。主题模式由 `StarshipService.ApplyPreset(id, profilePath, options)` 经 `ApplyCommonOptions()` 把根级 `add_newline` 与 `[directory] use_os_path_sep` 写进预设文本（预设没有 `[directory]` 段时补一个）；自定义模式由 `StarshipPromptService.Apply()` 写进托管块。新增的选项行带 `# PAM-COMMON-OPTIONS` 标记，`DetectAppliedPreset()` 比对预设原文时先剔除这些行。

第三个开关在 `BeautifyViewModel.SingleWindow.cs`：状态按 `ReadGlobalFlags()` 的 `alwaysShowTabs`/`showTabsInTitlebar` 实际值判断（两者都为 false 才算开启），草稿并入 `AppearanceSnapshot`，点「保存设置」时与外观改动合并成一次 `ApplyAppearance()` 落盘；关闭时显式写回 WT 默认的 true / true。`RestoreDefaults` 把它复位为关，`DiscardChanges` 随外观快照一起还原。

统一按钮在 `BeautifyViewModel.Save.cs`：

- 「保存设置」`SaveSettingsAsync`：按当前方案只写一种提示符配置——主题预设走 `StarshipService.ApplyPreset`（整份覆盖，并附上两个通用选项），自定义提示符走 `StarshipPromptService.Apply`（增量插入托管块）；主题开关关闭时给 `ApplyPreset` 传 `ensureProfileInit: false`，只更新 `starship.toml`；再提交有变化的外观项，反馈合并成一条，外观无改动时跳过。
- 「恢复默认」`RestoreDefaults`：两个通用开关与单窗口开关恢复程序默认值；主题模式选回无需特殊字体的默认预设，自定义模式把片段恢复为默认格式；都不写盘，需再点保存。
- 「放弃更改」`DiscardChanges`：还原外观草稿、从磁盘重读提示符与方案、把主题选择还原到已识别主题、重读通用选项，回到上次保存状态。

三个按钮的可用性只受 `IsBusy` 控制，放在高级选项下方，两个折叠区展开后也不会被遮住。美化页在 TabView 里常驻，切换页签不重置展开状态。`TabView` 用 `PART_SelectedContentHost` 呈现选中内容，切换时内容脱离并重入视觉树，绑定型 `DataContext` 会短暂求值为 null；`MainWindow` 构造函数把美化页 `DataContext` 直接赋为本地值，并通过 `BeautifyView.AttachViewModel()` 显式注入同一个 `MainViewModel.Beautify` 实例，按钮事件只走这个注入引用。界面说明文案使用日常用语；静态中文原文即本地化键，改文案需同步 `PAM.Core/LocKey.cs`。

备份语义：toml 的 `.bak` 是单槽（最近一次应用主题前，恢复后保留）；WT 的 `settings.json.bak` 是本软件首次修改前的原始快照，永不覆盖、恢复不删除，可反复恢复。

构造时探测一次状态填充界面，之后进入页签不再自动刷新；页头「刷新状态」或各操作触发重新探测。耗时操作 `IsBusy` 禁用按钮；状态文案在 ViewModel 存中文原文、读取时翻译。

主题开关在 `BeautifyViewModel.Theme.cs`：状态由 `$PROFILE` 受控 Starship 区块内的初始化脚本是否被注释决定（`StarshipService.IsProfileInitEnabled()`），按钮文字随状态在「启用主题 / 关闭主题」之间切换。点击时对 `GetManagedProfilePaths()` 列出的全部 `$PROFILE`（5.1 与 7 各一份）统一调用 `SetProfileInitEnabled()`；区块、`starship.toml` 与初始化缓存文件都保留，重新启用不需要重选主题。开关不写 `settings.json`，状态由配置文件本身承载。

按钮状态不依赖后台探测：`RefreshStateAsync()` 带重入保护，与构造时或另一次操作的探测重叠时会静默跳过。`ToggleStarshipThemeAsync()` 在写盘成功后直接更新 `_starshipThemeEnabled` 并调用 `RaiseStatusChanged()`；`RefreshStateAsync()` 仍按全部受管 `$PROFILE` 重算状态，作为程序重启或外部改动后的兜底。

## 设置页

配置存 `%AppData%\PAM\settings.json`；除热键外改动即时落盘并 `SignalSettingsChanged()` 通知托盘，热键须通过冲突检测后点「应用热键」。

| 配置项 | 说明 |
| --- | --- |
| 主题 | 亮 / 暗 / 跟随系统（`ThemeService`） |
| 语言 | 简体中文 / English，立即生效（`UiLocalizer` 遍历 XAML，绑定文本由 ViewModel 翻译） |
| 默认终端 | `pwsh` / `powershell` / `auto`，决定热键唤醒的 shell；数据文件始终同一份，切换后主界面重新载入 |
| 打开配置文件 | 按当前终端解析 `$PROFILE`，不存在则创建（UTF-8 BOM）后打开 |
| 快捷键设置 | `KeyInterop` 转 Win32 VK → `HotkeyChord`；`HotkeyConflictDetector` 依次查系统保留 → `RegisterHotKey` 真实占用 → 常见软件软冲突（弹窗二次确认） |
| 终端输入 | `switchImeToEnglish`（默认开）：托盘对新终端窗口切英文输入法，详见 [Tray](../PAM.Tray/AGENT-Tray.md)；日志 `%AppData%\PAM\ime-switch.log` |
| 无托盘行为 | `KeepTrayOnClose`（默认开）：关窗口后托盘与热键保留，关闭则发退出信号；设置页可查看 / 启动 / 结束托盘 |
| 开机自启 | `StartTrayWithWindows`（默认开）：由 `AppServices.Apply` / `Reload` 与 `OnStartup` 幂等同步 HKCU Run 项；设置页切换后回读注册表给出成功 / 失败反馈。目标 exe 不存在时不创建启动项，任务管理器留下的禁用标记会被清除 |
| 别名启动 | `aliasLaunchEnhanced`（默认开）：借 explorer 脱离终端进程树启动；`showAliasRemarkOnLaunch`：启动前打印一行备注；`friendlyCommandNotFound`（默认开）：终端输入未找到的命令时用一行友好提示替代默认红色报错；`hideTerminalAfterAliasLaunch`（默认关）：启动成功后由托管区公共函数先隐藏当前终端窗口、再执行 `exit` 结束会话，不经过托盘；每个新终端首次使用会编译一小段 Win32 声明。四个开关由托管区公共逻辑运行时读取，即时生效但需新开终端窗口 |
| 终端窗口 | 「失去焦点时隐藏终端窗口」不写 `settings.json`：开关按 WT 根级 `autoHideWindow` 的实际值回填，切换时把 `autoHideWindow` 与 `minimizeToNotificationArea` 两项一起写成同值；这是 WT 原生行为，作用于所有终端窗口，需新开窗口生效。「恢复默认设置」也会把两项复位为 false |
| 终端预热 | `warmPoolEnabled`（默认关）：开启后托盘提前打开一个已加载 `$PROFILE` 的隐藏 Windows Terminal 窗口，按热键直接显示；关闭时第一次按热键才打开。两种情况下热键都复用同一个窗口，详见 [Tray](../PAM.Tray/AGENT-Tray.md) |
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
  "startTrayWithWindows": true,
  "switchImeToEnglish": true,
  "aliasLaunchEnhanced": true,
  "showAliasRemarkOnLaunch": true,
  "friendlyCommandNotFound": true,
  "warmPoolEnabled": false,
  "hideTerminalAfterAliasLaunch": false
}
```

- `hotkey` 非法值读取时自动重置；枚举取值见 `AppSettings`。
- 托盘对文件时间戳有轮询兜底，手工编辑 `settings.json` 也会生效。
- PowerShell 7 与 5.1 的 `$PROFILE` 是两个不同文件（`Documents\PowerShell\` 与 `Documents\WindowsPowerShell\`）。PAM 每次保存与启动都把同一份托管区写入两个版本的 `$PROFILE`（已安装的版本各一份），因此友好提示等托管块在任意终端都生效。`MainViewModel.InitializeAsync()` 先比对 `lastBuiltVersion`（`ProfileRenderer.ManagedStructureVersion`）与 `profileStates` 里的文件元数据，仅当结构版本变更或文件被改动时才读取 `$PROFILE` 校验 `managedHash` 与标记块（缺任一块即视为需要重建），任一受管配置文件缺失 / 被改 / 缺块时按 `pam-data.json` 重建全部；`Inspect()` 只读取边界标记之后的内容。

## 其它

- `Services/AppServices.cs`：设置读写唯一入口，广播变更字段；写入成功后按设置值同步登录启动项（注册表写入失败不影响 settings.json）；同时持有 `PamData` 内存快照，`SaveData()` 落盘并替换快照，别名页与自定义命令页都基于 `Clone()` 改写各自字段，避免互相覆盖。`ThemeService` / `UiLocalizer` / `HotkeyConflictDetector` / `Loc` 同目录。
- 分层：`MainWindow.xaml.cs`（焦点 / 状态栏 / 关闭时按 `KeepTrayOnClose` 处理托盘）、`ViewModels/`、`Converters/`、`Controls/`。
- `MainViewModel.IsLoading` 控制空状态显示，加载中不闪现「还没有别名」。
- 图标 `Assets/App.ico` 由 `Assets/generate_icon.py` 生成（需 Pillow），改配色后重跑脚本再构建；`PAM.Tray.exe` 复用同一份。
