using System.Windows.Media;

namespace PAM.App.ViewModels;

/// <summary>预览中的一段带色文字；没有指定颜色时用终端风格的浅灰。</summary>
public sealed class PromptPreviewItem
{
    public PromptPreviewItem(string text, string? color)
    {
        Text = text;
        Color = color;
        Brush = ColorOption.CreateBrush(color ?? "#D4D4D4");
    }

    public string Text { get; }

    public string? Color { get; }

    public SolidColorBrush Brush { get; }
}
