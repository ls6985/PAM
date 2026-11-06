using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace PAM.App.Controls;

/// <summary>
/// 单行文本控件：空间不足时从中间省略，保留路径开头和文件名末尾。
/// 相比 TextBlock 的尾部省略，更适合显示长文件路径。
/// </summary>
public sealed class MiddleEllipsisTextBlock : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(MiddleEllipsisTextBlock),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(MiddleEllipsisTextBlock),
        new FrameworkPropertyMetadata(
            SystemColors.ControlTextBrush,
            FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(
        typeof(MiddleEllipsisTextBlock),
        new FrameworkPropertyMetadata(
            SystemFonts.MessageFontFamily,
            FrameworkPropertyMetadataOptions.Inherits |
            FrameworkPropertyMetadataOptions.AffectsMeasure |
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(
        typeof(MiddleEllipsisTextBlock),
        new FrameworkPropertyMetadata(
            SystemFonts.MessageFontSize,
            FrameworkPropertyMetadataOptions.Inherits |
            FrameworkPropertyMetadataOptions.AffectsMeasure |
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontStyleProperty = TextElement.FontStyleProperty.AddOwner(
        typeof(MiddleEllipsisTextBlock),
        new FrameworkPropertyMetadata(
            FontStyles.Normal,
            FrameworkPropertyMetadataOptions.Inherits |
            FrameworkPropertyMetadataOptions.AffectsMeasure |
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty.AddOwner(
        typeof(MiddleEllipsisTextBlock),
        new FrameworkPropertyMetadata(
            FontWeights.Normal,
            FrameworkPropertyMetadataOptions.Inherits |
            FrameworkPropertyMetadataOptions.AffectsMeasure |
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontStretchProperty = TextElement.FontStretchProperty.AddOwner(
        typeof(MiddleEllipsisTextBlock),
        new FrameworkPropertyMetadata(
            FontStretches.Normal,
            FrameworkPropertyMetadataOptions.Inherits |
            FrameworkPropertyMetadataOptions.AffectsMeasure |
            FrameworkPropertyMetadataOptions.AffectsRender));

    public MiddleEllipsisTextBlock()
    {
        Focusable = false;
        SnapsToDevicePixels = true;
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontStyle FontStyle
    {
        get => (FontStyle)GetValue(FontStyleProperty);
        set => SetValue(FontStyleProperty, value);
    }

    public FontWeight FontWeight
    {
        get => (FontWeight)GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public FontStretch FontStretch
    {
        get => (FontStretch)GetValue(FontStretchProperty);
        set => SetValue(FontStretchProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var formatted = CreateFormattedText(Text);

        var width = formatted.WidthIncludingTrailingWhitespace;
        if (!double.IsInfinity(availableSize.Width))
        {
            width = Math.Min(width, Math.Max(0, availableSize.Width));
        }

        return new Size(width, formatted.Height);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var text = Text;
        if (string.IsNullOrEmpty(text) || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var full = CreateFormattedText(text);
        var displayText = full.WidthIncludingTrailingWhitespace <= ActualWidth
            ? text
            : ElideToWidth(text, ActualWidth);

        if (displayText.Length == 0)
        {
            return;
        }

        var formatted = ReferenceEquals(displayText, text) || displayText == text
            ? full
            : CreateFormattedText(displayText);

        var y = Math.Max(0, (ActualHeight - formatted.Height) / 2);

        drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
        drawingContext.DrawText(formatted, new Point(0, y));
        drawingContext.Pop();
    }

    private FormattedText CreateFormattedText(string text)
    {
        return new FormattedText(
            text ?? string.Empty,
            CultureInfo.CurrentUICulture,
            FlowDirection,
            new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
            Math.Max(1, FontSize),
            Foreground,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    private string ElideToWidth(string text, double maxWidth)
    {
        var ellipsis = CreateFormattedText("…");
        if (ellipsis.WidthIncludingTrailingWhitespace > maxWidth)
        {
            return string.Empty;
        }

        var low = 0;
        var high = text.Length;
        var best = 0;

        while (low <= high)
        {
            var keep = low + (high - low) / 2;
            var candidate = BuildElided(text, keep);
            var width = CreateFormattedText(candidate).WidthIncludingTrailingWhitespace;

            if (width <= maxWidth)
            {
                best = keep;
                low = keep + 1;
            }
            else
            {
                high = keep - 1;
            }
        }

        return BuildElided(text, best);
    }

    private static string BuildElided(string text, int keep)
    {
        if (text.Length == 0)
        {
            return string.Empty;
        }

        if (keep <= 0)
        {
            return "…";
        }

        if (keep >= text.Length)
        {
            return text;
        }

        var prefixLength = Math.Max(1, (int)Math.Round(keep * 0.35, MidpointRounding.AwayFromZero));

        if (prefixLength >= keep)
        {
            prefixLength = keep - 1;
        }

        var suffixLength = keep - prefixLength;

        // 不要从代理项（surrogate pair）中间切断文本。
        while (prefixLength > 0 && char.IsLowSurrogate(text[prefixLength]))
        {
            prefixLength--;
        }

        var suffixStart = text.Length - suffixLength;
        while (suffixLength > 0 && char.IsLowSurrogate(text[suffixStart]))
        {
            suffixLength--;
            suffixStart++;
        }

        if (prefixLength <= 0 && suffixLength <= 0)
        {
            return "…";
        }

        if (prefixLength <= 0)
        {
            return string.Concat("…", text.AsSpan(text.Length - suffixLength));
        }

        if (suffixLength <= 0)
        {
            return string.Concat(text.AsSpan(0, prefixLength), "…");
        }

        return string.Concat(
            text.AsSpan(0, prefixLength),
            "…",
            text.AsSpan(text.Length - suffixLength));
    }
}
