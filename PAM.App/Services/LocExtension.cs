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

    public LocExtension(LocKey key) => Key = key;

    public LocKey Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var localized = new LocalizedText { Key = Key };

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

    public string Text
    {
        get => (string)GetValue(TextProperty);
        private set => SetValue(TextProperty, value);
    }

    private FrameworkElement? _element;

    public void Attach(FrameworkElement element)
    {
        _element = element;
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        element.Unloaded += OnElementUnloaded;
        Update();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Update();

    private void OnElementUnloaded(object? sender, RoutedEventArgs e)
    {
        if (_element is not null) _element.Unloaded -= OnElementUnloaded;
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        _element = null;
    }

    private static void OnKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LocalizedText)d).Update();

    private void Update() => Text = LocalizationService.Instance.T(Key);
}
