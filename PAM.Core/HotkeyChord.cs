namespace PAM.Core;

/// <summary>
/// 全局热键修饰键，数值与 Win32 <c>MOD_*</c> 标志一致，可直接传给 <c>RegisterHotKey</c>。
/// </summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0x0000,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
}

/// <summary>
/// 与 UI 框架无关的全局热键描述：修饰键 + 虚拟键码（VK）。
/// PAM.App 用它做录制与冲突检测，PAM.Tray 用它注册 Win32 热键。
/// </summary>
public sealed class HotkeyChord : IEquatable<HotkeyChord>
{
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;

    private static readonly Dictionary<string, HotkeyModifiers> ModifierAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ctrl"] = HotkeyModifiers.Control,
            ["control"] = HotkeyModifiers.Control,
            ["alt"] = HotkeyModifiers.Alt,
            ["shift"] = HotkeyModifiers.Shift,
            ["win"] = HotkeyModifiers.Win,
            ["windows"] = HotkeyModifiers.Win,
            ["meta"] = HotkeyModifiers.Win,
            ["super"] = HotkeyModifiers.Win,
        };

    private static readonly Dictionary<string, int> NamedKeys = BuildNamedKeys();
    private static readonly Dictionary<int, string> KeyDisplayNames = BuildDisplayNames();

    public HotkeyChord(HotkeyModifiers modifiers, int virtualKey)
    {
        Modifiers = modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt |
                                 HotkeyModifiers.Shift | HotkeyModifiers.Win);
        VirtualKey = virtualKey;
        Display = Format(Modifiers, virtualKey);
    }

    public HotkeyModifiers Modifiers { get; }

    /// <summary>Win32 虚拟键码（VK_*）。</summary>
    public int VirtualKey { get; }

    /// <summary>规范化后的可显示文本，例如 <c>Ctrl+Alt+T</c>。</summary>
    public string Display { get; }

    /// <summary>解析失败返回 null；成功时返回规范化后的组合。</summary>
    public static HotkeyChord? Parse(string? text)
        => TryParse(text, out var chord, out _) ? chord : null;

    public static bool TryParse(string? text, out HotkeyChord? chord, out string error)
    {
        chord = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "热键不能为空。";
            return false;
        }

        var tokens = text.Replace('＋', '+').Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
        {
            error = "热键必须包含至少一个修饰键和一个普通按键。";
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        var keyToken = string.Empty;

        foreach (var token in tokens)
        {
            if (ModifierAliases.TryGetValue(token, out var modifier))
            {
                modifiers |= modifier;
                continue;
            }

            if (keyToken.Length > 0)
            {
                error = "热键只能包含一个普通按键。";
                return false;
            }

            keyToken = token;
        }

        if (modifiers == HotkeyModifiers.None)
        {
            error = "热键必须包含 Ctrl、Alt、Shift 或 Win 中的至少一个修饰键。";
            return false;
        }

        if (keyToken.Length == 0)
        {
            error = "热键缺少普通按键。";
            return false;
        }

        if (!NamedKeys.TryGetValue(keyToken, out var virtualKey))
        {
            error = $"无法识别按键“{keyToken}”。";
            return false;
        }

        var candidate = new HotkeyChord(modifiers, virtualKey);
        if (!IsValid(candidate, out error))
        {
            return false;
        }

        chord = candidate;
        return true;
    }

    /// <summary>把虚拟键码与修饰键格式化为 <c>Ctrl+Alt+T</c> 这样的文本。</summary>
    public static string Format(HotkeyModifiers modifiers, int virtualKey)
    {
        var parts = new List<string>(5);

        if (modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(KeyName(virtualKey));

        return string.Join("+", parts);
    }

    /// <summary>返回虚拟键码的可读名称；无法识别时显示 VK 十六进制。</summary>
    public static string KeyName(int virtualKey)
    {
        if (KeyDisplayNames.TryGetValue(virtualKey, out var name)) return name;
        return $"VK(0x{virtualKey:X2})";
    }

    public static bool IsValid(HotkeyChord chord, out string error)
    {
        error = string.Empty;

        if (chord.Modifiers == HotkeyModifiers.None)
        {
            error = "热键必须包含 Ctrl、Alt、Shift 或 Win 中的至少一个修饰键。";
            return false;
        }

        if (chord.VirtualKey is VkShift or VkControl or VkMenu or VkLWin or VkRWin)
        {
            error = "不能把修饰键本身当作热键按键。";
            return false;
        }

        if (chord.VirtualKey <= 0 || chord.VirtualKey > 0xFE)
        {
            error = "无效的按键。";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Windows 系统独占的组合。返回中文原因；没有冲突时返回 null。
    /// 这些组合即使 RegisterHotKey 侥幸成功，也会被系统优先截获。
    /// </summary>
    public static string? GetSystemReservation(HotkeyChord chord)
    {
        var m = chord.Modifiers;
        var k = chord.VirtualKey;
        var winOnly = m == HotkeyModifiers.Win;
        var altOnly = m == HotkeyModifiers.Alt;
        var ctrlOnly = m == HotkeyModifiers.Control;

        if (m == (HotkeyModifiers.Control | HotkeyModifiers.Alt) && k == 0x2E)
            return "Ctrl+Alt+Del 由 Windows 安全界面独占，任何应用都无法接管。";
        if (m == (HotkeyModifiers.Control | HotkeyModifiers.Shift) && k == 0x1B)
            return "Ctrl+Shift+Esc 用于打开任务管理器。";
        if (ctrlOnly && k == 0x1B)
            return "Ctrl+Esc 用于打开开始菜单。";
        if (altOnly && k == 0x09)
            return "Alt+Tab 用于切换窗口。";
        if (m == (HotkeyModifiers.Alt | HotkeyModifiers.Shift) && k == 0x09)
            return "Alt+Shift+Tab 用于反向切换窗口。";
        if (altOnly && k == 0x73)
            return "Alt+F4 用于关闭当前窗口。";
        if (altOnly && k == 0x1B)
            return "Alt+Esc 用于按窗口顺序切换。";

        if (!m.HasFlag(HotkeyModifiers.Win)) return null;
        if (winOnly)
        {
            return k switch
            {
                0x4C => "Win+L 用于锁定计算机。",
                0x0D => "Win+Enter 用于启动 Windows 讲述人。",
                0x09 => "Win+Tab 用于打开任务视图。",
                0x44 => "Win+D 用于显示桌面。",
                0x45 => "Win+E 用于打开文件资源管理器。",
                0x52 => "Win+R 用于打开“运行”对话框。",
                0x49 => "Win+I 用于打开 Windows 设置。",
                0x53 or 0x51 => "Win+S / Win+Q 用于 Windows 搜索。",
                0x56 => "Win+V 用于打开剪贴板历史。",
                0x4B => "Win+K 用于打开“连接”面板。",
                0x50 => "Win+P 用于切换投影模式。",
                0x41 => "Win+A 用于打开操作中心。",
                0x47 => "Win+G 用于打开 Xbox Game Bar。",
                0x55 => "Win+U 用于打开轻松使用设置。",
                0x58 => "Win+X 用于打开快速链接菜单。",
                0x54 => "Win+T 用于浏览任务栏程序。",
                0x2C => "Win+PrintScreen 用于系统截图并保存。",
                0xBB or 0xBD => "Win++ / Win+- 用于打开或关闭放大镜。",
                _ => null,
            };
        }

        if (m == (HotkeyModifiers.Win | HotkeyModifiers.Shift) && k == 0x53)
            return "Win+Shift+S 由 Windows 截图工具占用。";
        if (m == (HotkeyModifiers.Win | HotkeyModifiers.Control) && k is 0x44 or 0x73 or 0x25 or 0x27)
            return "该组合用于 Windows 虚拟桌面管理。";
        if (m == (HotkeyModifiers.Win | HotkeyModifiers.Alt) && k == 0x52)
            return "Win+Alt+R 由 Xbox Game Bar 录制功能占用。";

        return null;
    }

    /// <summary>
    /// 常见应用级通用快捷键。注册成全局热键会拦截所有软件里的同一操作，属于"软冲突"。
    /// </summary>
    public static string? GetCommonAppShortcutWarning(HotkeyChord chord)
    {
        var m = chord.Modifiers;
        var k = chord.VirtualKey;

        if (m == HotkeyModifiers.Control)
        {
            return k switch
            {
                0x53 => "Ctrl+S 是几乎所有软件的“保存”，注册为全局热键后会被拦截。",
                0x4E => "Ctrl+N 是常见的“新建”，注册为全局热键后会被拦截。",
                0x4F => "Ctrl+O 是常见的“打开”，注册为全局热键后会被拦截。",
                0x43 => "Ctrl+C 是系统级“复制”，注册为全局热键后会被拦截。",
                0x56 => "Ctrl+V 是系统级“粘贴”，注册为全局热键后会被拦截。",
                0x58 => "Ctrl+X 是系统级“剪切”，注册为全局热键后会被拦截。",
                0x41 => "Ctrl+A 是常见的“全选”，注册为全局热键后会被拦截。",
                0x5A => "Ctrl+Z 是常见的“撤销”，注册为全局热键后会被拦截。",
                0x50 => "Ctrl+P 是常见的“打印”，注册为全局热键后会被拦截。",
                0x46 => "Ctrl+F 是常见的“查找”，注册为全局热键后会被拦截。",
                0x57 => "Ctrl+W 是常见的“关闭标签页”，注册为全局热键后会被拦截。",
                0x54 => "Ctrl+T 是常见的“新建标签页”，注册为全局热键后会被拦截。",
                _ => null,
            };
        }

        if (m == (HotkeyModifiers.Control | HotkeyModifiers.Shift) && k == 0x54)
            return "Ctrl+Shift+T 是浏览器“恢复关闭的标签页”，注册为全局热键后会被拦截。";

        return null;
    }

    /// <summary>常见第三方软件热键提示，用于给"已被占用"补充可能的占用者。</summary>
    public static string? GetSoftwareHint(HotkeyChord chord)
    {
        return chord.Display switch
        {
            "Ctrl+Alt+A" => "QQ / 微信等截图工具常使用 Ctrl+Alt+A。",
            "Ctrl+Alt+T" => "部分终端工具或 Linux 子系统使用 Ctrl+Alt+T。",
            "Ctrl+Shift+A" => "截图工具与部分 IDE 常使用 Ctrl+Shift+A。",
            "Ctrl+Alt+S" => "截图或输入法工具常使用 Ctrl+Alt+S。",
            "Ctrl+Space" => "中文输入法的中英文切换常使用 Ctrl+Space。",
            "Alt+Space" => "Windows 窗口系统菜单常使用 Alt+Space。",
            _ => null,
        };
    }

    public bool Equals(HotkeyChord? other)
        => other is not null && Modifiers == other.Modifiers && VirtualKey == other.VirtualKey;

    public override bool Equals(object? obj) => Equals(obj as HotkeyChord);

    public override int GetHashCode() => HashCode.Combine((uint)Modifiers, VirtualKey);

    public override string ToString() => Display;

    private static Dictionary<string, int> BuildNamedKeys()
    {
        var keys = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var c = 'A'; c <= 'Z'; c++) keys[c.ToString()] = c;
        for (var c = '0'; c <= '9'; c++) keys[c.ToString()] = c;
        for (var i = 1; i <= 24; i++) keys[$"F{i}"] = 0x6F + i;

        Add(keys, "Space", 0x20);
        Add(keys, "Enter", 0x0D);
        Add(keys, "Return", 0x0D);
        Add(keys, "Tab", 0x09);
        Add(keys, "Esc", 0x1B);
        Add(keys, "Escape", 0x1B);
        Add(keys, "Backspace", 0x08);
        Add(keys, "Delete", 0x2E);
        Add(keys, "Del", 0x2E);
        Add(keys, "Insert", 0x2D);
        Add(keys, "Home", 0x24);
        Add(keys, "End", 0x23);
        Add(keys, "PageUp", 0x21);
        Add(keys, "PageDown", 0x22);
        Add(keys, "Up", 0x26);
        Add(keys, "Down", 0x28);
        Add(keys, "Left", 0x25);
        Add(keys, "Right", 0x27);
        Add(keys, "PrintScreen", 0x2C);
        Add(keys, "Pause", 0x13);
        Add(keys, "CapsLock", 0x14);
        Add(keys, "ScrollLock", 0x91);
        Add(keys, "NumLock", 0x90);
        Add(keys, "Apps", 0x5D);

        for (var i = 0; i <= 9; i++) Add(keys, $"NumPad{i}", 0x60 + i);
        Add(keys, "NumPadMultiply", 0x6A);
        Add(keys, "NumPadAdd", 0x6B);
        Add(keys, "NumPadSubtract", 0x6D);
        Add(keys, "NumPadDecimal", 0x6E);
        Add(keys, "NumPadDivide", 0x6F);

        Add(keys, "OemSemicolon", 0xBA);
        Add(keys, "OemPlus", 0xBB);
        Add(keys, "OemComma", 0xBC);
        Add(keys, "OemMinus", 0xBD);
        Add(keys, "OemPeriod", 0xBE);
        Add(keys, "OemQuestion", 0xBF);
        Add(keys, "OemTilde", 0xC0);
        Add(keys, "OemOpenBrackets", 0xDB);
        Add(keys, "OemPipe", 0xDC);
        Add(keys, "OemCloseBrackets", 0xDD);
        Add(keys, "OemQuotes", 0xDE);
        Add(keys, "OemBackslash", 0xE2);

        // 修饰键本身也收录，便于解析后由 IsValid 给出明确错误。
        Add(keys, "Shift", 0x10);
        Add(keys, "Ctrl", 0x11);
        Add(keys, "Alt", 0x12);
        Add(keys, "LWin", 0x5B);
        Add(keys, "RWin", 0x5C);

        return keys;
    }

    private static Dictionary<int, string> BuildDisplayNames()
    {
        var names = new Dictionary<int, string>();

        foreach (var pair in NamedKeys)
        {
            // 同一 VK 有多个别名时保留语义最清晰的一个（先写入者优先）。
            if (!names.ContainsKey(pair.Value)) names[pair.Value] = pair.Key;
        }

        // 用规范名称覆盖别名。
        names[0x20] = "Space";
        names[0x0D] = "Enter";
        names[0x09] = "Tab";
        names[0x1B] = "Esc";
        names[0x08] = "Backspace";
        names[0x2E] = "Delete";
        names[0x2D] = "Insert";
        names[0x2C] = "PrintScreen";
        names[0x10] = "Shift";
        names[0x11] = "Ctrl";
        names[0x12] = "Alt";
        names[0x5B] = "LWin";
        names[0x5C] = "RWin";

        for (var c = 'A'; c <= 'Z'; c++) names[c] = c.ToString();
        for (var c = '0'; c <= '9'; c++) names[c] = c.ToString();
        for (var i = 1; i <= 24; i++) names[0x6F + i] = $"F{i}";

        return names;
    }

    private static void Add(Dictionary<string, int> map, string name, int vk) => map[name] = vk;
}
