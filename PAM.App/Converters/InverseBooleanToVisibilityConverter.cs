using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PAM.App.Converters;

/// <summary>
/// 布尔取反转 Visibility：true → Collapsed，false → Visible。
/// 用于加载中（IsLoading = true）时隐藏列表。
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
