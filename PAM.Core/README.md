# PAM.Core (核心逻辑)

无 UI 依赖的类库，负责所有数据格式、配置与跨进程共享逻辑。三个项目都引用它。

## `$PROFILE` 数据格式

### 别名

```powershell
# @alias-meta blbl 打开哔哩哔哩
Set-Alias -Name blbl -Value "C:\Program Files\App\app.exe"
```

备注写在上一行的 `# @alias-meta` 注释里。读取兼容单双引号，保存统一双引号并转义；只识别和清理上述格式，其余配置原样保留。

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

| 文件 | 职责 |
| --- | --- |
| `AppSettings.cs` | 设置模型（`Hotkey`、`TerminalType`、`Theme`、`Language`、`KeepTrayOnClose`、`AutoLaunchTerminal`）；`Normalize()` 修正手工改坏的值 |
| `SettingsService.cs` | 读写 `%AppData%\PAM\settings.json`（camelCase、临时文件原子替换，避免托盘读到半截 JSON）；损坏时回退默认值 |
| `ProfileService.cs` | 定位 `$PROFILE`（`ResolveProfilePath` 按终端解析并回报是否回退）、读写别名、`OpenProfileFile` 创建并打开配置文件、`LaunchPowerShell` 按终端启动 shell 并 dot-source 刚写入的配置 |
| `ProfileFileStore.cs` | 底层文件读写：编码 / BOM、换行风格、原子替换，别名与自定义命令共用 |
| `PamCustomRegion.cs` | 识别受控区块（含未闭合区块容错）与行掩码 |
| `CustomCommandService.cs` | 受控区块内自定义命令的读写与名称校验 |
| `Models/` | `AliasEntry`、`CustomCommandEntry`（状态文案经本地化服务翻译） |
| `HotkeyChord.cs` | 与 UI 无关的全局热键模型（修饰键 + Win32 VK）、`Ctrl+Alt+T` 风格文本解析、系统保留组合表、常见软件快捷键提示 |
| `ShellResolver.cs` | `pwsh` / `powershell` / `auto` → 实际 shell，并判断是否安装 PowerShell 7 |
| `StarshipService.cs` | Starship 探测 / Scoop 与一键安装 / `starship.toml` 路径与备份恢复 / `$PROFILE` init 区块 / 内置主题预设（`Presets\*.toml` EmbeddedResource）。测试可注入配置路径 |
| `WindowsTerminalService.cs` | 定位 WT `settings.json`（Stable / Preview / Canary / 非打包），读写 `profiles.defaults.font.face`：JSONC 文本拼接保注释、写前备份、校验不过不落盘 |
| `FontInstaller.cs` | 注册表枚举已装字体与等宽候选、内置字体是否已装、按当前用户安装 ttf（复制 + HKCU 注册 + 广播 `WM_FONTCHANGE`），免管理员权限 |
| `TrayBridge.cs` | App 与 Tray 之间的命名事件（设置变更 / 请求退出）、托盘进程探测与启动 |
| `LocalizationService.cs` | 中文原文 → 英文词表，支持精确匹配与带 `{0}` 占位符的动态消息匹配；切换语言时广播事件 |
