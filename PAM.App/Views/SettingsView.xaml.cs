using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PAM.App.ViewModels;
using PAM.Core;

namespace PAM.App.Views;

/// <summary>
/// “设置”页签：只负责交互（热键录制、按钮点击、托盘状态轮询），
/// 配置读写与冲突判定在 <see cref="SettingsViewModel"/> 中。
/// </summary>
public partial class SettingsView : UserControl
{
    private readonly DispatcherTimer _trayTimer;
    private SettingsViewModel? _viewModel;

    public SettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        _trayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _trayTimer.Tick += (_, _) => _viewModel?.RefreshTrayState();
    }

    private void View_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel?.RefreshTrayState();
        _trayTimer.Start();
    }

    private void View_Unloaded(object sender, RoutedEventArgs e) => _trayTimer.Stop();

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        => _viewModel = DataContext as SettingsViewModel;

    // --- 终端 ---

    private void OpenProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null) _ = _viewModel.OpenProfileAsync();
    }

    // --- 热键 ---

    private void ChangeHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.BeginCapture();
        FocusCaptureBox();
    }

    private void RestoreHotkeyButton_Click(object sender, RoutedEventArgs e)
        => _viewModel?.RestoreDefaultHotkey();

    private void CheckHotkeyButton_Click(object sender, RoutedEventArgs e)
        => _viewModel?.CheckConflict();

    private void ApplyHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;

        var confirmed = true;
        if (_viewModel.NeedsConfirmation)
        {
            confirmed = MessageBox.Show(
                Loc.T(LocKey.UseThisHotkeyAnyway),
                Loc.T(LocKey.HotkeyConflict),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) == MessageBoxResult.Yes;
        }

        _viewModel.ApplyHotkey(confirmed);
    }

    private void HotkeyCaptureBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is not null && !_viewModel.IsCapturing) _viewModel.BeginCapture();
        FocusCaptureBox();
        e.Handled = true;
    }

    private void FocusCaptureBox()
    {
        HotkeyCaptureBox.Focus();
        Keyboard.Focus(HotkeyCaptureBox);
    }

    /// <summary>
    /// 录制组合键：把 WPF 按键转成 Win32 虚拟键码，交由共享模型规范化。
    /// 这里必须吞掉按键，避免 Ctrl+S / Ctrl+N 等被窗口级快捷键抢走。
    /// </summary>
    private void HotkeyCaptureBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel is null || !_viewModel.IsCapturing)
        {
            return;
        }

        e.Handled = true;

        var modifiers = HotkeyModifiers.None;
        var wpfModifiers = Keyboard.Modifiers;
        if (wpfModifiers.HasFlag(ModifierKeys.Control)) modifiers |= HotkeyModifiers.Control;
        if (wpfModifiers.HasFlag(ModifierKeys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (wpfModifiers.HasFlag(ModifierKeys.Shift)) modifiers |= HotkeyModifiers.Shift;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) modifiers |= HotkeyModifiers.Win;

        // 按下 Alt 时 WPF 会把真实按键放在 SystemKey。
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            _viewModel.SetCapturePreview(Loc.T(LocKey.PressAKeyCombination));
            return;
        }

        if (key == Key.Escape && modifiers == HotkeyModifiers.None)
        {
            _viewModel.CancelCapture();
            return;
        }

        if (modifiers == HotkeyModifiers.None)
        {
            _viewModel.SetCapturePreview(Loc.T(LocKey.IncludeAtLeastOneModifierCtrlAltShiftWin));
            return;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        var chord = new HotkeyChord(modifiers, virtualKey);

        if (!HotkeyChord.IsValid(chord, out var error))
        {
            _viewModel.SetCapturePreview(Loc.T(error));
            return;
        }

        _viewModel.SetCapturedChord(chord);
    }

    // --- 后台托盘 ---

    private void StartTrayButton_Click(object sender, RoutedEventArgs e) => _viewModel?.StartTray();

    private void StopTrayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null) _ = _viewModel.StopTrayAsync();
    }

    private void RestoreDefaultsButton_Click(object sender, RoutedEventArgs e)
        => _viewModel?.RestoreDefaults();
}
