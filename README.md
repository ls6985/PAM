# PowerShell 管理器

基于 **WPF + WinForms + .NET 10** 的 Windows 终端配置工具：用图形界面维护 PowerShell `$PROFILE` 中的别名与自定义命令，并由一个后台托盘程序提供全局热键唤醒终端。

## 三个项目

| 项目 | 职责 | 什么时候读 |
| --- | --- | --- |
| [PAM.Core](PAM.Core/README.md) | 无 UI 的核心库：设置模型、`$PROFILE` 读写、热键描述、终端探测、本地化词表、跨进程信号 | 改数据格式、配置字段或 `$PROFILE` 读写规则时 |
| [PAM.App](PAM.App/README.md) | WPF 主界面与设置页，用户唯一直接操作的程序，也是 `settings.json` 的唯一写入方 | **最常读**：改界面、页签、主题 / 语言 / 热键交互时 |
| [PAM.Tray](PAM.Tray/README.md) | 纯粹的后台监听程序：常驻托盘、注册全局热键、按下后拉起 Windows Terminal；只读配置，从不写入 | 只有改热键注册或托盘行为时才需要看，日常开发基本不用碰 |

关系：

- 三个项目都引用 **PAM.Core**；PAM.Tray 另外引用 PAM.App 只是为了让发布目录同时带上主程序。
- **PAM.App 写 `settings.json` → PAM.Tray 读**，通过两个命名事件（设置变更 / 请求退出）通知，托盘另用文件时间戳轮询兜底。
- PAM.App 启动时会拉起同目录的 `PAM.Tray.exe`；单独调试主界面时若找不到托盘 exe 会静默跳过，界面与设置仍可用，只是全局热键不生效。
- 两者都不直接通信，唯一共享的数据是 `settings.json` 与 `$PROFILE`。

## 环境与运行

需要 Windows 10/11、.NET 10 Desktop Runtime、系统自带 Windows PowerShell 5.1、Windows Terminal (`wt.exe`)。PowerShell 7 可选，未安装时自动回退 5.1 并在设置页提示。

```powershell
dotnet run --project PAM.App        # 主界面（含设置页）
dotnet run --project PAM.Tray       # 仅后台托盘，通常不需要单独运行

dotnet publish PAM.App  -c Release -r win-x64 --self-contained false
dotnet publish PAM.Tray -c Release -r win-x64 --self-contained false
```

> 本机已知问题：改动 PAM.App / PAM.Core 后直接构建 PAM.Tray 偶发 MSBuild `Copy` 栈溢出（环境问题，非代码）；先 `dotnet clean` 再构建即可，或用 `-m:1` 单线程构建。

## 数据在哪

| 数据 | 位置 | 详细说明 |
| --- | --- | --- |
| 别名 / 自定义命令 | 默认终端对应的 `$PROFILE`（PowerShell 7 与 5.1 是两个不同文件） | [PAM.Core/README.md](PAM.Core/README.md) |
| 主题、语言、默认终端、热键、托盘行为 | `%AppData%\PAM\settings.json` | [PAM.App/README.md](PAM.App/README.md) |

详细文档按项目查阅：[Core](PAM.Core/README.md)（`$PROFILE` 格式与全部服务）· [App](PAM.App/README.md)（三个页签、设置页与配置字段）· [Tray](PAM.Tray/README.md)（热键注册与热重载）。
