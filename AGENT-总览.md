# PowerShell 管理器（AI 必读索引）

基于 **WPF + WinForms + .NET 10** 的 Windows 终端配置工具：用图形界面维护别名与自定义命令并把它们生成到 PowerShell `$PROFILE` 的托管区，由一个后台托盘程序提供全局热键唤醒终端。

> 本文件是给 AI 的必读索引：只记录项目结构、进程关系、环境与运行方式、数据位置。各模块的实现细节在对应项目的 AGENT 文档里，按下表选读，本文件不重复。

## 项目

| 项目 | 职责 | 什么时候读 |
| --- | --- | --- |
| [PAM.Core](PAM.Core/AGENT-Core.md) | 无 UI 的核心库：托管数据模型与 `pam-data.json` 读写、`$PROFILE` 分区渲染、设置模型、热键描述、终端探测、本地化词表、跨进程信号、Starship / 字体 / Windows Terminal 外观服务 | 改数据格式、配置字段、`$PROFILE` 分区规则、终端外观或美化功能时 |
| [PAM.App](PAM.App/AGENT-App.md) | WPF 主界面与设置页，用户唯一直接操作的程序，也是 `settings.json` 的唯一写入方 | **最常读**：改界面、页签、主题 / 语言 / 热键交互、终端外观、Starship 美化时 |
| [PAM.Tray](PAM.Tray/AGENT-Tray.md) | 纯粹的后台组件：常驻托盘、注册全局热键、按下后拉起 Windows Terminal；只读配置，从不写入，也不需要用户手动启动 | 只有改热键注册或托盘行为时才需要看，日常开发基本不用碰 |
| [PAM.Tests](PAM.Tests/AGENT-测试.md) | xUnit 测试，只覆盖 PAM.Core 的纯逻辑，不涉及 WPF 界面 | 平时不用读；只有改动 `$PROFILE` 区块格式或 `settings.json` 规则时才需要同步维护 |

根目录文件：`PAM.sln` · [`Directory.Build.targets`](Directory.Build.targets)（Debug 构建前结束运行中的实例） · `README.md`（给人看的项目简介，AI 不必读） · 本文件。

关系：

- **用户唯一入口是 `PAM.App.exe`**。它在 `OnStartup` 里调用 `TrayBridge.EnsureTrayRunning()` 拉起同目录的 `PAM.Tray.exe`，之后两者不再直接通信。
- **PAM.Tray 只读 `settings.json`，从不写配置**：单实例 `Mutex` 抢不到时静默退出，不弹提示；它与主界面进程唯一的联系是右键菜单里的"显示主界面"。
- 两个可执行程序（`PAM.App` / `PAM.Tray`）都只引用 **PAM.Core**，且**互不引用**；`PAM.Tests` 也只依赖 Core。
- **PAM.App 写 `settings.json` → PAM.Tray 读**，通过两个命名事件（设置变更 / 请求退出）通知，托盘另用文件时间戳轮询兜底。
- App 关闭后托盘是否继续驻留，由 `KeepTrayOnClose` 决定（默认保留，热键不受影响）。
- 托盘在 `switchImeToEnglish` 开启时，会把新打开的终端窗口的中文输入法切成英文输入模式（不换输入法与键盘布局），实现见 [PAM.Tray/AGENT-Tray.md](PAM.Tray/AGENT-Tray.md)。
- 别名与自定义命令的数据存于 `%AppData%\PAM\pam-data.json`（App 唯一写入方）；`$PROFILE` 以 `# >>> PAM Managed >>>` 分区，用户区原样保留，托管区由 `ProfileRenderer` 按数据单向全量生成。别名启动借 `explorer.exe` 代启动，开关写在 `settings.json`、由公共函数运行时读取，实现见 [PAM.Core/AGENT-Core.md](PAM.Core/AGENT-Core.md)。
- 美化页的外观卡片与自定义提示符分别接管 Windows Terminal `settings.json` 的外观属性与 `starship.toml` 的托管块，界面行为见 [PAM.App/AGENT-App.md](PAM.App/AGENT-App.md)、写入规则见 [PAM.Core/AGENT-Core.md](PAM.Core/AGENT-Core.md)。

## 环境与运行

需要 Windows 10/11、.NET 10 Desktop Runtime、系统自带 Windows PowerShell 5.1、Windows Terminal (`wt.exe`)。PowerShell 7 可选，未安装时自动回退 5.1 并在设置页提示。

```powershell
dotnet run --project PAM.App        # 唯一用户入口：主界面 + 自动拉起托盘
dotnet test                         # 一键运行 PAM.Core 的全部单元测试

# 发布：逐条 publish 到同一个目录，App 才能在旁边找到 Tray
dotnet publish PAM.App  -c Release -o publish
dotnet publish PAM.Tray -c Release -o publish
```

单元测试只测 PAM.Core 的纯逻辑，行为清单与维护规则见 [PAM.Tests/AGENT-测试.md](PAM.Tests/AGENT-测试.md)。

> 不要写成 `dotnet publish PAM.sln -o publish`：解决方案级 `--output` 不受支持，还会把 PAM.Tests 和 xunit、`testhost.exe` 一起倒进交付目录。
>
> Debug 构建时两个工程会把产物互相推给对方的 `bin`（见两个 csproj 里的 `Push*OutputTo*` 目标），所以任意一个 bin 目录都是完整可跑的一对：`EnsureTrayRunning()` 和托盘"显示主界面"在开发期就能正常工作。注意只构建其中一个工程不会刷新对方的产物，改了两边代码时构建整个 sln。

> 本机已知问题：改动 PAM.App / PAM.Core 后直接构建 PAM.Tray 偶发 MSBuild `Copy` 栈溢出（环境问题，非代码，发生在覆盖已存在输出文件时的删除阶段）。最有效的办法是先清空各项目的 `bin\<配置>\net10.0-windows` 输出目录再构建；`dotnet clean` 或 `-m:1` 单独使用仍可能复现。不想动现有产物时可用全新输出目录并跳过互推目标：`dotnet build PAM.sln -c Release -p:BaseOutputPath=<新目录> -p:PamSkipOutputPush=true -m:1`（该开关让两个 `Push*OutputTo*` 目标跳过，仅用于验证构建，产物不在常规 bin 目录）。
>
> 托盘 / 主程序运行时锁住 `bin` 里的 dll，会让构建失败（`MSB3021` / `MSB3027`）。根目录 `Directory.Build.targets` 在 Debug 构建前自动结束这两个进程，构建完需要再用 `rr` 或托盘热键重新打开。发布（`-c Release`）与设计时构建不受影响；想保留运行时加 `-p:PamKeepRunning=true`。

## 数据在哪

| 数据 | 位置 | 详细说明 |
| --- | --- | --- |
| 别名 / 自定义命令 | `%AppData%\PAM\pam-data.json`（唯一数据源） | [PAM.Core/AGENT-Core.md](PAM.Core/AGENT-Core.md) |
| `$PROFILE` 托管区 | 默认终端对应的 `$PROFILE`（PowerShell 7 与 5.1 是两个不同文件，按同一份数据分别生成） | [PAM.Core/AGENT-Core.md](PAM.Core/AGENT-Core.md) |
| 主题、语言、默认终端、热键、托盘行为 | `%AppData%\PAM\settings.json` | [PAM.App/AGENT-App.md](PAM.App/AGENT-App.md) |
| Starship 主题（美化页写入） | `~\.config\starship.toml`（应用主题前单槽备份为 `.bak`）；init 行写入 `$PROFILE` 受控区块 | [PAM.Core/AGENT-Core.md](PAM.Core/AGENT-Core.md) |
| 终端外观（美化页写入） | Windows Terminal `settings.json` 的 `profiles.defaults`：字体、复古效果、光标、背景图、透明度、亚克力；首次修改前的原始配置另存为 `settings.json.bak`，永不覆盖、恢复后保留 | [PAM.Core/AGENT-Core.md](PAM.Core/AGENT-Core.md) |

详细文档按项目查阅：[Core](PAM.Core/AGENT-Core.md)（`$PROFILE` 格式与全部服务）· [App](PAM.App/AGENT-App.md)（四个页签、外观卡片、设置页与配置字段）· [Tray](PAM.Tray/AGENT-Tray.md)（热键注册与热重载）。
