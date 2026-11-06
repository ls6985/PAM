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
        ["别名管理"] = "Aliases",
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
        ["（空代码）"] = "(empty code)",
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
        ["Enter 确定 · Esc 取消"] = "Enter to confirm · Esc to cancel",
        ["代码预览"] = "Code preview",
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
        ["{0} · 共 {1} 个自定义命令 · 执行策略受限，未启动新的 PowerShell 窗口。"] =
            "{0} · {1} custom commands · Execution policy restricted; no new window was opened.",
        ["{0} · 已启动新的 PowerShell 窗口 · 共 {1} 个自定义命令"] =
            "{0} · Opened a new PowerShell window · {1} custom commands",
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
        ["正在解析 $PROFILE…"] = "Resolving $PROFILE…",
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
        ["无法使用：{0}"] = "Cannot use: {0}",
        ["尚未检测，点击下方“检测冲突”。"] = "Not checked yet. Click \"Check conflicts\" below.",
        ["后台运行"] = "Background",
        ["关闭管理器时保留后台托盘"] = "Keep the tray running when the manager closes",
        ["开启后关闭窗口仍可使用全局热键与托盘菜单；关闭则一并退出后台进程。"] =
            "When on, the hotkey and tray menu still work after closing the window. When off, the background process exits too.",
        ["后台托盘状态"] = "Tray status",
        ["立即启动"] = "Start now",
        ["结束后台运行"] = "Stop background",
        ["设置文件"] = "Settings file",
        ["恢复默认设置"] = "Restore defaults",
        ["所有更改都会立即写入 settings.json。"] = "All changes are written to settings.json immediately.",
        ["设置已保存。"] = "Settings saved.",
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
        ["PowerShell 管理器已在运行，无需重复启动。"] = "PowerShell Manager is already running.",
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
    };

    private string _language = AppSettings.LanguageZhCN;

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
        return English.TryGetValue(source, out var translated) ? translated : source;
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
