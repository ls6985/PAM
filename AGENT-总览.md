# PowerShell 管理器 · AGENT 文档总览

基于 **WPF + WinForms + .NET 10** 的 Windows 终端配置工具：图形界面维护别名与自定义命令并生成到 PowerShell `$PROFILE` 的托管区，由一个后台托盘程序提供全局热键唤醒终端。

> 本套 `AGENT-*.md` 面向 AI，按项目分工。本文件只列项目概览、**必读约定**与**功能索引**；实现细节在各子文档，此处不重复。

## 项目与文档

| 项目 | 职责 | 什么时候读 |
| --- | --- | --- |
| [PAM.Core](PAM.Core/AGENT-Core.md) | 无 UI 的核心库：托管数据模型与 `pam-data.json` 读写、`$PROFILE` 分区渲染、设置模型、热键描述、终端探测、本地化词表、跨进程信号、Starship / 字体 / Windows Terminal 外观服务 | 改数据格式、配置字段、托管区规则或美化功能时 |
| [PAM.App](PAM.App/AGENT-App.md) | WPF 主界面与设置页，用户唯一直接操作的程序，也是 `settings.json` 与 `pam-data.json` 的唯一写入方 | **最常读**：改界面、页签、主题 / 语言 / 热键交互、终端外观、Starship 美化时 |
| [PAM.Tray](PAM.Tray/AGENT-Tray.md) | 后台托盘：注册全局热键、唤醒 Windows Terminal、切换输入法；只读配置，不写注册表，也不需要手动启动 | 改热键注册或托盘行为时 |
| [PAM.Tests](PAM.Tests/AGENT-测试.md) | xUnit 测试，只覆盖 PAM.Core 的纯逻辑，不涉及 WPF 界面 | 平时不用读；改托管区格式或配置字段后同步断言 |

根目录文件：`PAM.sln` · `publish.ps1`（唯一发布入口）· 本文件。

## 必读约定

1. **进程与入口**：用户唯一入口是 `PAM.App.exe`；`OnStartup` 经 `TrayBridge.EnsureTrayRunning()` 拉起同目录的 `PAM.Tray.exe`，之后两者不直接通信。App / Tray 都只引用 PAM.Core 且互不引用；Tests 只依赖 Core。
2. **配置归属**：`settings.json` / `pam-data.json` 由 App 唯一写入，Tray 只读；跨进程靠两个命名事件（设置变更、请求退出）通知，托盘另有文件时间戳轮询兜底。App 关闭后托盘是否驻留由 `KeepTrayOnClose`（默认开）决定。窗口行为类开关不写 `settings.json`，直接读写 Windows Terminal 的 `settings.json`。
3. **开机自启**：只由 App 的 `AutoStartService` 按 `startTrayWithWindows` 幂等写 HKCU `Run` 的 `PAM.Tray` 值；Tray 不写注册表，也不负责自己的启动。程序目录移动后打开一次主界面即校准路径。
4. **双 `$PROFILE`**：别名、自定义命令、未知命令提示等托管块按同一份数据写入 **PowerShell 7 与 5.1 两份** `$PROFILE`，内容一致、共用一份 `managedHash`。`terminalType` 只决定 PAM 启动哪个终端，不决定写入哪个文件。托管区以 **UTF-8 BOM** 写入，否则 Windows PowerShell 5.1 会整块解析失败。
5. **重建判定**：启动自检与保存都用 `ProfileRenderer.NeedsRebuild()`——除 `managedHash` 不符外，还检查任一受管 `$PROFILE` 是否齐全当前版本的标记块；只看哈希会让代码新增的托管块永远写不进去。
6. **生效时机**：`$PROFILE` 只在终端启动时加载一次，保存后要新开终端窗口；`settings.json` 里的运行时开关由托管区公共逻辑读取，本身不触发重写。
7. **交付**：`publish.ps1` 是唯一发布入口，产出 `publish/` 里同目录的一对 exe，登录启动项也指向这里；日常开发无需逐条执行 `dotnet publish`。

热键唤醒统一使用 Windows Terminal，托盘长期持有一个窗口并复用；开关与窗口找回的细节见 [Tray](PAM.Tray/AGENT-Tray.md)。

## 功能索引

| 功能 | 读哪份文档 | 主要落点 |
| --- | --- | --- |
| 别名启动、固定启动参数 | [Core](PAM.Core/AGENT-Core.md) | `AliasLaunchService` · `ProfileRenderer` |
| 自定义命令、内置命令（`pls` / `ongit` / `sudo`） | [Core](PAM.Core/AGENT-Core.md) + [App](PAM.App/AGENT-App.md) | `CustomCommandService` · `CustomCommandCode` |
| 未知命令友好提示 | [Core](PAM.Core/AGENT-Core.md) | `CommandHintService` · `friendlyCommandNotFound` |
| `$PROFILE` 托管区、重建判定、双版本写入 | [Core](PAM.Core/AGENT-Core.md) | `ProfileRenderer` · `ProfileFileStore` |
| `pam-data.json` / `settings.json` 字段、热键模型、本地化词表 | [Core](PAM.Core/AGENT-Core.md) | `PamData` · `AppSettings` · `SettingsService` · `LocalizationService` |
| Starship 主题、预设、初始化缓存 | [Core](PAM.Core/AGENT-Core.md) + [App](PAM.App/AGENT-App.md) | `StarshipService`；美化页 |
| 自定义提示符 | [Core](PAM.Core/AGENT-Core.md) + [App](PAM.App/AGENT-App.md) | `StarshipPromptService`；美化页 |
| 终端外观、字体、备份恢复 | [Core](PAM.Core/AGENT-Core.md) + [App](PAM.App/AGENT-App.md) | `WindowsTerminalService` · `FontInstaller`；美化页 |
| 失焦隐藏、单窗口标签栏 | [Core](PAM.Core/AGENT-Core.md) + [App](PAM.App/AGENT-App.md) | WT 根级属性 `autoHideWindow` / `minimizeToNotificationArea` / `alwaysShowTabs` / `showTabsInTitlebar`；设置页与美化页通用设置 |
| 别名启动后立即隐藏终端 | [Core](PAM.Core/AGENT-Core.md) + [Tray](PAM.Tray/AGENT-Tray.md) | `hideTerminalAfterAliasLaunch` · 托管区进入即 `ShowWindow(SW_HIDE)`、不结束会话；托盘热键唤回 |
| 界面页签、设置页、主题画刷、快捷键 | [App](PAM.App/AGENT-App.md) | `MainWindow` · `SettingsView` · `ThemeService` · `AppServices` |
| 关于页、更新检测、GitHub 入口、许可证 | [App](PAM.App/AGENT-App.md) | `AboutView` · `UpdateCheckService` · `GitHubLinks` · `LicenseWindow` |
| 开机自启 | [App](PAM.App/AGENT-App.md) | `AutoStartService` · `startTrayWithWindows` |
| 全局热键、托盘行为、终端窗口复用、输入法切换 | [Tray](PAM.Tray/AGENT-Tray.md) | `TrayApplication` · `HotkeyManager` · `TerminalPool` · `ImeSwitcher` |
| 改动数据格式、托管区格式或设置字段之后 | [Tests](PAM.Tests/AGENT-测试.md) | 同步对应断言，`dotnet test` 全绿 |
| 构建与交付 | 本文件「改动后的输出」 | `publish.ps1` |

## 环境与运行

需要 Windows 10/11、.NET 10 Desktop Runtime、系统自带 Windows PowerShell 5.1、Windows Terminal (`wt.exe`)。PowerShell 7 可选，未安装时自动回退 5.1 并在设置页提示。

```powershell
dotnet run --project PAM.App                             # 开发运行：主界面 + 后台托盘
dotnet test                                              # PAM.Core 单元测试
powershell -ExecutionPolicy Bypass -File .\publish.ps1   # 打包：产出 publish/
```

`publish.ps1` 先结束正在运行的后台实例，再把两个工程发布到全新临时位置，最后合并成 `publish/` 里完整的一对可执行文件并校验文件齐全。`publish/PAM.App.exe` 与 `publish/PAM.Tray.exe` 必须同目录。

## 改动后的输出

改完源码执行一次 `publish.ps1`，让 `publish/` 里的两个 exe 与当前源码一致；单元测试用 `dotnet test`。提交前先输出一次。汇报写改动内容、测试结果、exe 是否已重新输出，不写构建命令的临时参数或环境排障过程。改动不自动提交或推送。

## 数据在哪

| 数据 | 位置 | 谁写 / 说明 |
| --- | --- | --- |
| 别名 / 自定义命令 | `%AppData%\PAM\pam-data.json` | App 唯一写入；[Core](PAM.Core/AGENT-Core.md) |
| `$PROFILE` 托管区 | PowerShell 7 与 5.1 两份，按同一份数据分别生成 | `ProfileRenderer`；[Core](PAM.Core/AGENT-Core.md) |
| 主题、语言、默认终端、热键、托盘行为 | `%AppData%\PAM\settings.json` | App 唯一写入；[App](PAM.App/AGENT-App.md) |
| 登录启动项 | HKCU `...\CurrentVersion\Run` 的 `PAM.Tray`（REG_SZ，带引号绝对路径） | `AutoStartService`；[App](PAM.App/AGENT-App.md) |
| Starship 主题 | `~\.config\starship.toml`（应用前单槽备份 `.bak`）；初始化区块常驻 `$PROFILE` | [Core](PAM.Core/AGENT-Core.md) |
| Starship 初始化缓存 | `%LOCALAPPDATA%\starship-init-full.ps1`（UTF-8 BOM） | 缓存缺失时由 `$PROFILE` 自动生成，升级 Starship 后删除即可刷新 |
| 终端外观 | Windows Terminal `settings.json` 的 `profiles.defaults`；首次修改前的原始配置另存 `settings.json.bak`，永不覆盖、恢复后保留 | [Core](PAM.Core/AGENT-Core.md) |
| 窗口行为 / 标签栏 | 同一份 Windows Terminal `settings.json` 的四个根级布尔属性；由界面开关即时或随「保存设置」写入，状态按文件实际值读回 | [Core](PAM.Core/AGENT-Core.md) |
