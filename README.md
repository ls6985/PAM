# PowerShell 别名管理器

一个基于 **WPF + .NET 10** 的 Windows 桌面工具，用图形界面管理 Windows PowerShell `$PROFILE` 中的 `Set-Alias` 快捷启动项。

保存后会删除配置文件中可识别的旧 `Set-Alias` 行及其配套的 `# @alias-meta` 注释，再追加当前列表，并启动一个新的 PowerShell 窗口执行 `. $PROFILE`，让改动立即生效。

## 功能

- 读取真实 `$PROFILE`：通过 `powershell -NoProfile -Command "$PROFILE"` 定位当前用户的配置文件。
- 新建、编辑、删除别名：使用列表上方的内联编辑条，不弹出对话框。
- 删除确认：点击删除按钮后变为“确认删除？”，再次点击才会删除；按 `Esc` 可取消确认。
- 列表展示：别名使用 Consolas 等宽字体；长路径从中间省略，悬停可查看完整路径；文件不存在时显示警告图标。
- 搜索：按别名、路径或备注实时过滤。
- 双击列表行进入编辑；编辑期间别名输入框锁定，防止改错主键。
- 校验：别名必填、不能包含空格、不能与现有别名重复；路径必填但允许文件暂时不存在。
- 路径浏览：仅使用 `Microsoft.Win32.OpenFileDialog` 选择 `.exe`。
- 状态栏：显示 `已保存`、`有未保存更改`、`已写入`、`错误` 等状态。
- 配置文件缺失时提示，并在保存时自动创建（包括缺失的父目录）。
- 保留配置文件中其他所有内容，并尽量保持原有编码、BOM 与换行风格。
- 执行策略受限时（如 `Restricted` / `AllSigned`）显示警告，可一键执行 `Set-ExecutionPolicy RemoteSigned -Scope CurrentUser`。
- 使用 [WPF-UI](https://github.com/lepoco/wpfui) Fluent 控件，自动跟随系统深浅色主题。

## 环境要求

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)（开发/构建）
- .NET 10 Desktop Runtime（仅运行框架依赖发布版时需要）
- 系统自带 Windows PowerShell 5.1（`powershell.exe`）

项目目标框架固定为 `net10.0-windows`，并引用 WPF-UI `4.3.0`。

## 运行

在解决方案目录（包含 `PAM.sln`）执行：

```powershell
dotnet run --project PAM.App
```

## 发布

生成框架依赖的 Release 版本（非单文件）：

```powershell
dotnet publish PAM.App -c Release -r win-x64 --self-contained false
```

默认输出目录：

```text
PAM.App\bin\Release\net10.0-windows\win-x64\publish\
```

运行其中的 `PAM.App.exe` 即可。目标机器需要安装 .NET 10 Desktop Runtime。

如需自包含发布（体积更大，无需预装运行时）：

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

## 使用说明

1. 启动程序，等待状态栏显示配置文件路径和别名数量。
2. 点击“新建别名”，填写别名、应用程序路径和可选备注。
   - 别名示例：`blbl`
   - 路径示例：`C:\Program Files\App\app.exe`
   - 备注示例：`打开哔哩哔哩`
3. 按 `Enter` 或点击“确定”加入列表；按 `Esc` 或点击“取消”放弃输入。
4. 双击已有行可编辑路径和备注；点击行尾的 ✎ / 🗑 也可操作。
5. 点击“保存并生效”写入 `$PROFILE`，随后会打开一个新的 PowerShell 窗口并加载配置。

快捷键：

| 快捷键 | 作用 |
| --- | --- |
| `Ctrl+N` | 新建别名 |
| `Ctrl+S` | 保存并生效 |
| `Enter` | 编辑条内确认 |
| `Esc` | 取消编辑 / 取消删除确认 |

## 写入格式

每个别名会写成两行：

```powershell
# @alias-meta blbl 打开哔哩哔哩
Set-Alias -Name blbl -Value "C:\Program Files\App\app.exe"
```

- 备注存放在紧邻上一行的 `# @alias-meta <别名> <备注>` 注释中。
- 路径统一使用双引号包裹，并转义反引号、`$` 和 `"`。
- 读取时兼容单引号、双引号和无引号写法。
- 保存时只移除可识别的 `Set-Alias` 行及其配套元数据注释，配置中的函数、变量、`Import-Module` 等其他内容会原样保留。

> 提示：应用只会改动 `$PROFILE`（通常是 `Documents\WindowsPowerShell\Microsoft.PowerShell_profile.ps1`）。如果同时使用 PowerShell 7，它读取的是另一份 `$PROFILE`，本工具不会修改。

## 项目结构

```text
PAM.sln                     解决方案（PAM.Core / PAM.App / PAM.Tray）
PAM.Core                    类库：数据模型与服务层
  ProfileService.cs           $PROFILE 定位、解析、编码保持、安全写入、启动终端
  Models/AliasEntry.cs        别名数据模型与状态通知
PAM.App                     WPF 主程序（引用 PAM.Core）
  App.xaml / App.xaml.cs      主题资源、系统主题跟随、启动窗口
  MainWindow.xaml             界面布局（FluentWindow / 内联编辑条 / ListView）
  MainWindow.xaml.cs          界面逻辑、校验、加载/保存/启动流程
  Controls/MiddleEllipsisTextBlock.cs  路径中间省略控件
  Styles.xaml                 共用样式、行悬停/焦点触发器
PAM.Tray                    系统托盘入口（空壳项目，暂无逻辑）
```
