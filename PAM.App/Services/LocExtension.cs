using System.Collections.Generic;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using PAM.Core;

namespace PAM.App;

/// <summary>
/// XAML 本地化扩展：<c>{loc:Loc KeyName}</c> 按当前语言解析资源键的文本，并在语言切换时自动刷新。
/// 通过返回一个绑定到 <see cref="LocalizedText.Text"/> 的 <see cref="Binding"/> 实现动态更新，
/// 因此无需在 ViewModel 里为静态文本写一堆桥接属性。
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension() { }

    /// <summary>
    /// 单一 object 参数：能解析为 <see cref="LocKey"/> 的按枚举键处理（如 <c>SaveApply</c>），
    /// 其余按中文原文处理（如 <c>美化</c>）。两种用法共用一个构造，避免 WPF 在多个单参构造间产生歧义。
    /// </summary>
    public LocExtension(object value)
    {
        if (value is LocKey key)
        {
            Key = key;
        }
        else if (value is string text && Enum.TryParse<LocKey>(text, ignoreCase: true, out var parsed))
        {
            Key = parsed;
        }
        else
        {
            Source = value?.ToString();
        }
    }

    public LocKey Key { get; set; }

    public string? Source { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var localized = new LocalizedText { Key = Key, Source = Source };

        if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget target
            && target.TargetObject is FrameworkElement element)
        {
            localized.Attach(element);
        }

        var binding = new Binding(nameof(LocalizedText.Text))
        {
            Source = localized,
            Mode = BindingMode.OneWay,
        };

        return binding.ProvideValue(serviceProvider);
    }
}

/// <summary>
/// 绑定到某个 <see cref="LocKey"/> 的本地化文本源；键变化或语言切换时刷新 <see cref="Text"/>。
/// 跟随所属元素卸载而退订，避免语言切换事件泄漏。
/// </summary>
internal sealed class LocalizedText : DependencyObject
{
    public static readonly DependencyProperty KeyProperty = DependencyProperty.Register(
        nameof(Key),
        typeof(LocKey),
        typeof(LocalizedText),
        new PropertyMetadata(default(LocKey), OnKeyChanged));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(LocalizedText),
        new PropertyMetadata(string.Empty));

    public LocKey Key
    {
        get => (LocKey)GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    /// <summary>以中文原文为资源键时的源串；为空则回退到 <see cref="Key"/>。</summary>
    public string? Source
    {
        get => (string?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(string),
        typeof(LocalizedText),
        new PropertyMetadata(string.Empty, OnSourceChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        private set => SetValue(TextProperty, value);
    }

    public LocalizedText()
    {
        // 构造即订阅：无论 XAML 目标是 FrameworkElement 还是 Run / Span 等非 FrameworkElement，
        // 语言切换时都能刷新。
        Subscribe();
        Update();
    }

    public void Attach(FrameworkElement element)
    {
        element.Loaded += OnElementLoaded;
        element.Unloaded += OnElementUnloaded;
    }

    private void Subscribe()
    {
        // 先退订再订阅，保证幂等：构造与 Loaded 两条路径都不会重复订阅。
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void Unsubscribe() => LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;

    private void OnElementLoaded(object? sender, RoutedEventArgs e)
    {
        Subscribe();
        // 元素可能曾在卸载期间发生语言切换，重新加载时按当前语言重算文本。
        Update();
    }

    private void OnElementUnloaded(object? sender, RoutedEventArgs e) => Unsubscribe();

    private void OnLanguageChanged(object? sender, EventArgs e) => Update();

    private static void OnKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LocalizedText)d).Update();

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LocalizedText)d).Update();

    private void Update()
    {
        try
        {
            Text = string.IsNullOrEmpty(Source)
                ? LocalizationService.Instance.T(Key)
                : LocalizationService.Instance.T(Source);
        }
        catch (KeyNotFoundException)
        {
            // 兜底：键缺失时显示原始内容，避免任何未收录键导致界面崩溃。
            Text = string.IsNullOrEmpty(Source) ? Key.ToString() : Source;
        }
    }
}
