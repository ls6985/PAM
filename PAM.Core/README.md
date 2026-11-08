# PAM.Core (核心逻辑)

无 UI 依赖的类库，负责所有数据格式、配置与跨进程共享逻辑。三个项目都引用它。

## `$PROFILE` 数据格式

### 别名

```powershell
# @alias-meta blbl 打开哔哩哔哩
Set-Alias -Name blbl -Value "C:\Program Files\App\app.exe"
```

备注写在上一行的 `# @alias-meta` 注释里。读取兼容单双引号，保存统一双引号并转义；只识别和清理上述格式，其余配置原样保留。

### 别名启动器（自有标记块）

由 `AliasLaunchService` 在保存别名后写入，同样**不在** PAM-Custom 区块内：

```powershell
# >>> PAM Alias Launcher >>>
# 该区块由 PowerShell 管理器自动维护，请勿手动修改标记行。
function Invoke-PamAlias { ... }
Remove-Item -Path "Alias:bili" -ErrorAction SilentlyContinue
function bili { Invoke-PamAlias -Path "C:\App\bili.exe" -Remark "哔哩哔哩" -Rest $args }
# <<< PAM Alias Launcher <<<
```

`Set-Alias` 行仍由 `ProfileService` 维护且保持不变：PowerShell 里 Alias 优先级高于 Function，所以块内先删同名别名、再定义同名函数才会生效。公共函数运行时读 `%AppData%\PAM\settings.json` 的 `aliasLaunchEnhanced` / `showAliasRemarkOnLaunch`，决定是否脱离终端启动以及是否 `Write-Host` 备注；改设置即时生效，无需重写 `$PROFILE`。

**关终端连带关闭的根因与解法**：`Start-Process` 只保证 PowerShell 不阻塞等待，子进程仍挂在终端的控制台 / 进程树里；Electron 等程序还会 `AttachConsole(ATTACH_PARENT_PROCESS)` 把日志刷回终端。把 stdout/stderr 重定向到 NUL 只能掐掉日志、改不了控制台归属。现在**借 explorer.exe 代启动**：explorer 不属于任何终端控制台，由它 ShellExecute 启动的程序父进程就是 explorer，完全脱离终端进程树 → 关终端无影响、日志自然不显示、UAC 提升照常，也不需要重定向（回到 ShellExecute 路径，实测启动耗时略快）。无参数时直接把 exe 路径交给 explorer；带参数时先用 `WScript.Shell` COM 生成一个临时 `.lnk`（文件名按路径 hash 固定、每次覆盖复用、不删除以免 explorer 异步读取时文件已消失），参数原样传入。COM / explorer / 路径不存在时 `catch` 退回普通 `Start-Process`。注意 `$PROFILE` 只在终端启动时加载一次，保存别名后要**新开终端窗口**才生效。写入幂等，别名清空时整体移除区块，编码 / BOM / 换行原样保留。

### 自定义命令（受控区块）

由 PAM 独占维护，用 `#region PAM-Custom` / `#endregion` 包裹：

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

- 每条命令以 `# @pam-custom <名称> [备注]` 开头、`# @pam-custom-end` 结束，名称是唯一键；同名以最后一条为准。
- 禁用时写 `# @pam-custom-state disabled` 并把代码逐行注释为 `# 原行`（空行写 `#`），重新启用后原样还原，缩进与空行不丢失。
- 保存时只重写该区块；区块外的配置、别名、编码、BOM 与换行风格原样保留。别名保存也会跳过该区块，两种写入互不破坏。
- 区块不存在时自动创建；列表清空后保存会整体移除区块。

### Starship init 行（美化页受控区块）

应用主题预设时由 `StarshipService` 写入，用自有标记对（**不在** PAM-Custom 区块内，避免被上面的整体重写波及）：

```powershell
# >>> PAM Starship >>>
Invoke-Expression (&starship init powershell)
# <<< PAM Starship <<<
```

写入幂等：重跑只保留一份；游离的 `starship init` 行会被归拢进区块，但 PAM-Custom 区块内的行不动。

## 文件

按功能分文件夹：`Profile/`（$PROFILE 定位、读写与区块维护）、`Terminal/`（Windows Terminal 配置、外观与字体）、`Settings/`（设置模型与持久化）、`Starship/` 与 `StarshipPrompt/`（Starship 主题与自定义提示符）、`Models/`（数据模型）。命名空间统一为 `PAM.Core`，不随文件夹变化；根目录只留三个跨功能基础设施：`ProfileFileStore.cs`、`TrayBridge.cs`、`LocalizationService.cs`。

| 文件 | 职责 |
| --- | --- |
| `Settings/AppSettings.cs` | 设置模型（`Hotkey`、`TerminalType`、`Theme`、`Language`、`KeepTrayOnClose`、`AutoLaunchTerminal`、`SwitchImeToEnglish`、`AliasLaunchEnhanced`、`ShowAliasRemarkOnLaunch`）；`Normalize()` 修正手工改坏的值 |
| `Settings/SettingsService.cs` | 读写 `%AppData%\PAM\settings.json`（camelCase、临时文件原子替换，避免托盘读到半截 JSON）；损坏时回退默认值 |
| `Profile/ProfileService.cs` | 定位 `$PROFILE`（`ResolveProfilePath` 按终端解析并回报是否回退）、读写别名、`OpenProfileFile` 创建并打开配置文件、`LaunchPowerShell` 按终端启动 shell 并 dot-source 刚写入的配置 |
| `ProfileFileStore.cs` | 底层文件读写：编码 / BOM、换行风格、原子替换；别名、自定义命令与 Starship / 终端配置写入共用 |
| `Profile/PamCustomRegion.cs` | 识别受控区块（含未闭合区块容错）与行掩码 |
| `Profile/AliasLaunchService.cs` | 维护别名启动器自有标记块：同名函数包装 + `Invoke-PamAlias` 公共函数（explorer 代启动 / 带参数时临时 `.lnk` / 备注），开关由函数运行时读 settings.json |
| `Profile/CustomCommandService.cs` | 受控区块内自定义命令的读写与名称校验 |
| `Models/` | `AliasEntry`、`CustomCommandEntry`（状态文案经本地化服务翻译） |
| `Settings/HotkeyChord.cs` | 与 UI 无关的全局热键模型（修饰键 + Win32 VK）、`Ctrl+Alt+T` 风格文本解析、系统保留组合表、常见软件快捷键提示 |
| `Profile/ShellResolver.cs` | `pwsh` / `powershell` / `auto` → 实际 shell，并判断是否安装 PowerShell 7 |
| `Starship/StarshipService.cs` | Starship 探测 / Scoop 与一键安装 / `starship.toml` 路径与备份恢复 / `$PROFILE` init 区块 / 内置主题预设（`Presets\*.toml` EmbeddedResource）。测试可注入配置路径 |
| `StarshipPrompt/StarshipPromptService.cs` | 自定义提示符：存**一整行** Starship format（不再是六段模型）+ `PromptPalette` 12 色色板 + `ParsePreview()` 预览解析；托管块 `# >>> PAM Prompt >>>` 内写顶层 `format` 与 `add_newline`（提示符前空一行开关，默认 false），并接管 format 里引用到的模块段落——顶层 format 的外层 `(fg:…)` 只能染色裸文字，段落里的 format / 样式会盖过它，且 TOML 不允许重复定义表，所以被引用的 `[module]`（含 `[module.x]` 子表）整段移走、由托管块把段选色内嵌进模块自身 format 重写（模板在 `StarshipPromptService.Modules.cs`），未被引用的段落 / 注释 / CRLF / 无 BOM UTF-8 原样保留；块插在第一个段落表头之前，避免块内表头捕获根级键。写入只做 TOML 双引号层转义，读回即编辑框原文（只认块内第一个表头之前的 format 行），保证 `$`、`[`、`]`、`\`、引号可往返；`add_newline` 从块内同区域读回、`use_os_path_sep` 从块内 `[directory]` 段读回，缺失时按 `CreateDefaultOptions()` 回显；接管 `[directory]` 段时 `use_os_path_sep` 两种取值都显式落盘（false = `/` 分隔，默认），块外的旧顶层 `add_newline` / `format` 移走防 TOML 重复键；`Validate()` 拒绝空 / 多行 / 非 `#RRGGBB` 颜色。独立备份 `.pam-prompt.bak` 只在首次写入时创建，`Restore()` 还原原始文件并删除备份（"恢复应用前配置"按钮的可用性 = 有未撤销的备份）。不引入 TOML 库，配置即 toml 本身 |
| `StarshipPrompt/StarshipVariables.cs` | 精选 20 个 Starship 模块变量（取自 `starship module --list`），中文名 + 英文名称 + 四组分类 + 预览示例值；用户名 / 主机名 / 当前路径等动态值由 Starship 运行时读取，UI 只让"选"不让"填" |
| `Terminal/WindowsTerminalService.cs` | 定位 WT `settings.json`（Stable / Preview / Canary / 非打包）；`GetState()` 报告字体 / 原始备份路径与时间；`ApplyFont()` 写 `profiles.defaults.font.face`；`RestoreBackup()` 恢复原始配置。**备份只在首次写入时创建、之后永不覆盖，恢复后也不删除**——所以恢复永远回到安装本软件前的配置 |
| `Terminal/WindowsTerminalService.Appearance.cs` | 外观读写：`ReadAppearance()` 读回 `profiles.defaults` 的 8 个属性（null = 未设置 / 跟随 WT 默认），`ApplyAppearance(changes)` 只提交有差异的项；`SpliceProperty()` 是通用 JSONC 拼接器——逐层补齐对象、替换 / 删除标量、按 JsonDocument 的值原文定位本层属性（不会误改嵌套对象里的同名项）、保留注释 / 尾逗号 / BOM / 缩进，整体解析 + 逐项回读校验通过才原子落盘 |
| `Terminal/TerminalAppearance.cs` | 外观数据模型：`TerminalAppearance`（读回值）、`TerminalAppearanceChange`（Set / Remove 一项）、`TerminalAppearanceKey` 枚举、`TerminalAppearanceSchema`（键 → `profiles.defaults` 路径、光标形状清单、合法值校验） |
| `Terminal/FontInstaller.cs` | 注册表枚举已装字体与等宽候选、内置字体是否已装、按当前用户安装 ttf（复制 + HKCU 注册 + 广播 `WM_FONTCHANGE`），免管理员权限 |
| `TrayBridge.cs` | App 与 Tray 之间的命名事件（设置变更 / 请求退出）、托盘进程探测与启动 |
| `LocalizationService.cs` | 中文原文 → 英文词表，支持精确匹配与带 `{0}` 占位符的动态消息匹配；切换语言时广播事件 |
