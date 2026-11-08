# PowerShell 管理器

基于 **WPF + WinForms + .NET 10** 的 Windows 终端配置工具：用图形界面维护 PowerShell `$PROFILE` 中的别名与自定义命令，并由一个后台托盘程序提供全局热键唤醒终端。

## 项目

| 项目 | 职责 | 什么时候读 |
| --- | --- | --- |
| [PAM.Core](PAM.Core/README.md) | 无 UI 的核心库：设置模型、`$PROFILE` 读写、热键描述、终端探测、本地化词表、跨进程信号、Starship 与字体服务 | 改数据格式、配置字段、`$PROFILE` 读写规则或美化功能时 |
| [PAM.App](PAM.App/README.md) | WPF 主界面与设置页，用户唯一直接操作的程序，也是 `settings.json` 的唯一写入方 | **最常读**：改界面、页签、主题 / 语言 / 热键交互、Starship 美化时 |
| [PAM.Tray](PAM.Tray/README.md) | 纯粹的后台组件：常驻托盘、注册全局热键、按下后拉起 Windows Terminal；只读配置，从不写入，也不需要用户手动启动 | 只有改热键注册或托盘行为时才需要看，日常开发基本不用碰 |
| PAM.Tests | xUnit 测试，只覆盖 PAM.Core 的纯逻辑，不涉及 WPF 界面 | 平时不用读；只有改动 `$PROFILE` 区块格式或 `settings.json` 规则时才需要同步维护 |

根目录文件：`PAM.sln` · [`Directory.Build.targets`](Directory.Build.targets)（Debug 构建前结束运行中的实例） · `README.md`。

关系：

- **用户唯一入口是 `PAM.App.exe`**。它在 `OnStartup` 里调用 `TrayBridge.EnsureTrayRunning()` 拉起同目录的 `PAM.Tray.exe`，之后两者不再直接通信。
- **PAM.Tray 只读 `settings.json`，从不写配置**：单实例 `Mutex` 抢不到时静默退出，不弹提示；它与主界面进程唯一的联系是右键菜单里的"显示主界面"。
- 两个可执行程序（`PAM.App` / `PAM.Tray`）都只引用 **PAM.Core**，且**互不引用**；`PAM.Tests` 也只依赖 Core。
- **PAM.App 写 `settings.json` → PAM.Tray 读**，通过两个命名事件（设置变更 / 请求退出）通知，托盘另用文件时间戳轮询兜底。
- 托盘的 700ms 定时器除消费信号外，在 `switchImeToEnglish` 开启时还会检测新打开的终端窗口，用 UI Automation 判断任务栏输入指示器的中/英状态，仅在中文模式时模拟一次 Shift 把当前中文输入法切到英文模式（不更换输入法 / 键盘布局，见 [PAM.Tray/README.md](PAM.Tray/README.md)）。
- App 关闭后托盘是否继续驻留，由 `KeepTrayOnClose` 决定（默认保留，热键不受影响）。
- 别名启动行为由 `$PROFILE` 里 `AliasLaunchService` 维护的自有标记块承担：公共函数 `Invoke-PamAlias` 借 `explorer.exe` 代启动（带参数时经临时 `.lnk`），新进程父进程是 explorer、**完全脱离终端进程树与控制台**，因此关终端不会带走软件、程序日志也刷不回终端，不需要重定向 NUL，UAC 提升正常。开关写在 `settings.json`，公共函数运行时读取，改设置无需重写 `$PROFILE`；`$PROFILE` 只在终端启动时加载一次，改完要新开终端窗口。
- 自定义提示符由 `StarshipPromptService` 只接管 `~/.config/starship.toml` 的**顶层 `format`**（自有标记块内一行 Starship format）。美化页用「点选片段组合」：每个片段在下拉里选变量或固定符号、单独选颜色，整体由选择拼成 Starship format，变量不再手填、不可直接编辑；精选 20 个中文变量 + 固定符号（@ : ~ $ 空格）+ 12 色色板（颜色下拉与变量下拉同款 WPF-UI ComboBox，每项显示 `#RRGGBB` 色号，选「自定义…」后可在右侧直接填 `#` 后面的 6 位色号）。用户名 / 主机名 / 当前路径等是 Starship 运行时读取的值，不做可编辑文字；配置本身就是 toml，无需额外持久化。其余段落 / 注释原样保留，首次应用用独立的 `starship.toml.pam-prompt.bak` 保存原始文件（见 [PAM.Core/README.md](PAM.Core/README.md)）。

## 环境与运行

需要 Windows 10/11、.NET 10 Desktop Runtime、系统自带 Windows PowerShell 5.1、Windows Terminal (`wt.exe`)。PowerShell 7 可选，未安装时自动回退 5.1 并在设置页提示。

`PAM.sln` 只是工程清单，不是可执行程序；VS 的 F5 启动资源管理器里加粗的那个工程，请确认它是 `PAM.App`（右键 → 设为启动项目）。

```powershell
dotnet run --project PAM.App        # 唯一用户入口：主界面 + 自动拉起托盘
dotnet test                         # 一键运行 PAM.Core 的全部单元测试

# 发布：逐条 publish 到同一个目录，App 才能在旁边找到 Tray
dotnet publish PAM.App  -c Release -o publish
dotnet publish PAM.Tray -c Release -o publish
```

> 不要写成 `dotnet publish PAM.sln -o publish`：解决方案级 `--output` 不受支持，还会把 PAM.Tests 和 xunit、`testhost.exe` 一起倒进交付目录。
>
> Debug 构建时两个工程会把产物互相推给对方的 `bin`（见两个 csproj 里的 `Push*OutputTo*` 目标），所以任意一个 bin 目录都是完整可跑的一对：`EnsureTrayRunning()` 和托盘"显示主界面"在开发期就能正常工作。注意只构建其中一个工程不会刷新对方的产物，改了两边代码时构建整个 sln。

> 本机已知问题：改动 PAM.App / PAM.Core 后直接构建 PAM.Tray 偶发 MSBuild `Copy` 栈溢出（环境问题，非代码，发生在覆盖已存在输出文件时的删除阶段）。最有效的办法是先清空各项目的 `bin\<配置>\net10.0-windows` 输出目录再构建；`dotnet clean` 或 `-m:1` 单独使用仍可能复现。不想动现有产物时可用全新输出目录并跳过互推目标：`dotnet build PAM.sln -c Release -p:BaseOutputPath=<新目录> -p:PamSkipOutputPush=true -m:1`（该开关让两个 `Push*OutputTo*` 目标跳过，仅用于验证构建，产物不在常规 bin 目录）。
>
> 托盘 / 主程序运行时锁住 `bin` 里的 dll，会让构建失败（`MSB3021` / `MSB3027`）。根目录 `Directory.Build.targets` 在 Debug 构建前自动结束这两个进程，构建完需要再用 `rr` 或托盘热键重新打开。发布（`-c Release`）与设计时构建不受影响；想保留运行时加 `-p:PamKeepRunning=true`。

## 测试

`dotnet test` 在项目根目录一键运行，全部通过才算改完。只测 PAM.Core 的纯逻辑，不碰 WPF 界面，也**不会读写真实的 `$PROFILE` 和 `%AppData%\PAM\settings.json`**（全部在临时目录里跑）。

| 测试文件 | 锁住的行为 |
| --- | --- |
| `ProfileServiceTests.cs` | 受控区块只追加 / 整体替换 / 清空移除；区块与别名互不破坏；多行代码含缩进空行的启用禁用还原；单引号、双引号、无引号路径解析与写回转义（`` ` ``、`$`、`"`）；UTF-8 BOM、无 BOM 升级、ANSI 保留；CRLF 与 LF 各自的换行风格保留；配置文件缺失时自动建父目录；空文件、超长路径、特殊字符、非法路径参数 |
| `SettingsServiceTests.cs` | `settings.json` 的 camelCase 键名与缩进格式、全部属性读写映射、文件缺失或损坏（乱码、截断、非法 JSON）时回退默认值且不抛异常、手工改坏的枚举值被 `Normalize()` 修正 |

维护规则：**功能代码没改，就不用动测试代码，也不用读它**——改完 `PAM.Core` 的业务逻辑跑一遍 `dotnet test` 确认没被改坏即可。只有当 `$PROFILE` 区块格式、编码保留规则或 `settings.json` 字段本身发生变化时，才需要同步调整对应断言。测试不追求覆盖率，只覆盖上表列出的核心行为与边界场景。

## 数据在哪

| 数据 | 位置 | 详细说明 |
| --- | --- | --- |
| 别名 / 自定义命令 | 默认终端对应的 `$PROFILE`（PowerShell 7 与 5.1 是两个不同文件） | [PAM.Core/README.md](PAM.Core/README.md) |
| 主题、语言、默认终端、热键、托盘行为 | `%AppData%\PAM\settings.json` | [PAM.App/README.md](PAM.App/README.md) |
| Starship 主题（美化页写入） | `~\.config\starship.toml`（写前自动备份为 `.bak`）；init 行写入 `$PROFILE` 受控区块 | [PAM.Core/README.md](PAM.Core/README.md) |
| 终端字体（美化页写入） | Windows Terminal `settings.json` 的 `profiles.defaults.font.face`（写前自动备份） | [PAM.Core/README.md](PAM.Core/README.md) |

详细文档按项目查阅：[Core](PAM.Core/README.md)（`$PROFILE` 格式与全部服务）· [App](PAM.App/README.md)（三个页签、设置页与配置字段）· [Tray](PAM.Tray/README.md)（热键注册与热重载）。
