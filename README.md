# PowerShell 管理器 (PAM)

基于 **WPF + WinForms + .NET 10** 的 Windows 终端配置工具：用图形界面维护 PowerShell `$PROFILE` 中的别名与自定义命令，后台托盘程序注册全局热键，一键唤醒终端。

## 主要功能

- **别名启动**：把常用程序路径注册成 PowerShell 别名，可视化新建 / 编辑 / 删除，支持备注、搜索与一键打开 `$PROFILE`。
- **自定义命令**：维护 `$PROFILE` 受控区块里的 PowerShell 函数片段，可单独启用 / 禁用，不碰区块外的手工配置；另附三条随保存自动维护的内置命令 `pls` / `ongit` / `sudo`。
- **美化**：一键安装 Starship 并应用官方主题，或点选片段组合出自定义提示符；统一管理 Windows Terminal 的字体、复古效果、光标、背景图、窗口透明度等外观；内置 Cascadia Code NF 字体一键安装。
- **全局热键**：后台托盘常驻，任意界面按下热键即拉起 Windows Terminal。
- **终端体验**：新开终端时自动把中文输入法切到英文模式；别名借 explorer 以独立进程启动，关闭终端不会带走正在运行的软件。

## 环境要求

- Windows 10 / 11
- .NET 10 Desktop Runtime
- 系统自带 Windows PowerShell 5.1（PowerShell 7 可选，未安装时自动回退）
- Windows Terminal

## 构建与运行

```powershell
dotnet run --project PAM.App        # 主界面（自动拉起后台托盘）
dotnet test                         # 单元测试

# 发布：逐条 publish 到同一个目录，App 才能在旁边找到 Tray
dotnet publish PAM.App  -c Release -o publish
dotnet publish PAM.Tray -c Release -o publish
```

## 目录结构

| 目录 | 内容 |
| --- | --- |
| `PAM.App` | WPF 主界面（用户唯一入口），也是 `settings.json` 的唯一写入方 |
| `PAM.Core` | 无 UI 核心库：数据格式、配置读写与各服务 |
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

- 这三条随 `$PROFILE` 的受控区块自动维护，界面里只读展示，无需手动添加；`$PROFILE` 只在终端启动时加载，改动后要**新开终端窗口**。
- `sudo` 在新窗口启动提权进程：图形程序正常，纯命令行程序的输出不会回到当前终端；需要交互请直接打开管理员终端。
- Windows 11 24H2+ 若在设置里启用了系统自带的 `sudo`，本函数会覆盖它；仍可显式输入 `sudo.exe ...` 调用系统版。当前若输入 `sudo` 提示"不识别"，说明系统功能未启用，覆盖无影响。

## 说明

- 主题、语言、默认终端、热键等配置存于 `%AppData%\PAM\settings.json`。
- 别名与自定义命令只写入 `$PROFILE` 中本软件自己的标记区块，其余内容原样保留。
- 仓库里的 `AGENT-*.md` 是写给 AI 编程助手的开发文档，日常使用无需阅读。
