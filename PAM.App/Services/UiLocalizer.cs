using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using PAM.Core;

namespace PAM.App;

/// <summary>
/// 界面文本遍历器：在元素 Loaded 时把 XAML 里写死的中文字符串替换为当前语言，
/// 并在语言切换时重新遍历已打开的窗口。
/// 采用"原文作为资源键"的方式，无需把每个 XAML 文本改成资源引用；
/// 绑定（Binding）驱动的动态文本由各 ViewModel 自行翻译。
/// </summary>
public static class UiLocalizer
{
    private static readonly ConditionalWeakTable<DependencyObject, Dictionary<DependencyProperty, string>> Originals = new();
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        EventManager.RegisterClassHandler(
            typeof(FrameworkElement),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnElementLoaded));

        LocalizationService.Instance.LanguageChanged += (_, _) => RefreshAll();
    }

    /// <summary>语言切换后刷新所有窗口里的静态文本。</summary>
    public static void RefreshAll()
    {
        var application = Application.Current;
        if (application is null) return;

        if (!application.Dispatcher.CheckAccess())
        {
            application.Dispatcher.BeginInvoke(new Action(RefreshAll));
            return;
        }

        foreach (Window window in application.Windows)
        {
            Walk(window, new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance));
        }
    }

    /// <summary>
    /// 供代码动态设置文本：记住中文原文，当前语言下写入译文。
    /// 语言切换后遍历器会按记住的原文重新翻译。
    /// </summary>
    public static void SetTranslated(DependencyObject element, DependencyProperty property, string source)
    {
        var originals = Originals.GetValue(element, static _ => new Dictionary<DependencyProperty, string>());
        originals[property] = source;
        element.SetValue(property, Loc.T(source));
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject element)
        {
            Walk(element, new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance));
        }
    }

    private static void Walk(DependencyObject element, HashSet<DependencyObject> visited)
    {
        if (!visited.Add(element)) return;

        Translate(element);

        foreach (var child in GetChildren(element))
        {
            Walk(child, visited);
        }
    }

    private static void Translate(DependencyObject element)
    {
        if (element is TextBlock textBlock)
        {
            TranslateProperty(textBlock, TextBlock.TextProperty);
        }

        if (element is ContentControl contentControl)
        {
            TranslateProperty(contentControl, ContentControl.ContentProperty);
        }

        if (element is HeaderedContentControl headeredContent)
        {
            TranslateProperty(headeredContent, HeaderedContentControl.HeaderProperty);
        }

        if (element is HeaderedItemsControl headeredItems)
        {
            TranslateProperty(headeredItems, HeaderedItemsControl.HeaderProperty);
        }

        if (element is FrameworkElement frameworkElement)
        {
            TranslateProperty(frameworkElement, FrameworkElement.ToolTipProperty);
            TranslateProperty(frameworkElement, AutomationProperties.NameProperty);
            TranslateByName(frameworkElement, "Header");
            TranslateByName(frameworkElement, "Title");
            TranslateByName(frameworkElement, "PlaceholderText");
        }

        if (element is Window window)
        {
            TranslateProperty(window, Window.TitleProperty);
        }
    }

    private static void TranslateByName(FrameworkElement element, string propertyName)
    {
        var field = element.GetType().GetField(
            propertyName + "Property",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        if (field?.GetValue(null) is DependencyProperty property)
        {
            TranslateProperty(element, property);
        }
    }

    private static void TranslateProperty(DependencyObject element, DependencyProperty property)
    {
        try
        {
            // 绑定驱动的内容由数据源负责翻译，遍历器不能覆盖它。
            if (BindingOperations.IsDataBound(element, property)) return;
            if (element.GetValue(property) is not string text || text.Length == 0) return;

            var originals = Originals.GetValue(element, static _ => new Dictionary<DependencyProperty, string>());
            if (!originals.TryGetValue(property, out var source))
            {
                source = text;
                originals[property] = text;
            }

            var translated = Loc.T(source);
            if (!string.Equals(translated, text, StringComparison.Ordinal))
            {
                element.SetValue(property, translated);
            }
        }
        catch (InvalidOperationException)
        {
            // 极少数第三方控件的同名属性可能是只读 DP，跳过即可，不能让本地化拖垮界面。
        }
    }

    private static IEnumerable<DependencyObject> GetChildren(DependencyObject element)
    {
        var children = new List<DependencyObject>();

        if (element is Visual)
        {
            try
            {
                var count = VisualTreeHelper.GetChildrenCount(element);
                for (var i = 0; i < count; i++)
                {
                    if (VisualTreeHelper.GetChild(element, i) is DependencyObject child) children.Add(child);
                }
            }
            catch
            {
                // 极少数自定义元素可能拒绝遍历，忽略即可。
            }
        }

        // 未选中页签的内容可能只存在于逻辑树里，两个方向都走一遍。
        try
        {
            foreach (var child in LogicalTreeHelper.GetChildren(element))
            {
                if (child is DependencyObject dependencyObject && !children.Contains(dependencyObject))
                {
                    children.Add(dependencyObject);
                }
            }
        }
        catch
        {
            // 非逻辑树元素（例如纯 Visual）没有逻辑子级。
        }

        return children;
    }
}
