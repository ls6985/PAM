using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>
/// 提示符里的一个可选项片段：内容来自下拉（变量或固定符号），颜色单独选。
/// 任意属性变化都会通知外层重组 format；<see cref="ToSnippet" /> 把内容包成一段 Starship format。
/// </summary>
public sealed class PromptSegment : INotifyPropertyChanged
{
    // 固定符号清单：与 VariableOptions 合并成下拉项。语言无关，无需刷新。
    public static IReadOnlyList<ISegmentChoice> LiteralChoices { get; } =
    [
        new SymbolOption("@", "固定符号 @", "@"),
        new SymbolOption(":", "固定符号 :", ":"),
        new SymbolOption("~", "固定符号 ~", "~"),
        new SymbolOption("$", "固定符号 $", "$"),
        new SymbolOption(" ", "空格", "空格"),
    ];

    private readonly IReadOnlyList<ISegmentChoice> _choices;
    private ISegmentChoice _selectedOption;
    private string _color;
    private string _hexInput;
    private string _customText = string.Empty;
    private ColorOption? _selectedColorOption;

    public PromptSegment(IReadOnlyList<ISegmentChoice> choices, ISegmentChoice initial, string color)
    {
        _choices = choices;
        _selectedOption = initial;
        _color = color;
        _hexInput = GetHexDigitsOrEmpty(color);
    }

    /// <summary>选「自定义」时自由输入的文字；原样作为片段内容（可写任意文字或 $变量）。</summary>
    public string CustomText
    {
        get => _customText;
        set
        {
            if (!string.Equals(_customText, value ?? string.Empty, StringComparison.Ordinal))
            {
                _customText = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>下拉项来源（变量 + 固定符号），每个片段共享同一份。</summary>
    public IReadOnlyList<ISegmentChoice> Choices => _choices;

    public ISegmentChoice SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (value is not null && !ReferenceEquals(_selectedOption, value))
            {
                _selectedOption = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>下拉项来源：自定义项 + 12 色预设，每个片段共享同一份。</summary>
    public IReadOnlyList<ColorOption> ColorChoices => ColorOption.ChoicesWithCustom;

    public string Color
    {
        get => _color;
        set
        {
            if (value is not null && !string.Equals(_color, value, StringComparison.Ordinal))
            {
                _color = value;
                _hexInput = GetHexDigitsOrEmpty(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(HexInput));
            }
        }
    }

    /// <summary>
    /// 当前选中的颜色项（下拉的 SelectedItem）。自定义项被选中时不改动 <see cref="Color" />，
    /// 只让右侧输入框可见；选中预设项则把 <see cref="Color" /> 设为该色号。
    /// </summary>
    public ColorOption? SelectedColorOption
    {
        get
        {
            _selectedColorOption ??= ColorOption.Palette
                .FirstOrDefault(c => string.Equals(c.Hex, _color, StringComparison.OrdinalIgnoreCase))
                ?? ColorOption.Custom;
            return _selectedColorOption;
        }
        set
        {
            if (value is null || ReferenceEquals(_selectedColorOption, value))
            {
                return;
            }

            _selectedColorOption = value;
            OnPropertyChanged();
            if (!value.IsCustom)
            {
                Color = value.Hex;
            }
        }
    }

    /// <summary>
    /// 颜色编辑框用的色号（<c>#</c> 后面的 6 位，不含 <c>#</c>）。
    /// 只有凑满 6 位合法十六进制才写回 <see cref="Color" />，半成品只留在输入框里，
    /// 避免每敲一个字符就把 format 改成无效颜色。非标准色号（如命名色）显示为空。
    /// </summary>
    public string HexInput
    {
        get => _hexInput;
        set
        {
            var digits = SanitizeHexDigits(value);
            if (string.Equals(_hexInput, digits, StringComparison.Ordinal))
            {
                return;
            }

            _hexInput = digits;
            OnPropertyChanged();

            if (digits.Length == 6 && !string.Equals(_color, "#" + digits, StringComparison.OrdinalIgnoreCase))
            {
                _color = "#" + digits;
                OnPropertyChanged(nameof(Color));
                OnPropertyChanged(nameof(SelectedColorOption));
            }
        }
    }

    /// <summary>键入过程中的宽松清洗：只留十六进制字符、大写、最多 6 位。</summary>
    private static string SanitizeHexDigits(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(6);
        foreach (var ch in value)
        {
            if (builder.Length == 6)
            {
                break;
            }

            if (char.IsAsciiHexDigit(ch))
            {
                builder.Append(char.ToUpperInvariant(ch));
            }
        }

        return builder.ToString();
    }

    /// <summary>把已有的颜色字符串取成 6 位色号；不是 <c>#RRGGBB</c> 时返回空串（命名色不显示半截）。</summary>
    private static string GetHexDigitsOrEmpty(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (text.StartsWith('#'))
        {
            text = text[1..];
        }

        return text.Length == 6 && text.All(char.IsAsciiHexDigit)
            ? text.ToUpperInvariant()
            : string.Empty;
    }

    /// <summary>包成一段 Starship format：自定义原样、变量原样、固定文字做反转义，再外加 fg 颜色；自定义为空时不产出片段。</summary>
    public string ToSnippet()
    {
        var inner = _selectedOption.Kind == PromptSegmentKind.Custom
            ? _customText
            : _selectedOption.Kind == PromptSegmentKind.Variable
                ? _selectedOption.Value
                : EscapeLiteral(_selectedOption.Value);
        if (string.IsNullOrEmpty(inner))
        {
            return string.Empty;
        }

        return $"[{inner}](fg:{_color})";
    }

    private static string EscapeLiteral(string text)
        => text
            .Replace("\\", @"\\", StringComparison.Ordinal)
            .Replace("[", @"\[", StringComparison.Ordinal)
            .Replace("]", @"\]", StringComparison.Ordinal)
            .Replace("$", @"\$", StringComparison.Ordinal);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
