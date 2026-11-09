# PowerShell 管理器（AI 必读索引）

基于 **WPF + WinForms + .NET 10** 的 Windows 终端配置工具：用图形界面维护别名与自定义命令并把它们生成到 PowerShell `$PROFILE` 的托管区，由一个后台托盘程序提供全局热键唤醒终端。

> 本文件是给 AI 的必读索引：只记录项目结构、进程关系、环境与运行方式、数据位置。各模块的实现细节在对应项目的 AGENT 文档里，按下表选读，本文件不重复。

## 项目

| 项目                                 | 职责                                                                                                                    | 什么时候读                                                  |
| ---------------------------------- | --------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------ |
| [PAM.Core](PAM.Core/AGENT-Core.md) | 无 UI 的核心库：托管数据模型与 `pam-data.json` 读写、`$PROFILE` 分区渲染、设置模型、热键描述、终端探测、本地化词表、跨进程信号、Starship / 字体 / Windows Terminal 外观服务 | 改数据格式、配置字段、`$PROFILE` 分区规则、终端外观或美化功能时                  |
| [PAM.App](PAM.App/AGENT-App.md)    | WPF 主界面与设置页，用户唯一直接操作的程序，也是 `settings.json` 的唯一写入方                                                                     | **最常读**：改界面、页签、主题 / 语言 / 热键交互、终端外观、Starship 美化时        |
| [PAM.Tray](PAM.Tray/AGENT-Tray.md) | 纯粹的后台组件：常驻托盘、注册全局热键、按下后拉起 Windows Terminal；只读配置，从不写入，也不需要用户手动启动                                                       | 只有改热键注册或托盘行为时才需要看，日常开发基本不用碰                            |
| [PAM.Tests](PAM.Tests/AGENT-测试.md) | xUnit 测试，只覆盖 PAM.Core 的纯逻辑，不涉及 WPF 界面                                                                                 | 平时不用读；只有改动 `$PROFILE` 区块格式或 `settings.json` 规则时才需要同步维护 |

根目录文件：`PAM.sln` · [`publish.ps1`](publish.ps1)（唯一发布入口） · `README.md`（给人看的项目简介，AI 不必读） · 本文件。

关系：

- **用户唯一入口是 `PAM.App.exe`**。它在 `OnStartup` 里调用 `TrayBridge.EnsureTrayRunning()` 拉起同目录的 `PAM.Tray.exe`，之后两者不再直接通信。
- **PAM.Tray 只读 `settings.json`，从不写配置**：单实例 `Mutex` 抢不到时静默退出，不弹提示；它与主界面进程唯一的联系是右键菜单里的"显示主界面"。
- 两个可执行程序（`PAM.App` / `PAM.Tray`）都只引用 **PAM.Core**，且**互不引用**；`PAM.Tests` 也只依赖 Core。
- **PAM.App 写 `settings.json` → PAM.Tray 读**，通过两个命名事件（设置变更 / 请求退出）通知，托盘另用文件时间戳轮询兜底。
- App 关闭后托盘是否继续驻留，由 `KeepTrayOnClose` 决定（默认保留，热键不受影响）。
- **开机自启由 PAM.App 注册**：`AutoStartService` 把同目录的 `PAM.Tray.exe` 写进 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 的 `PAM.Tray` 值，并清除任务管理器留下的禁用标记；`OnStartup` 与 `AppServices.Apply` / `Reload` 按 `startTrayWithWindows` 幂等同步，程序目录移动后打开一次主界面即可修正路径。PAM.Tray 自身不写注册表，也不负责自己的启动。
- 托盘在 `switchImeToEnglish` 开启时，会把新打开的终端窗口的中文输入法切成英文输入模式（不换输入法与键盘布局），实现见 [PAM.Tray/AGENT-Tray.md](PAM.Tray/AGENT-Tray.md)。
- 别名与自定义命令的数据存于 `%AppData%\PAM\pam-data.json`（App 唯一写入方）；`$PROFILE` 以 `# >>> PAM Managed >>>` 分区，用户区原样保留，托管区由 `ProfileRenderer` 按数据单向全量生成。别名启动借 `explorer.exe` 代启动，开关写在 `settings.json`、由公共函数运行时读取，实现见 [PAM.Core/AGENT-Core.md](PAM.Core/AGENT-Core.md)。
- 美化页的外观卡片与自定义提示符分别接管 Windows Terminal `settings.json` 的外观属性与 `starship.toml` 的托管块，界面行为见 [PAM.App/AGENT-App.md](PAM.App/AGENT-App.md)、写入规则见 [PAM.Core/AGENT-Core.md](PAM.Core/AGENT-Core.md)。

## 环境与运行

需要 Windows 10/11、.NET 10 Desktop Runtime、系统自带 Windows PowerShell 5.1、Windows Terminal (`wt.exe`)。PowerShell 7 可选，未安装时自动回退 5.1 并在设置页提示。

```powershell
dotnet run --project PAM.App                             # 开发运行：主界面 + 后台托盘
dotnet test                                              # PAM.Core 单元测试
powershell -ExecutionPolicy Bypass -File .\publish.ps1   # 打包：产出 publish/
```

`publish.ps1` 是唯一的发布入口：先结束正在运行的后台实例并清理构建中间目录，再把两个工程分别发布到临时目录中的全新位置，最后合并成 `publish/` 里完整的一对可执行文件并校验文件齐全。`publish/` 是可直接运行的交付目录，其中的 `PAM.App.exe` 与 `PAM.Tray.exe` 必须同目录，登录启动项也指向这里的 `PAM.Tray.exe`。日常开发不需要手动逐条 `dotnet publish`。

单元测试只测 PAM.Core 的纯逻辑，行为清单与维护规则见 [PAM.Tests/AGENT-测试.md](PAM.Tests/AGENT-测试.md)。

## 数据在哪

| 数据                 | 位置                                                                                                                         | 详细说明                                                                                 |
| ------------------ | -------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| 别名 / 自定义命令         | `%AppData%\PAM\pam-data.json`（唯一数据源）                                                                                       | [PAM.Core/AGENT-Core.md](PAM.Core/AGENT-Core.md)                                     |
| `$PROFILE` 托管区     | 默认终端对应的 `$PROFILE`（PowerShell 7 与 5.1 是两个不同文件，按同一份数据分别生成）                                                                  | [PAM.Core/AGENT-Core.md](PAM.Core/AGENT-Core.md)                                     |
| 主题、语言、默认终端、热键、托盘行为 | `%AppData%\PAM\settings.json`                                                                                              | [PAM.App/AGENT-App.md](PAM.App/AGENT-App.md)                                         |
| 登录启动项              | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 的 `PAM.Tray` 值（REG_SZ，带引号的绝对路径）                                       | 由 PAM.App 按 `startTrayWithWindows` 同步，见 [PAM.App/AGENT-App.md](PAM.App/AGENT-App.md) |
| Starship 主题（美化页写入） | `~\.config\starship.toml`（应用主题前单槽备份为 `.bak`）；init 行写入 `$PROFILE` 受控区块                                                      | [PAM.Core/AGENT-Core.md](PAM.Core/AGENT-Core.md)                                     |
| 终端外观（美化页写入）        | Windows Terminal `settings.json` 的 `profiles.defaults`：字体、复古效果、光标、背景图、透明度、亚克力；首次修改前的原始配置另存为 `settings.json.bak`，永不覆盖、恢复后保留 | [PAM.Core/AGENT-Core.md](PAM.Core/AGENT-Core.md)                                     |

详细文档按项目查阅：[Core](PAM.Core/AGENT-Core.md)（`$PROFILE` 格式与全部服务）· [App](PAM.App/AGENT-App.md)（四个页签、外观卡片、设置页与配置字段）· [Tray](PAM.Tray/AGENT-Tray.md)（热键注册与热重载）。
