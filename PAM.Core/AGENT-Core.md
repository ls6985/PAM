# PAM.Core (核心逻辑)

无 UI 依赖的类库，负责所有数据格式、配置与跨进程共享逻辑。App / Tray / Tests 都引用它。

## 数据文件与 `$PROFILE` 分区

### `pam-data.json`（唯一数据源）

`%AppData%\PAM\pam-data.json`，由 `Data/PamDataService.cs` 读写（camelCase、临时文件原子替换、损坏回退空数据）：

```json
{
  "version": 1,
  "managedHash": "…",
  "aliases": [
    { "name": "blbl", "path": "C:\\Program Files\\App\\app.exe", "remark": "打开哔哩哔哩" }
  ],
  "customCommands": [
    {
      "name": "hello",
      "codeLines": ["function hello {", "    Write-Host 'hi'", "}"],
      "isEnabled": true,
      "remark": "打招呼"
    }
  ]
}
```

- `version` 供结构演进时做兼容；`managedHash` 是托管区文本的 SHA-256，启动时与 `$PROFILE` 实测值比对，不一致即重建。
- 内置命令不写入数据文件，由 `CustomCommandService.BuiltinCommands` 定义表固定生成。
- `settings.json` 仍只存设置，两者不合并：备份、迁移与版本演进互不绑定。

### `$PROFILE` 分区

`Profile/ProfileRenderer.cs` 是唯一写入方。边界标记 `# >>> PAM Managed >>>` 之前为用户区，逐行原样保留；标记之后为托管区，每次按数据全量重写。旧版本散落在用户区里的 Starship / Alias Launcher / PAM-Custom 标记块会被识别为生成物并归拢，不进入用户区。编码、BOM 与换行风格只处理一次。

托管区内部固定顺序：

```powershell
# >>> PAM Managed >>>
# 以下由 PowerShell 管理器自动生成；修改会在下次保存时被覆盖。

# >>> PAM Starship >>>          （存在时保留，由美化页写入）
Invoke-Expression (&starship init powershell)
# <<< PAM Starship <<<

# >>> PAM Alias Launcher >>>
function Invoke-PamAlias { ... }
if (-not (Get-Command -Name "bili" -CommandType Alias,Function -ErrorAction SilentlyContinue)) {
    function bili { Invoke-PamAlias -Path "C:\App\bili.exe" -Remark "哔哩哔哩" -Rest $args }
}
# <<< PAM Alias Launcher <<<

#region PAM-Custom
# ...
#endregion
```

- 用户区已有同名 alias 或 function 时包装函数让位；`Render()` 同时返回冲突名称，界面保存后提示。
- 公共函数借 `explorer.exe` 代启动（带参数经临时 `.lnk`，异常时退回普通 `Start-Process`），新进程完全脱离终端进程树：关终端不影响、日志不回显、UAC 正常。
- 开关 `aliasLaunchEnhanced` / `showAliasRemarkOnLaunch` 由公共函数运行时读 `settings.json`，改设置即时生效、无需重写 `$PROFILE`；但 **`$PROFILE` 只在终端启动时加载一次，保存后要新开终端窗口**。
- 别名清空时整体移除 Launcher 子块；托管区始终含 PAM-Custom 子块（内置命令）。渲染幂等。

### 自定义命令片段

`CustomCommandService.BuildLines()` 按 `codeLines` 生成 `#region PAM-Custom` … `#endregion`：每条命令以 `# @pam-custom <名称> [备注]` 开头、`# @pam-custom-end` 结束；禁用写 `# @pam-custom-state disabled` 并把代码逐行注释为 `# 原行`（空行写 `#`），重新启用原样还原。内置命令按定义表固定追加最新版本。

### 内置命令（随托管区固定写入）

`CustomCommandService.BuiltinCommands` 是唯一来源（名称 / 备注 / 代码常量）；新增一项即可，测试里的 `BuiltinNames` 同步即可。

| 命令 | 行为 |
| --- | --- |
| `pls` | 列出全部别名及路径、备注 |
| `ongit <信息>` | `git add -A` 后 `git commit -m`（`$args` 合成一条消息） |
| `sudo [-w] <程序/别名> [参数]` | `Start-Process -Verb RunAs` 提权；支持 exe、路径、PAM 别名；`-w` 等待 |

- 不在数据快照里：`BuildLines()` 每次固定追加最新版本，`GetBuiltinEntries()` 供界面只读展示；`IsBuiltinName()` 供命令页与别名页保留名称。
- `sudo` 限制：`RunAs` 必新开窗口、CLI 输出不回写；参数须自拼单个字符串；别名路径正则必须拆开拼接，否则被 `pls` 误列（有测试锁住）。系统 `sudo.exe` 会被本函数覆盖，想用系统版敲 `sudo.exe`。

### Starship init 行（美化页受控区块）

`StarshipService` 应用主题预设时写入，自有标记对（**不在** PAM-Custom 区块内，避免被整体重写波及）：

```powershell
# >>> PAM Starship >>>
Invoke-Expression (&starship init powershell)
# <<< PAM Starship <<<
```

写入幂等；托管区里游离的 `starship init` 行会归拢进区块，用户区与 PAM-Custom 区块内的行不动。`ProfileRenderer` 重建托管区时按标记保留该块并置于托管区最前。

## 文件

按功能分文件夹：`Data/`、`Profile/`、`Terminal/`、`Settings/`、`Starship/`、`StarshipPrompt/`、`Models/`；命名空间统一 `PAM.Core`。根目录只留三个跨功能基础设施：`ProfileFileStore.cs`（编码 / BOM / 换行 / 原子替换的底层读写，各写入方共用）、`TrayBridge.cs`（App 与 Tray 的命名事件、托盘探测与启动）、`LocalizationService.cs`（中 → 英词表，支持 `{0}` 占位符，切换时广播事件）。

| 文件 | 职责 |
| --- | --- |
| `Data/PamData.cs` | 托管数据模型（`PamData` / `AliasData` / `CustomCommandData`）、`version` 与 `Normalize()` / `Clone()` |
| `Data/PamDataService.cs` | 读写 `%AppData%\PAM\pam-data.json`（camelCase、临时文件原子替换）；损坏时回退空数据 |
| `Settings/AppSettings.cs` | 设置模型；`Normalize()` 修正手工改坏的值 |
| `Settings/SettingsService.cs` | 读写 `%AppData%\PAM\settings.json`（camelCase、临时文件原子替换）；损坏时回退默认值 |
| `Settings/AutoStartService.cs` | 当前用户登录启动项的读写与幂等同步（HKCU `Run` 的 `PAM.Tray` 值 + Explorer `StartupApproved\Run` 禁用标记）；`BuildRunValue()` / `ExtractExecutablePath()` / `PointsTo()` 是不碰注册表的纯函数 |
| `Settings/HotkeyChord.cs` | 与 UI 无关的热键模型、`Ctrl+Alt+T` 风格文本解析、系统保留组合表 |
| `Profile/ProfileRenderer.cs` | `$PROFILE` 唯一写入方：上下分区、旧生成块归拢、托管区全量重写、哈希与同名冲突检测 |
| `Profile/ProfileService.cs` | 定位 `$PROFILE`、`OpenProfileFile`、执行策略、按终端启动 shell；另提供路径规范化与 PowerShell 字符串转义的公共静态方法 |
| `Profile/PamCustomRegion.cs` | 识别受控区块与行掩码 |
| `Profile/AliasLaunchService.cs` | `BuildLines()` 产出别名启动器片段（含同名避让与公共函数），不读写文件 |
| `Profile/CustomCommandService.cs` | `BuildLines()` 产出 PAM-Custom 片段；内置命令定义表（`pls` / `ongit` / `sudo`）、`GetBuiltinEntries()` 只读展示、`IsBuiltinName()` / `TryValidateName()` 名称校验；不读写文件 |
| `Profile/ShellResolver.cs` | `pwsh` / `powershell` / `auto` → 实际 shell，判断是否装了 PowerShell 7 |
| `Starship/StarshipService.cs` | Starship 探测 / Scoop 一键安装 / `starship.toml` 路径与备份恢复 / init 区块 / 内置主题预设（`Presets\*.toml` EmbeddedResource） |
| `StarshipPrompt/StarshipPromptService.cs` | 自定义提示符：托管块 `# >>> PAM Prompt >>>` 写顶层 `format`、`add_newline`，并接管 format 引用到的 `[module]` 段（TOML 不允许重复定义表，段选色内嵌进模块自身 format 重写）；独立备份 `.pam-prompt.bak` 只在首次写入创建，恢复即删除；不引入 TOML 库 |
| `StarshipPrompt/StarshipVariables.cs` | 精选 20 个 Starship 模块变量：中文名 + 分类 + 预览示例值 |
| `Terminal/WindowsTerminalService.cs` | 定位 WT `settings.json`（Stable / Preview / Canary / 非打包）、`ApplyFont()`、原始备份恢复；**备份只在首次写入时创建、之后永不覆盖，恢复后也不删除** |
| `Terminal/WindowsTerminalService.Appearance.cs` | 外观 8 属性读写；`SpliceProperty()` 通用 JSONC 拼接：逐层补齐、保留注释 / 尾逗号 / BOM、整体解析 + 逐项回读校验才原子落盘 |
| `Terminal/TerminalAppearance.cs` | 外观数据模型与 `profiles.defaults` 键位表 |
| `Terminal/FontInstaller.cs` | 注册表枚举字体、按当前用户安装 ttf（HKCU，免管理员） |
| `Models/` | `AliasEntry`、`CustomCommandEntry`：界面绑定模型（含 `INotifyPropertyChanged`），不参与 JSON 序列化 |
