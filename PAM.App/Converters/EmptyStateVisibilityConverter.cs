using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PAM.App.Converters;

/// <summary>
/// 空状态可见性：加载中（IsLoading = true）时始终隐藏，
/// 加载完成且列表为空（IsListEmpty = true）时才显示，避免初始加载时空文字闪现覆盖列表。
/// 参数顺序：[0] = IsLoading，[1] = IsListEmpty。
/// </summary>
public class EmptyStateVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
        {
            return Visibility.Collapsed;
        }

        var isLoading = values[0] is true;
        var isEmpty = values[1] is true;

        return !isLoading && isEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
