# PAM.Core (核心逻辑)

无 UI 依赖的类库，负责所有数据格式、配置与跨进程共享逻辑。App / Tray / Tests 都引用它。

## `$PROFILE` 数据格式

### 别名

```powershell
# @alias-meta blbl 打开哔哩哔哩
Set-Alias -Name blbl -Value "C:\Program Files\App\app.exe"
```

备注写在上一行的 `# @alias-meta` 注释里。读取兼容单双引号，保存统一双引号并转义；只识别和清理上述格式，其余配置原样保留。

### 别名启动器（自有标记块）

`AliasLaunchService` 在保存别名后写入，**不在** PAM-Custom 区块内：

```powershell
# >>> PAM Alias Launcher >>>
# 该区块由 PowerShell 管理器自动维护，请勿手动修改标记行。
function Invoke-PamAlias { ... }
Remove-Item -Path "Alias:bili" -ErrorAction SilentlyContinue
function bili { Invoke-PamAlias -Path "C:\App\bili.exe" -Remark "哔哩哔哩" -Rest $args }
# <<< PAM Alias Launcher <<<
```

- `Set-Alias` 行由 `ProfileService` 维护；Alias 优先级高于 Function，块内先删同名别名再定义同名函数才生效。
- 公共函数借 `explorer.exe` 代启动（带参数经临时 `.lnk`，异常时退回普通 `Start-Process`），新进程完全脱离终端进程树：关终端不影响、日志不回显、UAC 正常，无需重定向 NUL。
- 开关 `aliasLaunchEnhanced` / `showAliasRemarkOnLaunch` 由公共函数运行时读 `settings.json`，改设置即时生效、无需重写 `$PROFILE`；但 **`$PROFILE` 只在终端启动时加载一次，保存后要新开终端窗口**。
- 写入幂等，别名清空时整体移除区块，编码 / BOM / 换行原样保留。

### 自定义命令（受控区块）

由 PAM 独占维护，`#region PAM-Custom` / `#endregion` 包裹：

```powershell
#region PAM-Custom
# 该区块由 PowerShell 管理器自动维护，请勿手动修改标记行。

# @pam-custom hello 打招呼
function hello {
    Write-Host "hi"
}
# @pam-custom-end

# @pam-custom gpr 拉取并变基
# @pam-custom-state disabled
# function gpr {
#     git pull --rebase
# }
# @pam-custom-end
#endregion
```

- 每条命令以 `# @pam-custom <名称> [备注]` 开头、`# @pam-custom-end` 结束，名称唯一，同名以最后一条为准。
- 禁用写 `# @pam-custom-state disabled` 并把代码逐行注释为 `# 原行`（空行写 `#`），重新启用原样还原，缩进与空行不丢失。
- 保存只重写该区块；区块外的配置、别名、编码、BOM 与换行风格原样保留，别名写入也跳过该区块。区块不存在自动创建，清空后保存整体移除。

### 内置命令（随受控区块固定写入）

`CustomCommandService.BuiltinCommands` 是唯一来源（名称 / 备注 / 代码常量）；新增一项即可，测试里的 `BuiltinNames` 同步即可。

| 命令 | 行为 |
| --- | --- |
| `pls` | 列出全部别名及路径、备注 |
| `ongit <信息>` | `git add -A` 后 `git commit -m`（`$args` 合成一条消息） |
| `sudo [-w] <程序/别名> [参数]` | `Start-Process -Verb RunAs` 提权；支持 exe、路径、PAM 别名；`-w` 等待 |

- 不在用户快照里：`Save()` 每次固定追加最新版本，`Parse()` 按名称打 `IsBuiltin`（界面只读）；`IsBuiltinName()` 供命令页与别名页保留名称。
- `EnsureBuiltinPresent()` 缺任一内置命令就重建区块（老配置自动补齐），用户命令原样保留。
- `sudo` 限制：`RunAs` 必新开窗口、CLI 输出不回写；参数须自拼单个字符串；别名路径正则必须拆开拼接，否则被 `pls` 误列（有测试锁住）。系统 `sudo.exe` 会被本函数覆盖，想用系统版敲 `sudo.exe`。

### Starship init 行（美化页受控区块）

`StarshipService` 应用主题预设时写入，自有标记对（**不在** PAM-Custom 区块内，避免被整体重写波及）：

```powershell
# >>> PAM Starship >>>
Invoke-Expression (&starship init powershell)
# <<< PAM Starship <<<
```

写入幂等；游离的 `starship init` 行会归拢进区块，PAM-Custom 区块内的行不动。

## 文件

按功能分文件夹：`Profile/`、`Terminal/`、`Settings/`、`Starship/`、`StarshipPrompt/`、`Models/`；命名空间统一 `PAM.Core`。根目录只留三个跨功能基础设施：`ProfileFileStore.cs`（编码 / BOM / 换行 / 原子替换的底层读写，各写入方共用）、`TrayBridge.cs`（App 与 Tray 的命名事件、托盘探测与启动）、`LocalizationService.cs`（中 → 英词表，支持 `{0}` 占位符，切换时广播事件）。

| 文件 | 职责 |
| --- | --- |
| `Settings/AppSettings.cs` | 设置模型；`Normalize()` 修正手工改坏的值 |
| `Settings/SettingsService.cs` | 读写 `%AppData%\PAM\settings.json`（camelCase、临时文件原子替换）；损坏时回退默认值 |
| `Settings/HotkeyChord.cs` | 与 UI 无关的热键模型、`Ctrl+Alt+T` 风格文本解析、系统保留组合表 |
| `Profile/ProfileService.cs` | 定位 `$PROFILE`、读写别名、`OpenProfileFile`、按终端启动 shell |
| `Profile/PamCustomRegion.cs` | 识别受控区块与行掩码 |
| `Profile/AliasLaunchService.cs` | 维护别名启动器标记块（见上节） |
| `Profile/CustomCommandService.cs` | 受控区块内自定义命令的读写与名称校验；内置命令定义表（`pls` / `ongit` / `sudo`）随区块固定写入、列表中只读展示（`IsBuiltin`）、名称保留校验（`IsBuiltinName`）；`EnsureBuiltinPresent` 供别名页保存后补齐全部内置命令 |
| `Profile/ShellResolver.cs` | `pwsh` / `powershell` / `auto` → 实际 shell，判断是否装了 PowerShell 7 |
| `Starship/StarshipService.cs` | Starship 探测 / Scoop 一键安装 / `starship.toml` 路径与备份恢复 / init 区块 / 内置主题预设（`Presets\*.toml` EmbeddedResource） |
| `StarshipPrompt/StarshipPromptService.cs` | 自定义提示符：托管块 `# >>> PAM Prompt >>>` 写顶层 `format`、`add_newline`，并接管 format 引用到的 `[module]` 段（TOML 不允许重复定义表，段选色内嵌进模块自身 format 重写）；独立备份 `.pam-prompt.bak` 只在首次写入创建，恢复即删除；不引入 TOML 库 |
| `StarshipPrompt/StarshipVariables.cs` | 精选 20 个 Starship 模块变量：中文名 + 分类 + 预览示例值 |
| `Terminal/WindowsTerminalService.cs` | 定位 WT `settings.json`（Stable / Preview / Canary / 非打包）、`ApplyFont()`、原始备份恢复；**备份只在首次写入时创建、之后永不覆盖，恢复后也不删除** |
| `Terminal/WindowsTerminalService.Appearance.cs` | 外观 8 属性读写；`SpliceProperty()` 通用 JSONC 拼接：逐层补齐、保留注释 / 尾逗号 / BOM、整体解析 + 逐项回读校验才原子落盘 |
| `Terminal/TerminalAppearance.cs` | 外观数据模型与 `profiles.defaults` 键位表 |
| `Terminal/FontInstaller.cs` | 注册表枚举字体、按当前用户安装 ttf（HKCU，免管理员） |
| `Models/` | `AliasEntry`、`CustomCommandEntry` |
