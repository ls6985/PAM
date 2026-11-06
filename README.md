# PowerShell 管理器

一个基于 **WPF + WinForms + .NET 10** 的 Windows 终端管理工具集。

包含一个图形界面用于管理 PowerShell `$PROFILE` 中的别名，以及一个后台托盘程序用于全局热键唤醒终端。

## 项目结构

- [PAM.App](PAM.App/README.md)：WPF 主界面（别名管理与配置写入）。
- [PAM.Tray](PAM.Tray/README.md)：WinForms 后台常驻（全局热键与终端唤醒）。
- [PAM.Core](PAM.Core/README.md)：无 UI 依赖的核心逻辑库（模型与服务）。

## 环境要求

- Windows 10/11
- .NET 10 SDK / Desktop Runtime
- 系统自带 Windows PowerShell 5.1
- Windows Terminal (`wt.exe`)
- （可选）PowerShell 7：安装后托盘热键会优先用 `pwsh.exe` 启动终端，未安装则回退 `powershell.exe`

## 运行与发布

在根目录执行：

```powershell
# 运行 UI 界面
dotnet run --project PAM.App

# 运行后台托盘
dotnet run --project PAM.Tray

# 发布
dotnet publish PAM.App -c Release -r win-x64 --self-contained false
dotnet publish PAM.Tray -c Release -r win-x64 --self-contained false
写入格式
别名在 $PROFILE 中的存储格式如下：

powershell
# @alias-meta blbl 打开哔哩哔哩
Set-Alias -Name blbl -Value "C:\Program Files\App\app.exe"
备注存在上一行。

读取时兼容单双引号，保存时统一双引号并转义。

只识别和清理上述格式，其他配置原样保留。