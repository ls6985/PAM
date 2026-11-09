# PowerShell 管理器 (PAM)

基于 **WPF + WinForms + .NET 10** 的 Windows 终端配置工具：用图形界面维护别名与自定义命令，把它们生成到 PowerShell `$PROFILE` 的托管区；后台托盘程序注册全局热键，一键唤醒终端。

## 主要功能

- **别名启动**：把常用程序路径注册成终端里可直接输入的快捷命令，可视化新建 / 编辑 / 删除，支持备注、搜索与一键打开 `$PROFILE`；数据存在 `pam-data.json`，`$PROFILE` 只负责生成同名函数。
- **自定义命令**：维护可单独启用 / 禁用的 PowerShell 函数片段，可随意编辑 `$PROFILE` 里属于自己的上半部分；另附三条随保存自动维护的内置命令 `pls` / `ongit` / `sudo`。
- **美化**：一键安装 Starship 并应用官方主题，或点选片段组合出自定义提示符；统一管理 Windows Terminal 的字体、复古效果、光标、背景图、窗口透明度等外观；内置 Cascadia Code NF 字体一键安装。
- **全局热键**：后台托盘随 Windows 登录自动启动并常驻，任意界面按下热键即拉起 Windows Terminal；可在「设置 → 后台运行」里关闭开机自启。
- **终端体验**：新开终端时自动把中文输入法切到英文模式；别名借 explorer 以独立进程启动，关闭终端不会带走正在运行的软件。

## 环境要求

- Windows 10 / 11
- .NET 10 Desktop Runtime
- 系统自带 Windows PowerShell 5.1（PowerShell 7 可选，未安装时自动回退）
- Windows Terminal

## 构建与运行

```powershell
dotnet run --project PAM.App                             # 开发运行：主界面（自动拉起后台托盘）
dotnet test                                              # 单元测试

# 打包：一键产出可直接运行的 publish/ 目录
powershell -ExecutionPolicy Bypass -File .\publish.ps1
```

`publish.ps1` 是唯一的发布入口：先结束后台实例、清理构建中间目录，把主界面与托盘分别发布到临时的全新位置，再合并到 `publish/` 并校验文件齐全。`publish/` 里的 `PAM.App.exe` 与 `PAM.Tray.exe` 必须放在一起，开机自启注册的也是这个目录里的托盘。日常开发不需要手动逐条 `dotnet publish`。

打包会先结束后台托盘；想立刻恢复热键可加 `-RestartTray` 参数，或直接运行 `publish\PAM.Tray.exe`，否则下次登录时会自动启动。

## 目录结构

| 目录 | 内容 |
| --- | --- |
| `PAM.App` | WPF 主界面（用户唯一入口），也是 `settings.json` 与 `pam-data.json` 的唯一写入方 |
| `PAM.Core` | 无 UI 核心库：数据格式、配置读写、`$PROFILE` 托管区渲染与各服务 |
| `PAM.Tray` | 后台托盘：全局热键、拉起终端，只读配置 |
| `PAM.Tests` | xUnit 单元测试（只测 Core） |

## 内置命令

保存一次自定义命令（或保存别名）后，新开终端窗口即可使用：

| 输入 | 作用 |
| --- | --- |
| `pls` | 列出全部别名及其路径、备注 |
| `ongit 首次提交` | 暂存当前仓库全部改动（`git add -A`）并提交（`git commit -m 首次提交`）；多个词会合成一条消息 |
| `sudo notepad` | 弹 UAC 以管理员身份启动程序，也支持 PAM 别名，如 `sudo blbl`；`sudo -w setup.exe` 会等待进程结束 |

说明：

- 这三条随 `$PROFILE` 的托管区自动维护，界面里只读展示，无需手动添加；`$PROFILE` 只在终端启动时加载，改动后要**新开终端窗口**。
- `sudo` 在新窗口启动提权进程：图形程序正常，纯命令行程序的输出不会回到当前终端；需要交互请直接打开管理员终端。
- Windows 11 24H2+ 若在设置里启用了系统自带的 `sudo`，本函数会覆盖它；仍可显式输入 `sudo.exe ...` 调用系统版。当前若输入 `sudo` 提示"不识别"，说明系统功能未启用，覆盖无影响。

## 说明

- 主题、语言、默认终端、热键、托盘行为等配置存于 `%AppData%\PAM\settings.json`；别名与自定义命令存于 `%AppData%\PAM\pam-data.json`。
- 默认登录 Windows 后自动启动后台托盘：写入当前用户的开机启动项（免管理员权限），不依赖主界面先打开；在「设置 → 后台运行」可关闭。程序目录移动后打开一次主界面即可更新启动项路径，关闭主界面时是否保留托盘由另一项设置单独控制。
- `$PROFILE` 以 `# >>> PAM Managed >>>` 为界：上半部分是用户原有配置，软件逐行原样保留、可自由编辑；下半部分是由数据文件全量生成的托管区，手工修改会在下次启动或保存时被覆盖。
- 用户区若已定义同名的 alias 或函数，对应的快捷命令会让位，界面保存时会列出这些冲突名称。
- 切换默认终端（PowerShell 7 / 5.1）时数据仍是同一份，软件会按当前终端的 `$PROFILE` 重建托管区；`$PROFILE` 只在终端启动时加载，改动后要**新开终端窗口**。
- 仓库里的 `AGENT-*.md` 是写给 AI 编程助手的开发文档，日常使用无需阅读。
