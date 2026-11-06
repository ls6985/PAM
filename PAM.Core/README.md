# PAM.Core (核心逻辑)

无任何 UI 依赖的类库。

- `Models/AliasEntry.cs`：别名数据模型，包含 `Name`、`Path`、`Remark` 及删除确认状态 `DeletePending`。
- `AppSettings.cs`：应用设置模型（热键 `Hotkey`、终端类型 `TerminalType`（`pwsh` / `powershell`）、`AutoLaunchTerminal`），为设置页与自定义命令预留。
- `SettingsService.cs`：把 `AppSettings` 读写到 `%AppData%\PAM\settings.json`（camelCase、缩进）；文件不存在或损坏时回退默认值。
- `ProfileService.cs`：`$PROFILE` 定位与读写。
  - 定位：调用 `powershell -NoProfile -Command "$PROFILE"`。
  - 读写：解析特定格式，保留编码与 BOM。
  - 受控区块：读取和清理别名时都会跳过 `#region PAM-Custom` 区块。
  - 启动：`LaunchPowerShell` 使用 `wt.exe` 拉起终端。
- `ProfileFileStore.cs`：`$PROFILE` 底层文件读写的共享实现（编码/BOM 识别、换行风格、临时文件原子替换），别名与自定义命令共用。
- `PamCustomRegion.cs`：识别 `#region PAM-Custom` 受控区块（含未闭合区块的容错）。
- `CustomCommandService.cs`：受控区块内自定义命令的读写。
  - 区块格式：`# @pam-custom <名称> [备注]` 头 + `# @pam-custom-state disabled`（可选）+ 代码 + `# @pam-custom-end`。
  - 禁用命令写为逐行注释，读取时自动还原；同名命令以最后一条为准。
  - `TryValidateName` 校验命令名可作为 PowerShell 函数名使用。
- `Models/CustomCommandEntry.cs`：自定义命令模型（`Name`、`Code`、`Remark`、`IsEnabled`、`DeletePending`）。