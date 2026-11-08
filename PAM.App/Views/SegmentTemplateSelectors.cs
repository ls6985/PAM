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
/// ComboBox 收起时显示短模板、展开时显示完整模板。
/// WPF 把模板应用在项内容宿主 <see cref="ContentPresenter" /> 上而非 <see cref="ComboBoxItem" />，
/// 故向上遍历视觉树：下拉项位于 <see cref="ComboBoxItem" /> 内（Popup 中），选中框不在，据此区分。
/// 仅当容器本身是 <see cref="Visual" /> 才遍历，避免 <see cref="VisualTreeHelper.GetParent" /> 对非 Visual 容器抛异常。
/// </summary>
public sealed class SegmentChoiceTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ShortTemplate { get; set; }

    public DataTemplate? FullTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        => TemplateSelectorHelper.IsInsideComboBoxItem(container) ? FullTemplate : ShortTemplate;
}

/// <summary>
/// 判断模板宿主是否位于展开的 <see cref="ComboBoxItem" /> 内（即在下拉列表中）。
/// 只对 <see cref="Visual" /> 向上遍历；遇到非 Visual 容器直接判定为「不在下拉列表中」（即收起态）。
/// </summary>
internal static class TemplateSelectorHelper
{
    public static bool IsInsideComboBoxItem(DependencyObject? container)
    {
        var current = container as Visual;
        while (current is not null)
        {
            if (current is ComboBoxItem)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current) as Visual;
        }

        return false;
    }
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

/// <summary>
/// 颜色下拉模板选择：自定义项显示「自定义…」，其余显示色块 + 色号。
/// 收起（选中框）与展开（下拉项）使用同一套模板，与「内容」下拉的 ComboBox 实现方式保持一致。
/// </summary>
public sealed class ColorChoiceTemplateSelector : DataTemplateSelector
{
    public DataTemplate? SwatchTemplate { get; set; }

    public DataTemplate? CustomTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        => item is ColorOption { IsCustom: true } ? CustomTemplate : SwatchTemplate;
}

/// <summary>颜色下拉选中「自定义…」时显示右侧色号输入框，否则隐藏。</summary>
public sealed class ColorCustomToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ColorOption { IsCustom: true } ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
