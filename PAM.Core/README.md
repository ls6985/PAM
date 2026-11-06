# PAM.Core (核心逻辑)

无任何 UI 依赖的类库。

- `Models/AliasEntry.cs`：别名数据模型，包含 `Name`、`Path`、`Remark` 及删除确认状态 `DeletePending`。
- `ProfileService.cs`：`$PROFILE` 定位与读写。
  - 定位：调用 `powershell -NoProfile -Command "$PROFILE"`。
  - 读写：解析特定格式，保留编码与 BOM。
  - 多目标：探测并遍历 PowerShell 5.1 与 7 的路径。
  - 启动：`LaunchPowerShell` 使用 `wt.exe` 拉起终端。