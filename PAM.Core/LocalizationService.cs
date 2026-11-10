using System.Text;
using System.Text.RegularExpressions;

namespace PAM.Core;

/// <summary>
/// 轻量本地化服务：以中文原文作为键，切换到英文时返回译文。
/// 这样做的好处是现有 XAML 里的中文字符串无需逐条改成资源键，
/// 由 PAM.App 的界面遍历器统一替换；托盘程序也能复用同一份词表。
/// </summary>
public sealed class LocalizationService
{
    public static LocalizationService Instance { get; } = new();

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        // ---- 通用 / 窗口 ----
        ["PowerShell 管理器"] = "PowerShell Manager",
        ["自定义命令"] = "Custom Commands",
        ["设置"] = "Settings",
        ["取消"] = "Cancel",
        ["确定"] = "OK",
        ["编辑"] = "Edit",
        ["删除"] = "Delete",
        ["退出"] = "Exit",
        ["状态"] = "Status",
        ["名称"] = "Name",
        ["路径"] = "Path",
        ["备注"] = "Note",
        ["操作"] = "Actions",
        ["别名"] = "Alias",
        ["浏览…"] = "Browse…",
        ["保存并生效"] = "Save & Apply",
        ["新建别名"] = "New Alias",
        ["新建命令"] = "New Command",
        ["确认删除？"] = "Confirm delete?",
        ["文件不存在"] = "File not found",
        ["删除（再次点击确认）"] = "Delete (click again to confirm)",
        ["切换启用状态"] = "Toggle enabled state",
        ["点击启用 / 禁用"] = "Click to enable / disable",
        ["已启用"] = "Enabled",
        ["已禁用"] = "Disabled",
        ["显示主界面"] = "Show Main Window",
        ["运行中"] = "Running",
        ["未运行"] = "Not running",

        // ---- 别名页 ----
        ["在 $PROFILE 中集中管理 Set-Alias 快捷启动项，保存后立即在新窗口生效。"] =
            "Manage Set-Alias shortcuts in $PROFILE. They take effect in new windows right after saving.",
        ["复制 $PROFILE 路径"] = "Copy $PROFILE path",
        ["搜索别名、路径或备注"] = "Search alias, path or note",
        ["别名（必填）"] = "Alias (required)",
        ["应用程序路径（必填）"] = "Application path (required)",
        ["备注（可选）"] = "Note (optional)",
        ["例如 blbl"] = "e.g. blbl",
        ["例如：打开哔哩哔哩"] = "e.g. open Bilibili",
        ["高级选项：启动参数（可选）"] = "Advanced: launch arguments (optional)",
        ["启动参数"] = "Launch arguments",
        ["例如：--incognito"] = "e.g. --incognito",
        ["每次启动该程序都会带上这些参数；在终端里临时传入的参数排在后面。含空格的参数请用英文双引号包起来。"] =
            "These arguments are added every time the app starts; arguments passed in the terminal come after them. Wrap values containing spaces in double quotes.",
        ["Enter 确定 · Esc 取消"] = "Enter to confirm · Esc to cancel",
        ["别名不能为空。"] = "Alias cannot be empty.",
        ["别名不能包含空格。"] = "Alias cannot contain spaces.",
        ["该别名已存在，请换一个。"] = "This alias already exists. Choose another.",
        ["请选择或填写应用程序路径。"] = "Select or enter an application path.",
        ["⚠ 文件不存在，仍可保存。"] = "⚠ File not found, but you can still save.",
        ["选择要启动的应用程序"] = "Select an application to launch",
        ["应用程序 (*.exe)|*.exe|所有文件 (*.*)|*.*"] = "Applications (*.exe)|*.exe|All files (*.*)|*.*",
        ["还没有别名"] = "No aliases yet",
        ["点击右上角“新建别名”，为常用应用添加一个 PowerShell 快捷命令。"] =
            "Click \"New Alias\" in the top right to add a PowerShell shortcut for an app.",
        ["没有匹配的别名"] = "No matching aliases",
        ["换个关键字试试，或清空搜索框查看全部别名。"] =
            "Try another keyword, or clear the search box to see all aliases.",
        ["路径已复制"] = "Path copied",
        ["尚未读取到 $PROFILE 路径，无法复制。"] = "$PROFILE path is not available yet; cannot copy.",
        ["执行策略受限"] = "Execution policy restricted",
        ["尝试修复"] = "Try to fix",

        // ---- 自定义命令页 ----
        ["把常用函数或脚本片段收进 $PROFILE 的受控区块，保存时只改写该区块，其余配置原样保留。"] =
            "Keep frequently used functions and snippets in a managed $PROFILE block. Only that block is rewritten.",
        ["保存时只重写该区块，区块外的 $PROFILE 内容不会被修改。"] =
            "Only this block is rewritten; the rest of $PROFILE is preserved.",
        ["搜索名称、代码或备注"] = "Search name, code or note",
        ["Ctrl+Enter 确定 · Esc 取消"] = "Ctrl+Enter to confirm · Esc to cancel",
        ["命令名称（必填）"] = "Command name (required)",
        ["例如 ll 或 Get-Project"] = "e.g. ll or Get-Project",
        ["例如：列出当前目录并按时间排序"] = "e.g. list the current directory by time",
        ["启用该命令"] = "Enable this command",
        ["命令代码（必填）"] = "Command code (required)",
        ["代码会被原样写入受控区块；禁用的命令保存为注释行，重新启用后完整还原。"] =
            "Code is written verbatim into the managed block. Disabled commands are saved as comments and restored when re-enabled.",
        ["命令名称不能为空。"] = "Command name cannot be empty.",
        ["命令名称不能包含空格。"] = "Command name cannot contain spaces.",
        ["命令名称包含 PowerShell 不支持的字符。"] = "Command name contains characters not supported by PowerShell.",
        ["该名称已存在，请换一个。"] = "This name already exists. Choose another.",
        ["{0} 是内置命令，请换一个名称。"] = "\"{0}\" is reserved for a built-in command. Choose another.",
        ["该名称是内置命令，请换一个。"] = "This name is a built-in command. Choose another.",
        ["内置"] = "Built-in",
        ["内置命令，随保存自动维护，不可编辑或删除"] =
            "Built-in command, maintained automatically on save; cannot be edited or deleted.",
        ["命令代码不能为空。"] = "Command code cannot be empty.",
        ["还没有自定义命令"] = "No custom commands yet",
        ["点击右上角“新建命令”，把常用函数或脚本片段写进 $PROFILE 的受控区块。"] =
            "Click \"New Command\" in the top right to add a function or snippet to the managed $PROFILE block.",
        ["没有匹配的命令"] = "No matching commands",
        ["换个关键字试试，或清空搜索框查看全部命令。"] =
            "Try another keyword, or clear the search box to see all commands.",

        // ---- 状态栏与错误信息（代码里用 {0} 占位） ----
        ["正在启动…"] = "Starting…",
        ["正在读取 $PROFILE…"] = "Reading $PROFILE…",
        ["无法读取 $PROFILE 路径"] = "Unable to read $PROFILE path",
        ["已保存 · 共 {0} 个别名"] = "Saved · {0} aliases",
        ["已保存 · 共 {0} 个自定义命令"] = "Saved · {0} custom commands",
        ["有未保存更改 · 共 {0} 个别名"] = "Unsaved changes · {0} aliases",
        ["有未保存更改 · 共 {0} 个自定义命令"] = "Unsaved changes · {0} custom commands",
        ["配置文件不存在，点击“保存并生效”时将自动创建。"] =
            "The profile does not exist; it will be created when you click \"Save & Apply\".",
        ["错误：读取 $PROFILE 失败：{0}"] = "Error: failed to read $PROFILE: {0}",
        ["错误：无法定位 $PROFILE：{0}"] = "Error: cannot locate $PROFILE: {0}",
        ["正在写入 $PROFILE…"] = "Writing $PROFILE…",
        ["已按 pam-data.json 重建 $PROFILE 托管区。"] =
            "Rebuilt the managed section of $PROFILE from pam-data.json.",
        ["{0} 与 $PROFILE 中已有命令同名，这些别名不会生效。"] =
            "{0} conflicts with an existing command in $PROFILE; these aliases will not take effect.",
        ["错误：写入失败：{0}"] = "Error: failed to write: {0}",
        ["错误：检查执行策略失败：{0}"] = "Error: failed to check execution policy: {0}",
        ["执行策略为 {0}，$PROFILE 可能无法加载。"] = "Execution policy is {0}; $PROFILE may not load.",
        ["正在将执行策略设为 RemoteSigned (CurrentUser)…"] =
            "Setting execution policy to RemoteSigned (CurrentUser)…",
        ["错误：修复执行策略失败：{0}"] = "Error: failed to fix execution policy: {0}",
        ["执行策略仍为 {0}，请检查系统策略。"] = "Execution policy is still {0}; check the system policy.",
        ["执行策略仍受限，请手动检查系统策略。"] = "Execution policy is still restricted; check it manually.",
        ["执行策略已设为 RemoteSigned (CurrentUser)。再次保存即可在新窗口生效。"] =
            "Execution policy set to RemoteSigned (CurrentUser). Save again to apply in a new window.",
        ["已写入 · 执行策略受限，未启动新的 PowerShell 窗口。请点击“尝试修复”。"] =
            "Saved · Execution policy is restricted; no new PowerShell window was opened. Click \"Try to fix\".",
        ["已写入 · 已启动新的 PowerShell 窗口 · 共 {0} 个别名"] =
            "Saved · Opened a new PowerShell window · {0} aliases",
        ["错误：已写入，但启动 PowerShell 失败：{0}"] =
            "Error: saved, but failed to launch PowerShell: {0}",
        ["错误：复制路径失败：{0}"] = "Error: failed to copy path: {0}",
        ["编辑别名 · {0}"] = "Edit alias · {0}",
        ["编辑命令 · {0}"] = "Edit command · {0}",
        ["错误：读取自定义命令失败：{0}"] = "Error: failed to read custom commands: {0}",
        ["正在写入自定义命令…"] = "Writing custom commands…",
        ["已创建 $PROFILE 并写入"] = "Created $PROFILE and saved",
        ["已写入"] = "Saved",
        // 这四条是运行时拼好的完整状态文案（prefix 只有"已写入"和"已创建 $PROFILE 并写入"两种），
        // 写成显式模板才能让 prefix 一起被翻译；不要再加通用的 {0} 前缀模板，否则会先命中而漏译。
        ["已写入 · 共 {0} 个自定义命令 · 执行策略受限，未启动新的 PowerShell 窗口。"] =
            "Saved · {0} custom commands · Execution policy restricted; no new window was opened.",
        ["已创建 $PROFILE 并写入 · 共 {0} 个自定义命令 · 执行策略受限，未启动新的 PowerShell 窗口。"] =
            "Created $PROFILE and saved · {0} custom commands · Execution policy restricted; no new window was opened.",
        ["已写入 · 已启动新的 PowerShell 窗口 · 共 {0} 个自定义命令"] =
            "Saved · Opened a new PowerShell window · {0} custom commands",
        ["已创建 $PROFILE 并写入 · 已启动新的 PowerShell 窗口 · 共 {0} 个自定义命令"] =
            "Created $PROFILE and saved · Opened a new PowerShell window · {0} custom commands",
        ["错误：写入自定义命令失败：{0}"] = "Error: failed to write custom commands: {0}",

        // ---- 设置页 ----
        ["调整外观、终端与全局热键。修改会立即保存，并同步到后台托盘。"] =
            "Adjust appearance, terminal and global hotkey. Changes are saved immediately and synced to the tray.",
        ["外观"] = "Appearance",
        ["主题"] = "Theme",
        ["PAM 界面的明暗风格。"] = "Light / dark appearance of the PAM interface.",
        ["语言"] = "Language",
        ["界面显示语言，切换后立即生效。"] = "Interface language; applies immediately.",
        ["跟随系统"] = "Follow system",
        ["浅色"] = "Light",
        ["深色"] = "Dark",
        ["简体中文"] = "简体中文",
        ["English"] = "English",
        ["自动探测（优先 PowerShell 7）"] = "Auto-detect (prefer PowerShell 7)",
        ["PowerShell 7 (pwsh.exe)"] = "PowerShell 7 (pwsh.exe)",
        ["Windows PowerShell 5.1 (powershell.exe)"] = "Windows PowerShell 5.1 (powershell.exe)",
        ["终端"] = "Terminal",
        ["默认终端"] = "Default terminal",
        ["用于全局热键唤醒的终端、保存后启动的窗口，以及下方“打开配置文件”。"] =
            "Used by the global hotkey, by the window opened after saving, and by \"Open profile file\" below.",
        ["未检测到 PowerShell 7，已回退到 Windows PowerShell 5.1。"] =
            "PowerShell 7 was not found; falling back to Windows PowerShell 5.1.",
        ["打开配置文件"] = "Open profile file",
        ["按上方选择的默认终端打开对应的 $PROFILE；文件不存在时会自动创建。"] =
            "Opens the matching $PROFILE for the selected terminal. The file is created if missing.",
        ["尚未解析到 $PROFILE 路径"] = "$PROFILE path not resolved yet",
        ["全局热键"] = "Global hotkey",
        ["打开终端"] = "Open terminal",
        ["当前热键"] = "Current hotkey",
        ["更改…"] = "Change…",
        ["恢复默认"] = "Restore default",
        ["点击“更改”后按下组合键；Esc 取消录制。"] =
            "Click \"Change\" then press a key combination. Esc cancels.",
        ["请按下组合键…"] = "Press a key combination…",
        ["至少包含一个修饰键（Ctrl / Alt / Shift / Win）。"] =
            "Include at least one modifier (Ctrl / Alt / Shift / Win).",
        ["检测冲突"] = "Check conflicts",
        ["应用热键"] = "Apply hotkey",
        ["可用：该组合当前没有被系统或其他软件占用。"] =
            "Available: not currently used by Windows or other apps.",
        ["系统保留：{0}"] = "Reserved by Windows: {0}",
        ["已被占用：{0}"] = "Already in use: {0}",
        ["当前由 PAM 托盘占用，可继续使用。"] = "Currently held by the PAM tray; safe to keep.",
        ["常见软件快捷键提醒：{0}"] = "Common shortcut warning: {0}",
        ["尚未检测，点击下方“检测冲突”。"] = "Not checked yet. Click \"Check conflicts\" below.",
        ["终端输入"] = "Terminal input",
        ["打开终端时把中文输入法切到英文模式"] = "Switch the Chinese IME to English mode when a terminal opens",
        ["后台托盘检测到新打开的 PowerShell / Windows Terminal 窗口时，先读任务栏输入指示器；只有处于中文模式才模拟一次 Shift 切到英文输入状态。输入法本身不变，也不会换成英文键盘；已是英文模式时一个键都不按。每个窗口只处理一次。"] =
            "When a new PowerShell / Windows Terminal window opens, the tray reads the taskbar input indicator and taps Shift once only if the IME is in Chinese mode. The input method itself and the keyboard layout stay unchanged; nothing is pressed when it is already in English mode. Each window is handled once.",
        ["别名启动"] = "Alias launching",
        ["独立进程启动（静默日志）"] = "Launch as a separate process (silence output)",
        ["用别名启动软件时改为独立进程，并把子进程的 stdout/stderr 重定向到 NUL 设备：终端不再刷程序日志，关闭 PowerShell 也不会把软件一起关掉。关闭则恢复原来的直接启动方式。$PROFILE 只在终端启动时加载一次，保存别名后要新开终端窗口才生效。"] =
            "Launch aliases in a separate process and redirect its stdout/stderr to the NUL device: no program output in the terminal, and closing PowerShell will not close the app. Turn off to restore the original direct launch. $PROFILE is loaded only when a terminal starts, so open a new terminal window after saving aliases.",
        ["启动时显示别名备注"] = "Show alias remark on launch",
        ["启动前在终端打印一行该别名的备注；没有备注时不打印。"] =
            "Print the alias remark in the terminal before launching; nothing is printed when the remark is empty.",
        ["未找到命令时显示友好提示"] = "Friendly hint for unknown commands",
        ["终端里输入不存在的命令时，用一行简短提示代替 PowerShell 的红色报错，并引导到 pls 或管理器。关闭后恢复默认报错；$PROFILE 只在终端启动时加载，保存后要新开终端窗口才生效。"] =
            "When a command is not found, replace PowerShell's red error with one short hint pointing to pls or the manager. Turn off to restore the default error. $PROFILE is loaded only when a terminal starts, so open a new terminal window after saving.",
        ["后台运行"] = "Background",
        ["开机自动启动后台托盘"] = "Start the tray automatically at sign-in",
        ["登录 Windows 后自动启动托盘并注册全局热键，无需先打开管理器。移动程序目录后打开一次管理器即可更新启动项路径。"] =
            "Start the tray and register the global hotkey when you sign in to Windows, without opening the manager first. If you move the program folder, open the manager once to refresh the startup entry path.",
        ["关闭管理器时保留后台托盘"] = "Keep the tray running when the manager closes",
        ["关闭后台托盘后，全局热键立即失效（无法用快捷键唤起终端），托盘菜单也会消失；下次打开管理器或点“立即启动”才会恢复。"] =
            "Turning off the background tray immediately disables the global hotkey (you can no longer open a terminal with it) and removes the tray menu. It comes back only when you reopen the manager or click \"Start now\".",
        ["后台托盘状态"] = "Tray status",
        ["立即启动"] = "Start now",
        ["结束后台运行"] = "Stop background",
        ["设置文件"] = "Settings file",
        ["恢复默认设置"] = "Restore defaults",
        ["所有更改都会立即写入 settings.json。"] = "All changes are written to settings.json immediately.",
        ["切换默认终端后已重新读取 $PROFILE，未保存的更改已丢弃。"] =
            "The new default terminal's $PROFILE has been loaded; unsaved changes were discarded.",
        ["设置已保存并同步到后台托盘。"] = "Settings saved and synced to the tray.",
        ["设置已保存，但未找到后台托盘程序，热键将在下次启动托盘后生效。"] =
            "Settings saved. The tray executable was not found, so the hotkey will take effect when it is started.",
        ["保存设置失败：{0}"] = "Failed to save settings: {0}",
        ["打开配置文件失败：{0}"] = "Failed to open the profile file: {0}",
        ["已创建并打开配置文件：{0}"] = "Created and opened the profile file: {0}",
        ["已打开配置文件：{0}"] = "Opened the profile file: {0}",
        ["热键已应用并同步到后台托盘。"] = "Hotkey applied and synced to the tray.",
        ["热键冲突，未应用。"] = "Hotkey conflicts; not applied.",
        ["热键冲突"] = "Hotkey conflict",
        ["仍然使用这个热键吗？"] = "Use this hotkey anyway?",
        ["确定要恢复全部默认设置吗？"] = "Restore all settings to their defaults?",
        ["恢复默认设置"] = "Restore defaults",
        ["已恢复默认设置。"] = "Default settings restored.",
        ["后台托盘已启动。"] = "The background tray has been started.",
        ["已开启开机自动启动：登录 Windows 后会自动运行后台托盘。"] =
            "Startup enabled: the tray will run automatically when you sign in to Windows.",
        ["已关闭开机自动启动：下次登录不会自动运行后台托盘。"] =
            "Startup disabled: the tray will not run automatically at your next sign-in.",
        ["设置已保存，但开机启动项未能更新；请检查是否被安全软件或系统策略拦截。"] =
            "Settings saved, but the startup entry could not be updated. Check whether security software or a system policy blocked it.",
        ["无法启动后台托盘，请检查 PAM.Tray.exe 是否与管理程序在同一目录。"] =
            "Could not start the tray. Check that PAM.Tray.exe is next to the manager executable.",
        ["已通知后台托盘退出。"] = "The background tray was asked to exit.",
        ["热键修改后需要后台托盘重新注册；当前托盘未运行，已为你启动。"] =
            "The tray must re-register the hotkey. It was not running, so it has been started.",

        // ---- 热键解析错误 ----
        ["热键不能为空。"] = "Hotkey cannot be empty.",
        ["热键必须包含至少一个修饰键和一个普通按键。"] =
            "A hotkey needs at least one modifier and one regular key.",
        ["热键只能包含一个普通按键。"] = "A hotkey can only contain one regular key.",
        ["热键必须包含 Ctrl、Alt、Shift 或 Win 中的至少一个修饰键。"] =
            "Include at least one of Ctrl / Alt / Shift / Win.",
        ["热键缺少普通按键。"] = "Missing regular key.",
        ["无法识别按键“{0}”。"] = "Unrecognized key \"{0}\".",
        ["不能把修饰键本身当作热键按键。"] = "A modifier key cannot be the hotkey key.",
        ["无效的按键。"] = "Invalid key.",
        ["不支持该按键，请换一个字母、数字或功能键。"] =
            "Unsupported key. Use a letter, digit or function key instead.",

        // ---- 系统保留组合 ----
        ["Ctrl+Alt+Del 由 Windows 安全界面独占，任何应用都无法接管。"] =
            "Ctrl+Alt+Del is reserved by the Windows secure screen and cannot be captured.",
        ["Ctrl+Shift+Esc 用于打开任务管理器。"] = "Ctrl+Shift+Esc opens Task Manager.",
        ["Ctrl+Esc 用于打开开始菜单。"] = "Ctrl+Esc opens the Start menu.",
        ["Alt+Tab 用于切换窗口。"] = "Alt+Tab switches windows.",
        ["Alt+Shift+Tab 用于反向切换窗口。"] = "Alt+Shift+Tab switches windows in reverse order.",
        ["Alt+F4 用于关闭当前窗口。"] = "Alt+F4 closes the current window.",
        ["Alt+Esc 用于按窗口顺序切换。"] = "Alt+Esc cycles through windows.",
        ["Win+L 用于锁定计算机。"] = "Win+L locks the computer.",
        ["Win+Enter 用于启动 Windows 讲述人。"] = "Win+Enter starts Windows Narrator.",
        ["Win+Tab 用于打开任务视图。"] = "Win+Tab opens Task View.",
        ["Win+D 用于显示桌面。"] = "Win+D shows the desktop.",
        ["Win+E 用于打开文件资源管理器。"] = "Win+E opens File Explorer.",
        ["Win+R 用于打开“运行”对话框。"] = "Win+R opens the Run dialog.",
        ["Win+I 用于打开 Windows 设置。"] = "Win+I opens Windows Settings.",
        ["Win+S / Win+Q 用于 Windows 搜索。"] = "Win+S / Win+Q opens Windows Search.",
        ["Win+V 用于打开剪贴板历史。"] = "Win+V opens clipboard history.",
        ["Win+K 用于打开“连接”面板。"] = "Win+K opens the Connect pane.",
        ["Win+P 用于切换投影模式。"] = "Win+P switches projection modes.",
        ["Win+A 用于打开操作中心。"] = "Win+A opens Action Center.",
        ["Win+G 用于打开 Xbox Game Bar。"] = "Win+G opens Xbox Game Bar.",
        ["Win+U 用于打开轻松使用设置。"] = "Win+U opens Ease of Access settings.",
        ["Win+X 用于打开快速链接菜单。"] = "Win+X opens the Quick Link menu.",
        ["Win+T 用于浏览任务栏程序。"] = "Win+T browses taskbar apps.",
        ["Win+PrintScreen 用于系统截图并保存。"] = "Win+PrintScreen captures and saves a screenshot.",
        ["Win++ / Win+- 用于打开或关闭放大镜。"] = "Win++ / Win+- controls the Magnifier.",
        ["Win+Shift+S 由 Windows 截图工具占用。"] = "Win+Shift+S is used by the Windows snipping tool.",
        ["该组合用于 Windows 虚拟桌面管理。"] = "This combination manages Windows virtual desktops.",
        ["Win+Alt+R 由 Xbox Game Bar 录制功能占用。"] =
            "Win+Alt+R is used by Xbox Game Bar recording.",

        // ---- 常见软件快捷键 ----
        ["Ctrl+S 是几乎所有软件的“保存”，注册为全局热键后会被拦截。"] =
            "Ctrl+S is Save in almost every app; a global hotkey would intercept it.",
        ["Ctrl+N 是常见的“新建”，注册为全局热键后会被拦截。"] =
            "Ctrl+N is a common New shortcut; a global hotkey would intercept it.",
        ["Ctrl+O 是常见的“打开”，注册为全局热键后会被拦截。"] =
            "Ctrl+O is a common Open shortcut; a global hotkey would intercept it.",
        ["Ctrl+C 是系统级“复制”，注册为全局热键后会被拦截。"] =
            "Ctrl+C is the system Copy shortcut; a global hotkey would intercept it.",
        ["Ctrl+V 是系统级“粘贴”，注册为全局热键后会被拦截。"] =
            "Ctrl+V is the system Paste shortcut; a global hotkey would intercept it.",
        ["Ctrl+X 是系统级“剪切”，注册为全局热键后会被拦截。"] =
            "Ctrl+X is the system Cut shortcut; a global hotkey would intercept it.",
        ["Ctrl+A 是常见的“全选”，注册为全局热键后会被拦截。"] =
            "Ctrl+A is a common Select All shortcut; a global hotkey would intercept it.",
        ["Ctrl+Z 是常见的“撤销”，注册为全局热键后会被拦截。"] =
            "Ctrl+Z is a common Undo shortcut; a global hotkey would intercept it.",
        ["Ctrl+P 是常见的“打印”，注册为全局热键后会被拦截。"] =
            "Ctrl+P is a common Print shortcut; a global hotkey would intercept it.",
        ["Ctrl+F 是常见的“查找”，注册为全局热键后会被拦截。"] =
            "Ctrl+F is a common Find shortcut; a global hotkey would intercept it.",
        ["Ctrl+W 是常见的“关闭标签页”，注册为全局热键后会被拦截。"] =
            "Ctrl+W is a common Close Tab shortcut; a global hotkey would intercept it.",
        ["Ctrl+T 是常见的“新建标签页”，注册为全局热键后会被拦截。"] =
            "Ctrl+T is a common New Tab shortcut; a global hotkey would intercept it.",
        ["Ctrl+Shift+T 是浏览器“恢复关闭的标签页”，注册为全局热键后会被拦截。"] =
            "Ctrl+Shift+T reopens closed browser tabs; a global hotkey would intercept it.",
        ["QQ / 微信等截图工具常使用 Ctrl+Alt+A。"] =
            "Screenshot tools such as QQ / WeChat often use Ctrl+Alt+A.",
        ["部分终端工具或 Linux 子系统使用 Ctrl+Alt+T。"] =
            "Some terminal tools and WSL launchers use Ctrl+Alt+T.",
        ["截图工具与部分 IDE 常使用 Ctrl+Shift+A。"] =
            "Screenshot tools and some IDEs often use Ctrl+Shift+A.",
        ["截图或输入法工具常使用 Ctrl+Alt+S。"] =
            "Screenshot or input-method tools often use Ctrl+Alt+S.",
        ["中文输入法的中英文切换常使用 Ctrl+Space。"] =
            "Chinese input methods often use Ctrl+Space to switch languages.",
        ["Windows 窗口系统菜单常使用 Alt+Space。"] =
            "Alt+Space opens the Windows window system menu.",
        ["该组合已被系统或其他常驻软件占用，请换一个。"] =
            "This combination is already taken by Windows or another running app. Pick another one.",

        // ---- 托盘 ----
        ["热键被占用"] = "Hotkey is already in use",
        ["确定要退出 {0} 吗？"] = "Exit {0}?",
        ["未找到主界面程序：{0}"] = "Main app not found: {0}",
        ["启动主界面失败：{0}"] = "Failed to start the main app: {0}",
        ["启动 Windows Terminal 失败：{0}"] = "Failed to start Windows Terminal: {0}",
        ["重新加载设置"] = "Reload settings",
        ["热键：{0}"] = "Hotkey: {0}",
        ["热键 {0} 注册失败，可能已被系统或其他软件占用。请到“设置”中更换。"] =
            "Could not register hotkey {0}. It may be taken by Windows or another app. Change it in Settings.",
        ["设置已重新加载：热键 {0}"] = "Settings reloaded: hotkey {0}",

        // ---- 美化（Starship / 字体 / 主题） ----
        ["美化"] = "Beautify",
        ["为终端配置 Starship 提示符与 Nerd Font 字体。"] =
            "Configure the Starship prompt and Nerd Fonts for your terminal.",
        ["检测中…"] = "Detecting…",
        ["未安装"] = "Not installed",
        ["已安装 {0}"] = "Installed {0}",
        ["未检测到 Scoop"] = "Scoop not detected",
        ["Scoop 已就绪"] = "Scoop is ready",
        ["Starship 是跨 shell 的提示符引擎；通过 Scoop 安装后自动可用，无需手动配置 PATH。"] =
            "Starship is a cross-shell prompt engine. Once installed via Scoop it works out of the box; no PATH setup needed.",
        ["Starship 官方建议先装好 Nerd Font：请在上方选择字体后再安装。"] =
            "Starship's docs recommend installing a Nerd Font first: pick a font above before installing.",
        ["已安装，无需重复安装；换字体后重新打开终端即可生效。"] =
            "Already installed — no need to install again. Reopen your terminal after changing fonts to take effect.",
        ["未选字体或已安装时不可用。"] =
            "Disabled until a font is selected; unavailable when Starship is already installed.",
        ["一键安装（含 Scoop）"] = "One-click install (incl. Scoop)",
        ["重新检测"] = "Re-detect",
        ["正在安装 Scoop 与 Starship，可能需要几分钟…"] =
            "Installing Scoop and Starship. This may take a few minutes…",
        ["正在安装 Scoop…"] = "Installing Scoop…",
        ["Scoop 安装完成。"] = "Scoop installed.",
        ["Starship 安装完成。"] = "Starship installed.",
        ["未检测到 Scoop，请先安装 Scoop。"] = "Scoop was not detected. Install Scoop first.",
        ["scoop install starship 失败（退出码 {0}）。{1}"] =
            "scoop install starship failed (exit code {0}). {1}",
        ["Scoop 安装失败（退出码 {0}）。{1}"] = "Scoop installation failed (exit code {0}). {1}",
        ["操作完成。"] = "Done.",
        ["操作失败：{0}"] = "Operation failed: {0}",

        // ---------------- 美化页：外观（Windows Terminal「默认值 → 外观」） ----------------
        ["配置终端外观、Starship 提示符与 Nerd Font；首次修改终端配置前会自动保存一份原始配置，随时可恢复。"] =
            "Configure terminal appearance, the Starship prompt and Nerd Fonts. A snapshot of the original terminal config is saved before the first change and can be restored at any time.",
        ["对应 Windows Terminal「默认值 → 外观」的设置，写入 settings.json 的 profiles.defaults；只保存改动过的项，留空或选「终端默认」即交还终端默认。"] =
            "Maps to Windows Terminal's Defaults > Appearance and writes profiles.defaults in settings.json. Only changed items are saved; leave a field empty or pick \"Terminal default\" to hand it back to the terminal.",
        ["文本"] = "Text",
        ["字体"] = "Font",
        ["写入 profiles.defaults.font.face；内置 Cascadia Code NF 已随本程序打包，未安装时可一键按当前用户安装。"] =
            "Writes profiles.defaults.font.face. Cascadia Code NF ships with this app; install it for the current user with one click if missing.",
        ["显示效果"] = "Effects",
        ["复古风格终端显示"] = "Retro terminal effect",
        ["CRT 扫描线与辉光效果（experimental.retroTerminalEffect）。"] =
            "CRT scanlines and glow (experimental.retroTerminalEffect).",
        ["启用复古效果"] = "Enable retro effect",
        ["光标"] = "Cursor",
        ["光标形状"] = "Cursor shape",
        ["对应 cursorShape；选「终端默认」即删除该设置。"] =
            "Maps to cursorShape. Pick \"Terminal default\" to remove the setting.",
        ["光标颜色"] = "Cursor color",
        ["对应 cursorColor，填 #RGB 或 #RRGGBB；留空使用配色方案里的光标色。"] =
            "Maps to cursorColor. Enter #RGB or #RRGGBB; leave empty to use the color scheme's cursor color.",
        ["例如 #FFFFFF；留空表示不设置"] = "For example #FFFFFF; leave empty to leave it unset",
        ["背景图"] = "Background image",
        ["背景图路径"] = "Background image path",
        ["对应 backgroundImage；也支持 desktopWallpaper 与 ms-appdata URI。清空即移除背景图。"] =
            "Maps to backgroundImage. desktopWallpaper and ms-appdata URIs also work. Clear the field to remove the image.",
        ["清空"] = "Clear",
        ["移除背景图设置"] = "Remove the background image setting",
        ["背景图透明度"] = "Background image opacity",
        ["对应 backgroundImageOpacity（0~1），这里按百分比显示。"] =
            "Maps to backgroundImageOpacity (0-1), shown here as a percentage.",
        ["窗口"] = "Window",
        ["窗口透明度"] = "Window opacity",
        ["对应 opacity（0~100，100 为完全不透明）；需低于 100 才能看出效果。"] =
            "Maps to opacity (0-100, where 100 is fully opaque). Set it below 100 to see the effect.",
        ["窗口透明亚克力材质"] = "Acrylic transparency",
        ["对应 useAcrylic；开启后透明背景带模糊（Windows 11 效果最佳）。"] =
            "Maps to useAcrylic. Enabling it blurs the transparent background (best on Windows 11).",
        ["使用亚克力材质"] = "Use acrylic",
        ["放弃更改"] = "Discard changes",
        ["保存外观设置"] = "Save appearance",
        ["「恢复 Windows Terminal 备份」：本程序第一次修改终端配置（字体 / 外观）时会把当时的 settings.json 完整备份为 settings.json.bak，之后无论改多少次都不覆盖；点此永远恢复到安装本软件前的配置。备份保留，可反复恢复。"] =
            "\"Restore Windows Terminal backup\": the first time this app changes terminal settings (font / appearance), it saves the whole settings.json as settings.json.bak. Later changes never overwrite it, so restoring always returns to the config from before this app was installed. The backup is kept and can be restored repeatedly.",
        ["回到本软件第一次修改前的 settings.json；原始备份不会被覆盖或删除，可反复恢复。"] =
            "Return to the settings.json from before this app's first change. The original backup is never overwritten or deleted, so it can be restored repeatedly.",
        ["原始配置备份创建于 {0}；恢复时永远回到这份配置。"] =
            "Original backup created at {0}. Restoring always returns to this config.",
        ["还没有原始配置备份；第一次通过本程序修改终端配置时会自动生成。"] =
            "No original backup yet. It is created automatically the first time this app changes terminal settings.",
        ["备份文件：{0}"] = "Backup file: {0}",
        ["已恢复到本软件首次修改前的 Windows Terminal 配置；原始备份保留，可再次恢复。"] =
            "Restored the Windows Terminal config from before this app's first change. The original backup is kept and can be restored again.",
        ["有尚未保存的修改；保存后打开新终端窗口即可看到效果。"] =
            "Unsaved changes. Save them, then open a new terminal window to see the effect.",
        ["当前显示的是 settings.json 里已保存的值；改动后点「保存外观设置」。"] =
            "Showing the values saved in settings.json. Click \"Save appearance\" after making changes.",
        ["正在保存终端外观设置…"] = "Saving terminal appearance settings…",
        ["没有需要保存的更改。"] = "Nothing to save.",
        ["光标颜色要写成 #RGB 或 #RRGGBB；留空表示使用终端默认颜色。"] =
            "Cursor color must be #RGB or #RRGGBB; leave empty to use the terminal default.",
        ["已保存 {0} 项终端外观设置，打开新终端窗口即可看到效果。"] =
            "Saved {0} terminal appearance setting(s). Open a new terminal window to see the effect.",
        ["选择终端背景图"] = "Choose a terminal background image",
        ["图片文件 (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|所有文件 (*.*)|*.*"] =
            "Image files (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All files (*.*)|*.*",
        ["终端默认"] = "Terminal default",
        ["竖线 (bar)"] = "Bar",
        ["复古粗块 (vintage)"] = "Vintage block",
        ["下划线 (underscore)"] = "Underscore",
        ["实心框 (filledBox)"] = "Filled box",
        ["空心框 (emptyBox)"] = "Empty box",
        ["双下划线 (doubleUnderscore)"] = "Double underscore",

        // 外观写入失败的提示（出现即代表 settings.json 未被改动）。
        ["未知的外观设置项。"] = "Unknown appearance setting.",
        ["外观设置的值不是合法的 JSON 字面量。"] = "The appearance value is not a valid JSON literal.",
        ["开关值只能是 true / false。"] = "A toggle can only be true or false.",
        ["光标形状不合法。"] = "Invalid cursor shape.",
        ["不支持的光标形状。"] = "Unsupported cursor shape.",
        ["光标颜色要写成 #RGB 或 #RRGGBB。"] = "Cursor color must be #RGB or #RRGGBB.",
        ["背景图路径不能为空。"] = "The background image path cannot be empty.",
        ["背景图透明度必须在 0 ~ 1 之间。"] = "Background image opacity must be between 0 and 1.",
        ["窗口透明度必须是 0 ~ 100 的整数。"] = "Window opacity must be an integer between 0 and 100.",
        ["settings.json 的目标层级不是对象，无法自动修改，请手动调整。"] =
            "A target level in settings.json is not an object; cannot modify automatically. Edit it manually.",
        ["settings.json 的目标属性不是简单值或结构异常，无法自动修改，请手动调整。"] =
            "A target property in settings.json is not a simple value or has an unexpected structure; edit it manually.",
        ["外观设置的属性路径为空。"] = "The appearance setting path is empty.",

        ["Windows Terminal 当前字体：{0}"] = "Windows Terminal current font: {0}",
        ["Windows Terminal 当前字体：未设置（跟随 WT 默认）"] =
            "Windows Terminal current font: not set (using the WT default)",
        ["安装内置字体"] = "Install bundled font",
        ["把打包的 Cascadia Code NF 安装为当前用户字体（免管理员权限）。"] =
            "Installs the bundled Cascadia Code NF for the current user (no admin rights needed).",
        ["想要其他 Nerd Font？安装 Scoop 后执行：scoop bucket add nerd-fonts，再安装如 JetBrains-Mono-NF、FiraCode-NF 等包，重启本页即可在下拉中看到。"] =
            "Want other Nerd Fonts? With Scoop installed, run: scoop bucket add nerd-fonts, then install e.g. JetBrains-Mono-NF or FiraCode-NF. Reopen this tab and they will appear in the list.",
        ["正在安装内置字体…"] = "Installing the bundled font…",
        ["未找到 Windows Terminal 的 settings.json，请先启动一次 Windows Terminal。"] =
            "Windows Terminal settings.json was not found. Start Windows Terminal once first.",
        ["未找到字体文件：{0}"] = "Font file not found: {0}",
        ["只支持 .ttf 字体文件。"] = "Only .ttf font files are supported.",
        ["安装字体失败：{0}"] = "Failed to install the font: {0}",
        ["已为当前用户安装 {0}。"] = "Installed {0} for the current user.",
        ["字体名称不能为空。"] = "The font name cannot be empty.",

        ["主题"] = "Theme",
        ["主题预设"] = "Theme preset",
        ["标注“需 Nerd Font”的预设要先装好字体，否则提示符会出现乱码方块。"] =
            "Presets marked as needing a Nerd Font require the font to be installed first, otherwise the prompt shows placeholder boxes.",
        ["应用主题"] = "Apply theme",
        ["正在应用主题预设…"] = "Applying the theme preset…",
        ["主题已应用：{0}；原 starship.toml 已自动备份。"] =
            "Theme applied: {0}. The original starship.toml has been backed up.",
        ["主题已应用：{0}。该预设需要 Nerd Font，当前未安装内置字体，提示符可能显示乱码方块，建议先安装。"] =
            "Theme applied: {0}. This preset needs a Nerd Font, which is not installed yet; the prompt may show placeholder boxes. Install the bundled font first.",
        ["请先选择一个主题预设。"] = "Select a theme preset first.",
        ["未找到内置主题预设：{0}"] = "Bundled theme preset not found: {0}",

        ["配置维护"] = "Config maintenance",
        ["已创建"] = "Created",
        ["未创建"] = "Not created",
        ["starship.toml.bak 存在"] = "starship.toml.bak exists",
        ["打开 starship.toml"] = "Open starship.toml",
        ["恢复 starship.toml 备份"] = "Restore starship.toml backup",
        ["恢复 Windows Terminal 备份"] = "Restore Windows Terminal backup",
        ["正在打开 starship.toml…"] = "Opening starship.toml…",
        ["已创建并打开配置文件：{0}"] = "Created and opened the config file: {0}",
        ["已打开配置文件：{0}"] = "Opened the config file: {0}",
        ["正在恢复 starship.toml 备份…"] = "Restoring the starship.toml backup…",
        ["已从备份恢复 starship.toml。"] = "starship.toml has been restored from the backup.",
        ["正在恢复 Windows Terminal 备份…"] = "Restoring the Windows Terminal backup…",
        ["没有可恢复的备份。"] = "No backup to restore.",
        ["状态刷新失败：{0}"] = "Failed to refresh the status: {0}",

        // Windows Terminal settings.json 的拼接失败提示（出现即代表未写入，原配置完好）。
        ["settings.json 的顶层不是对象，无法自动修改。"] =
            "The root of settings.json is not an object; cannot modify automatically.",
        ["settings.json 的 profiles 不是对象（可能是旧版数组结构），无法自动修改，请手动调整。"] =
            "profiles in settings.json is not an object (possibly a legacy array layout); cannot modify automatically. Edit it manually.",
        ["settings.json 的 profiles 结构异常，无法自动修改。"] =
            "Unexpected profiles structure in settings.json; cannot modify automatically.",
        ["settings.json 的 profiles.defaults 不是对象，无法自动修改。"] =
            "profiles.defaults in settings.json is not an object; cannot modify automatically.",
        ["settings.json 的 profiles.defaults 结构异常，无法自动修改。"] =
            "Unexpected profiles.defaults structure in settings.json; cannot modify automatically.",
        ["profiles.defaults.font 不是对象，无法自动修改。"] =
            "profiles.defaults.font is not an object; cannot modify automatically.",
        ["profiles.defaults.font 结构异常，无法自动修改。"] =
            "Unexpected profiles.defaults.font structure in settings.json; cannot modify automatically.",
        ["无法定位 profiles.defaults.font.face，请手动修改字体设置。"] =
            "Cannot locate profiles.defaults.font.face. Edit the font setting manually.",
        ["settings.json 结构异常，无法自动修改。"] =
            "Unexpected settings.json structure; cannot modify automatically.",
        ["写入后校验未通过，已保持原配置不变。"] =
            "Post-write validation failed; the original file was kept unchanged.",

        // ---------------- 美化页：自定义提示符 ----------------
        ["自定义提示符"] = "Custom prompt",
        ["预览为示意值，实际文字由 Starship 按系统状态生成（切换目录时路径会跟着变）。"] =
            "The preview uses sample values; Starship generates the real text from system state (the path follows cd).",
        ["插入变量"] = "Insert variable",
        ["当前颜色"] = "Current color",
        ["空格"] = "Space",
        ["用户名"] = "Username",
        ["主机名"] = "Hostname",
        ["冒号"] = "Colon",
        ["结束符"] = "Tail",
        ["重置为默认"] = "Reset to defaults",
        ["恢复应用前配置"] = "Restore pre-apply config",
        ["应用到 Starship"] = "Apply to Starship",
        ["starship.toml 尚未创建，应用后会自动生成"] = "starship.toml does not exist yet; applying will create it",
        ["当前提示符由 PowerShell 管理器接管"] = "The prompt is currently managed by PowerShell Manager",
        ["当前使用原有提示符配置（尚未应用过自定义）"] = "Using the original prompt (custom prompt not applied yet)",
        ["正在写入自定义提示符…"] = "Writing the custom prompt...",
        ["自定义提示符已写入 starship.toml，原配置已单独备份；打开新终端即可看到效果。"] =
            "Custom prompt written to starship.toml with a separate backup of the original config. Open a new terminal to see it.",
        ["正在恢复应用前的提示符配置…"] = "Restoring the pre-apply prompt config...",
        ["已恢复应用前的 starship.toml，自定义提示符已移除。"] =
            "Restored the original starship.toml; the custom prompt has been removed.",
        ["没有可恢复的应用前备份。"] = "No pre-apply backup to restore.",
        ["已重置为默认内容，点击“应用到 Starship”才会写入。"] =
            "Defaults restored in the editor. Click \"Apply to Starship\" to write them.",
        ["提示符前空一行"] = "Blank line before prompt",
        ["路径分隔符用 /"] = "Use / as path separator",
        ["开启后每个提示符前会空一行（Starship add_newline）。"] =
            "When enabled, an empty line is inserted before every prompt (Starship add_newline).",
        ["取消后路径用系统默认的反斜杠 \\ 分隔（Starship use_os_path_sep）。"] =
            "When disabled, paths use the system backslash separator (Starship use_os_path_sep).",
    };

    private string _language = AppSettings.LanguageZhCN;

    /// <summary>
    /// 带占位符的词条（例如「共 {0} 个别名」）会被编译成匹配模式，
    /// 让运行时拼好的字符串（例如「共 3 个别名」）也能被翻译。
    /// </summary>
    private static readonly Lazy<List<(Regex Pattern, string Template)>> FormatPatterns =
        new(BuildFormatPatterns);

    private LocalizationService() { }

    /// <summary>当前语言（zh-CN / en-US）。</summary>
    public string Language => _language;

    public bool IsEnglish => string.Equals(_language, AppSettings.LanguageEnUS, StringComparison.OrdinalIgnoreCase);

    /// <summary>语言切换后触发，界面遍历器与视图模型据此刷新文本。</summary>
    public event EventHandler? LanguageChanged;

    /// <summary>把中文原文翻译成当前语言；未收录的条目原样返回。</summary>
    public string T(string? source)
    {
        if (string.IsNullOrEmpty(source)) return string.Empty;
        if (!IsEnglish) return source;
        if (English.TryGetValue(source, out var translated)) return translated;

        foreach (var (pattern, template) in FormatPatterns.Value)
        {
            var match = pattern.Match(source);
            if (!match.Success) continue;

            var args = new object[match.Groups.Count - 1];
            for (var i = 0; i < args.Length; i++) args[i] = match.Groups[i + 1].Value;

            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        return source;
    }

    private static List<(Regex Pattern, string Template)> BuildFormatPatterns()
    {
        var patterns = new List<(Regex, string)>();

        foreach (var pair in English)
        {
            if (!pair.Key.Contains("{0}", StringComparison.Ordinal)) continue;

            var parts = Regex.Split(pair.Key, @"\{\d+\}");
            var builder = new StringBuilder("^");
            for (var i = 0; i < parts.Length; i++)
            {
                builder.Append(Regex.Escape(parts[i]));
                if (i < parts.Length - 1) builder.Append("(.*?)");
            }

            builder.Append('$');
            patterns.Add((
                new Regex(builder.ToString(), RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant),
                pair.Value));
        }

        return patterns;
    }

    /// <summary>带占位符的翻译，例如 <c>F("共 {0} 个别名", 3)</c>。</summary>
    public string F(string format, params object[] args)
    {
        var translated = T(format);
        if (args.Length == 0) return translated;

        try
        {
            return string.Format(translated, args);
        }
        catch (FormatException)
        {
            return translated;
        }
    }

    public void SetLanguage(string? language)
    {
        var normalized = string.Equals(language, AppSettings.LanguageEnUS, StringComparison.OrdinalIgnoreCase)
            ? AppSettings.LanguageEnUS
            : AppSettings.LanguageZhCN;

        if (_language == normalized) return;

        _language = normalized;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}
