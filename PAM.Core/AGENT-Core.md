# PAM.Core（核心逻辑）

无 UI 依赖的类库，负责数据格式、配置、渲染与跨进程共享逻辑；App / Tray / Tests 都引用它。本文件给出改 Core 时需要遵循的数据与 `$PROFILE` 约定，以及文件索引。

## 数据文件

### `pam-data.json`（唯一数据源）

`%AppData%\PAM\pam-data.json`，由 `Data/PamDataService.cs` 读写（camelCase、临时文件原子替换、损坏回退空数据）：

```json
{
  "version": 1,
  "managedHash": "…",
  "lastBuiltVersion": "1",
  "profileStates": [{ "profilePath": "C:\\Users\\…\\Microsoft.PowerShell_profile.ps1", "lastWriteUtc": "2026-01-01T00:00:00Z", "length": 2048 }],
  "aliases": [{ "name": "blbl", "path": "C:\\Program Files\\App\\app.exe", "remark": "打开哔哩哔哩", "arguments": "--new-window" }],
  "customCommands": [{ "name": "hello", "codeLines": ["…"], "isEnabled": true, "remark": "打招呼", "allowsArguments": false, "arguments": "" }]
}
```

- `version` 供结构演进时做兼容；`managedHash` 是托管区文本的 SHA-256。两份 `$PROFILE`（PowerShell 7 与 5.1）内容一致、哈希相同，故只存一份。
- `lastBuiltVersion` 记录上次重建 / 自检时的托管区结构版本（`ProfileRenderer.ManagedStructureVersion`），`profileStates` 记录各受管 `$PROFILE` 的写入时间与大小。启动自检先比对这两项，仅当结构版本变更或文件元数据变化时才读取 `$PROFILE` 校验哈希与标记块，常态下跳过读取与重建。
- 别名的 `arguments` 是每次启动固定附带的参数字符串：按空白分词，双引号可包裹含空格的段；留空时生成的包装函数只传 `$args`。
- 内置命令不写入数据文件，由 `CustomCommandService.BuiltinCommands` 定义表固定生成。
- `settings.json` 只存设置，两者不合并：备份、迁移与版本演进互不绑定。`warmPoolEnabled` 等纯托盘行为开关不进入此文件、不参与 `ProfileRenderer` 渲染、不改变 `managedHash`，只在运行时由 PAM.Tray 读取。
- 失焦隐藏与单窗口这两个开关不落在 `settings.json` / `pam-data.json`：状态由 Windows Terminal `settings.json` 的四个根级属性承载，界面按实际值读回。`hideTerminalAfterAliasLaunch` 是 `settings.json` 的普通布尔字段，默认关；键名沿用 hide 前缀以保持既有配置兼容，行为是敲下别名回车即隐藏当前终端窗口，启动在后台继续，会话不结束。

### `settings.json`

字段与界面行为见 [App](../PAM.App/AGENT-App.md)，模型定义在 `Settings/AppSettings.cs`（`Normalize()` 修正非法值）。

## `$PROFILE` 托管区

`Profile/ProfileRenderer.cs` 是唯一写入方。边界标记 `# >>> PAM Managed >>>` 之前为用户区，逐行原样保留；之后为托管区，每次按数据全量重写。旧版本散落在用户区里的 Starship / Alias Launcher / PAM-Custom 标记块会被识别为生成物并归拢，不进入用户区。编码、BOM 与换行风格只处理一次；写入以 **UTF-8 BOM** 落盘，否则 Windows PowerShell 5.1 会把含中文或英文单引号的行读成乱码、整块解析失败。

托管区内部固定顺序：

1. `# >>> PAM Starship >>>` — 常驻；关闭主题时区块内每行加 `# ` 前缀（完整内容见 `StarshipService.InitBlockLines`）
2. `# >>> PAM Alias Launcher >>>` — 公共函数 `Invoke-PamAlias` 与各别名的同名包装函数；别名清空时整块移除。公共函数运行时读 `hideTerminalAfterAliasLaunch`，一进入就隐藏当前前台的 WT 窗口，随后在后台完成启动，不结束会话
3. `# >>> PAM Command Hint >>>` — 注册 `CommandNotFoundAction` 的未知命令友好提示
4. `#region PAM-Custom` … `#endregion` — 始终存在（内置命令）

要点：

- 用户区已有同名 alias 或 function 时包装函数让位；`Render()` 同时返回冲突名称，界面保存后提示。
- 别名包装函数借 `Get-Command` 避让同名项。公共函数借 `explorer.exe` 代启动（带参数经临时 `.lnk`，异常时退回普通 `Start-Process`），新进程完全脱离终端进程树：关终端不影响、日志不回显、UAC 正常。
- 配置固定参数时 `-Rest` 以数组字面量开头，终端调用时临时传入的 `$args` 接在其后；未配置时仍是 `-Rest $args`。参数按字面量生成，不做变量展开。
- `aliasLaunchEnhanced` / `showAliasRemarkOnLaunch` / `friendlyCommandNotFound` / `hideTerminalAfterAliasLaunch` 由托管区公共逻辑运行时读 `settings.json`，改设置即时生效、无需重写 `$PROFILE`。`$PROFILE` 只在终端启动时加载一次，保存后需新开终端窗口。
- 「按回车即隐藏」全在托管区完成：读完开关后立即用 `GetForegroundWindow()` 取当前前台窗口，按类名 `CASCADIA_HOSTING_WINDOW_CLASS` 校验它就是 Windows Terminal 窗口（否则不隐藏，避免误伤其它应用），随即 `ShowWindow(SW_HIDE)`；之后的备注输出与启动动作都在后台继续，窗口不关闭、会话不结束。隐藏不经过托盘，托盘是否运行都不影响；托盘持有该窗口时按热键可 `ShowWindow(SW_SHOW)` 唤回同一个会话，会话状态（当前目录、历史、变量）保留。Win32 声明用 `Add-Type -MemberDefinition` 按 `PAM.Win32` 类型名复用，每个新终端会话首次编译一次，失败时静默跳过隐藏、照常启动。托管区文本变化不改变数据哈希，靠 `ManagedStructureVersion` 与标记检查触发重建。
- 未知命令提示只在 `friendlyCommandNotFound` 开启时接管：红色 `CommandNotFoundException` 被一段 `CommandNotFoundAction` 替换为单行友好提示（含命令名），并借 `CommandScriptBlock` 返回空块，使 `Get-Command` 等内部查找不产生提示。
- 重建判定用 `ProfileRenderer.NeedsRebuild()`：哈希不符，或任一受管 `$PROFILE` 缺当前版本固定写入的标记块，即整体重建。Command Hint 与 PAM-Custom 两块始终检查；Alias Launcher 只在存在时检查它是否含当前版本的「进入即隐藏」片段 `AliasLaunchService.HideOnAliasMarker`（没有别名时该块本就不写入；旧版的 `exit` 片段不含此串）。`RenderToProfiles()` 按同一份数据写入多份 `$PROFILE`。渲染幂等。

### 自定义命令片段

`CustomCommandService.BuildLines()` 按 `codeLines` 生成 `#region PAM-Custom` … `#endregion`：每条命令以 `# @pam-custom <名称> [备注]` 开头、`# @pam-custom-end` 结束；禁用写 `# @pam-custom-state disabled` 并把代码逐行注释为 `# 原行`（空行写 `#`），重新启用原样还原。内置命令按定义表固定追加最新版本。

- 用户命令写成同名函数：`codeLines` 存命令体，`CustomCommandCode.BuildFunctionLines()` 把它包成 `function <名称> { … }`；未包装的裸命令会随 `$PROFILE` 加载立即执行。
- 函数体左对齐、不额外缩进：PowerShell 5.1 要求 here-string 结束标记（`"@` / `'@`）位于行首，缩进会改变内容甚至解析失败。
- `allowsArguments` 为真时，`arguments`（固定参数）与 `@args` 依次追加到命令体最后一个非空行末尾。函数没有 `param` 块，`$args` 自动收集调用参数，`@args` 负责展开；关闭开关时函数体内仍可直接用 `$args`。
- 旧数据兼容：早期版本要求代码写完整的 `function <名称> { … }`。`CustomCommandCode.StripOuterFunction()` 在读取、提交与渲染三处剥掉同名外壳（不是同名函数、花括号不配对时原样返回），数据在下次保存时落成命令体。

### 内置命令（随托管区固定写入）

`CustomCommandService.BuiltinCommands` 是唯一来源（名称 / 备注 / 代码常量）；新增一项时同步测试里的 `BuiltinNames`。

| 命令 | 行为 |
| --- | --- |
| `pls` | 列出全部别名及路径、备注 |
| `ongit <信息>` | `git add -A` 后 `git commit -m`（`$args` 合成一条消息） |
| `sudo [-w] <程序/别名> [参数]` | `Start-Process -Verb RunAs` 提权；支持 exe、路径、PAM 别名；`-w` 等待 |

`GetBuiltinEntries()` 供界面只读展示，`IsBuiltinName()` 供命令页与别名页保留名称。`sudo` 的 `RunAs` 必新开窗口、CLI 输出不回写，参数须自拼单个字符串；别名路径正则拆开拼接，否则会被 `pls` 的别名扫描误列。系统 `sudo.exe` 会被本函数覆盖。

### Starship 主题启用 / 关闭

`StarshipService.InitBlockLines` 是 `EnsureProfileInit()` 写入的完整区块，自有 `# >>> PAM Starship >>>` 标记对，不在 PAM-Custom 区块内。区块把 `--print-full-init` 的输出缓存到 `%LOCALAPPDATA%\starship-init-full.ps1`（UTF-8 BOM，兼容 Windows PowerShell 5.1），启动时直接加载静态脚本，仅缓存缺失时重新生成，生成失败回退官方 `Invoke-Expression (&starship init powershell)` 写法。区块末尾保留 `InitLine`，使 `HasInitLine()` 仍能识别主题已启用；该方法跳过注释行，因此关闭状态下返回 false。

- 主题开关不删除区块：`IsProfileInitEnabled()` 以区块内是否存在未注释的可执行行作为界面开关的唯一状态来源；`SetProfileInitEnabled()` 关闭时给区块内每一行（两个标记行除外）统一加 `# ` 前缀，开启时统一去掉，两者互为逆操作、可逐字节往返且幂等。用户区与 PAM-Custom 区块内的行不动，编码、BOM 与换行风格保持。
- 区块缺失且要启用时按 `InitBlockLines` 重建；`ProfileRenderer` 重建托管区时按标记逐行原样保留该块并置于托管区最前，注释状态不会因重建而丢失。
- `ApplyPreset()` 的 `ensureProfileInit` 参数为 false 时只写 `starship.toml`，供主题开关关闭时保存设置，不把注释状态写回。重新启用不需要重选主题，`starship.toml` 与初始化缓存都保留。

## 文件索引

按功能分文件夹：`Data/`、`Profile/`、`Terminal/`、`Settings/`、`Starship/`、`StarshipPrompt/`、`Models/`；命名空间统一 `PAM.Core`。根目录只留三个跨功能基础设施：`ProfileFileStore.cs`（编码 / BOM / 换行 / 原子替换的底层读写，各写入方共用）、`TrayBridge.cs`（两个命名事件：设置变更 / 请求退出，以及托盘探测与启动）、`LocalizationService.cs`（中 → 英词表，支持 `{0}` 占位符，切换时广播事件）。

| 文件 | 职责 |
| --- | --- |
| `Data/PamData.cs` | 托管数据模型（`PamData` / `AliasData` / `CustomCommandData`）、`version` 与 `Normalize()` / `Clone()`；`CustomCommandData` 含 `codeLines`、`allowsArguments`、`arguments` |
| `Data/PamDataService.cs` | 读写 `%AppData%\PAM\pam-data.json`（camelCase、临时文件原子替换）；损坏时回退空数据 |
| `Settings/AppSettings.cs` | 设置模型；`Normalize()` 修正手工改坏的值 |
| `Settings/SettingsService.cs` | 读写 `%AppData%\PAM\settings.json`；损坏时回退默认值 |
| `Settings/AutoStartService.cs` | 登录启动项的读写与幂等同步（HKCU `Run` 的 `PAM.Tray` 值 + Explorer `StartupApproved\Run` 禁用标记）；`BuildRunValue()` / `ExtractExecutablePath()` / `PointsTo()` 是不碰注册表的纯函数 |
| `Settings/HotkeyChord.cs` | 与 UI 无关的热键模型、`Ctrl+Alt+T` 风格文本解析、系统保留组合表 |
| `Profile/ProfileRenderer.cs` | `$PROFILE` 唯一写入方：上下分区、旧生成块归拢、托管区全量重写、哈希与同名冲突检测 |
| `Profile/ProfileService.cs` | 定位 `$PROFILE`、`OpenProfileFile`、执行策略、按终端启动 shell；路径规范化与 PowerShell 字符串转义的公共静态方法 |
| `Profile/PamCustomRegion.cs` | 识别受控区块与行掩码 |
| `Profile/AliasLaunchService.cs` | `BuildLines()` 产出别名启动器片段（同名避让、固定启动参数拆分与公共函数），不读写文件 |
| `Profile/CommandHintService.cs` | `BuildLines()` 产出未知命令提示片段：注册 `CommandNotFoundAction`，运行时读 `friendlyCommandNotFound` 与当前语言，按 `CommandScriptBlock` 接管输出；不读写文件 |
| `Profile/CustomCommandService.cs` | `BuildLines()` 产出 PAM-Custom 片段；内置命令定义表（`pls` / `ongit` / `sudo`）、`GetBuiltinEntries()`、`IsBuiltinName()` / `TryValidateName()`；不读写文件 |
| `Profile/CustomCommandCode.cs` | 命令体 ↔ 函数定义转换：`BuildFunctionLines()`、`StripOuterFunction()`；纯字符串处理 |
| `Profile/ShellResolver.cs` | `pwsh` / `powershell` / `auto` → 实际 shell，判断是否装了 PowerShell 7 |
| `Starship/StarshipService.cs` | Starship 探测 / Scoop 一键安装 / `starship.toml` 路径与备份恢复 / 受控初始化区块的静态缓存与注释式主题启停 / 内置预设（`Presets\*.toml` EmbeddedResource） |
| `StarshipPrompt/StarshipPromptService.cs` | 自定义提示符：托管块 `# >>> PAM Prompt >>>` 写顶层 `format`、`add_newline`，并接管 format 引用到的 `[module]` 段（TOML 不允许重复定义表，段选色内嵌进模块自身 format 重写）；独立备份 `.pam-prompt.bak` 只在首次写入创建，恢复即删除；不引入 TOML 库 |
| `StarshipPrompt/StarshipVariables.cs` | 精选 20 个 Starship 模块变量：中文名 + 分类 + 预览示例值 |
| `Terminal/WindowsTerminalService.cs` | 定位 WT `settings.json`（Stable / Preview / Canary / 非打包）、`ApplyFont()`、原始备份恢复；备份只在首次写入时创建、之后永不覆盖，恢复后也不删除 |
| `Terminal/WindowsTerminalService.Appearance.cs` | 12 个设置项（8 个 `profiles.defaults` + 4 个根级窗口属性）的批量读写；`SpliceProperty()` 通用 JSONC 拼接：逐层补齐、保留注释 / 尾逗号 / BOM、整体解析 + 逐项回读校验才原子落盘 |
| `Terminal/WindowsTerminalService.Global.cs` | `ReadGlobalFlags()` 回读四个根级布尔属性；写入复用 `ApplyAppearance()`，保证多个界面入口写同一文件时不互相覆盖 |
| `Terminal/TerminalAppearance.cs` | 设置项枚举、`profiles.defaults` 与根级属性的键位表、合法值校验 |
| `Terminal/FontInstaller.cs` | 注册表枚举字体、按当前用户安装 ttf（HKCU，免管理员） |
| `Models/` | `AliasEntry`、`CustomCommandEntry`：界面绑定模型（含 `INotifyPropertyChanged`），不参与 JSON 序列化 |
