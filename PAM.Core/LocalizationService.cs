namespace PAM.Core;

/// <summary>
/// 轻量本地化服务：以显式 <see cref="LocKey"/> 为资源键，按当前语言返回中文或英文。
/// 语言表（key → {zh, en}）集中在 <c>LocTable</c>；原先「中文键 + 正则反查」的机制已移除。
/// 代码侧通过 <c>Loc.T / Loc.F</c> 调用；XAML 侧通过 <c>LocExtension</c> 绑定键。
/// 仍持有中文原文的调用（动态拼装、XAML 桥接）走 <see cref="T(string)"/> 的精确 zh → en 查表兜底。
/// </summary>
public sealed class LocalizationService
{
    public static LocalizationService Instance { get; } = new();

    private string _language = AppSettings.LanguageZhCN;

    private LocalizationService() { }

    /// <summary>当前语言（zh-CN / en-US）。</summary>
    public string Language => _language;

    public bool IsEnglish => string.Equals(_language, AppSettings.LanguageEnUS, StringComparison.OrdinalIgnoreCase);

    /// <summary>语言切换后触发，界面遍历器与视图模型据此刷新文本。</summary>
    public event EventHandler? LanguageChanged;

    /// <summary>按当前语言返回指定资源键的文本。</summary>
    public string T(LocKey key)
    {
        return IsEnglish ? LocTable.Data[key].En : LocTable.Data[key].Zh;
    }

    /// <summary>带占位符的翻译，例如 <c>F(LocKey.EditAlias0, 3)</c>。</summary>
    public string F(LocKey key, params object[] args)
    {
        var text = T(key);
        if (args.Length == 0) return text;

        try
        {
            return string.Format(text, args);
        }
        catch (FormatException)
        {
            return text;
        }
    }

    /// <summary>
    /// 中文原文兜底查表：供仍持有中文文本的调用（动态拼装、XAML 桥接）使用。
    /// 仅做精确的 zh → en 查表，不再使用正则反查。未收录的条目原样返回。
    /// </summary>
    public string T(string? source)
    {
        if (string.IsNullOrEmpty(source)) return string.Empty;
        if (!IsEnglish) return source;
        return LocTable.ZhToEn.TryGetValue(source, out var translated) ? translated : source;
    }

    /// <summary>带占位符的中文兜底翻译。</summary>
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
