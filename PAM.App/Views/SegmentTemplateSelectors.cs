using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using PAM.App.ViewModels;
using PAM.Core;

namespace PAM.App.Views;

/// <summary>
/// 让 ComboBox 在「收起」时显示短模板、「展开」时显示完整模板。
/// 注意：WPF 把模板应用在项内容宿主 <see cref="ContentPresenter" /> 上，而非 <see cref="ComboBoxItem" />，
/// 所以不能用「container is ComboBoxItem」判断；正确做法是向上遍历视觉树——
/// 下拉项一定位于 <see cref="ComboBoxItem" /> 内（在 Popup 中），而选中框不在，据此区分两种状态。
/// </summary>
public sealed class SegmentChoiceTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ShortTemplate { get; set; }

    public DataTemplate? FullTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        var current = container;
        while (current is not null)
        {
            if (current is ComboBoxItem)
            {
                return FullTemplate;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return ShortTemplate;
    }
}

/// <summary>颜色下拉同款：收起只显示色块，展开显示色块 + hex。</summary>
public sealed class ColorChoiceTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ShortTemplate { get; set; }

    public DataTemplate? FullTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        var current = container;
        while (current is not null)
        {
            if (current is ComboBoxItem)
            {
                return FullTemplate;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return ShortTemplate;
    }
}

/// <summary>
/// 把颜色下拉的绑定对象（<see cref="ColorOption" /> 或 <see cref="string" /> 形式的 hex）统一转成画刷，
/// 避免选中框在某些绑定路径下拿到非 <see cref="ColorOption" /> 导致色块不显示。
/// </summary>
public sealed class ColorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hex = value switch
        {
            ColorOption c => c.Hex,
            string s => s,
            _ => "#FFFFFF",
        };
        return ColorOption.CreateBrush(hex);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>选中项为「自定义」时显示编辑框，否则隐藏。</summary>
public sealed class CustomSelectedToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ISegmentChoice choice && choice.Kind == PromptSegmentKind.Custom
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
