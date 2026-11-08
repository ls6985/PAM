using System.Collections.Generic;
using System.Windows.Media;
using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>颜色下拉的一项：hex 值 + 可直接绑定到 Rectangle.Fill 的画刷。</summary>
public sealed class ColorOption
{
    /// <summary>共享色板：六段编辑行与预览都用同一份，避免每行各建一份集合。</summary>
    public static IReadOnlyList<ColorOption> Palette { get; } =
        PromptPalette.Colors.Select(static hex => new ColorOption(hex)).ToList();

    /// <summary>下拉首项「自定义…」：选中后由右侧输入框自由填色号。</summary>
    public static ColorOption Custom { get; } = new ColorOption(string.Empty) { IsCustom = true };

    /// <summary>带「自定义」项的完整下拉列表（每个片段共享同一份）。</summary>
    public static IReadOnlyList<ColorOption> ChoicesWithCustom { get; } =
        new List<ColorOption> { Custom }.Concat(Palette).ToList();

    public ColorOption(string hex)
    {
        Hex = hex;
        Brush = CreateBrush(hex);
    }

    public string Hex { get; }

    public SolidColorBrush Brush { get; }

    /// <summary>是否为「自定义…」项；自定义项 Hex 为空、Brush 取中性灰。</summary>
    public bool IsCustom { get; set; }

    public static SolidColorBrush CreateBrush(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex)!;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        catch
        {
            var brush = new SolidColorBrush(Colors.White);
            brush.Freeze();
            return brush;
        }
    }
}
