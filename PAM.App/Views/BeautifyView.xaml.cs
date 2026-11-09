using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using PAM.App.ViewModels;

namespace PAM.App.Views;

/// <summary>
/// "美化"页签的视图壳：仅把按钮事件转发给 <see cref="BeautifyViewModel" />；刷新不再在进入页签时自动触发，
/// 改由页头「刷新状态」按钮发起，避免切换页签卡顿。
/// </summary>
public partial class BeautifyView : UserControl
{
    private BeautifyViewModel? _viewModel;

    public BeautifyView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => _viewModel = DataContext as BeautifyViewModel;
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
        => _ = _viewModel?.RefreshStateAsync();

    private void OneClickInstallButton_Click(object sender, RoutedEventArgs e)
        => _ = _viewModel?.InstallStarshipWithScoopAsync();

    private void InstallBundledFontButton_Click(object sender, RoutedEventArgs e)
        => _ = _viewModel?.InstallBundledFontAsync();

    private void ApplyAppearanceButton_Click(object sender, RoutedEventArgs e)
        => _ = _viewModel?.ApplyAppearanceAsync();

    private void DiscardAppearanceButton_Click(object sender, RoutedEventArgs e)
        => _viewModel?.DiscardAppearance();

    private void BrowseBackgroundImageButton_Click(object sender, RoutedEventArgs e)
        => _viewModel?.BrowseBackgroundImage();

    private void ClearBackgroundImageButton_Click(object sender, RoutedEventArgs e)
        => _viewModel?.ClearBackgroundImage();

    private void ApplyPresetButton_Click(object sender, RoutedEventArgs e)
        => _ = _viewModel?.ApplyPresetAsync();

    private void ApplyPromptButton_Click(object sender, RoutedEventArgs e)
        => _ = _viewModel?.ApplyPromptAsync();

    private void RestorePromptButton_Click(object sender, RoutedEventArgs e)
        => _ = _viewModel?.RestorePromptAsync();

    private void ResetPromptButton_Click(object sender, RoutedEventArgs e)
        => _viewModel?.ResetPrompt();

    private void AddSegmentButton_Click(object sender, RoutedEventArgs e)
        => _viewModel?.AddSegment();

    private void RemoveSegmentButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: PromptSegment segment })
        {
            _viewModel?.RemoveSegment(segment);
        }
    }

    private void OpenTomlButton_Click(object sender, RoutedEventArgs e)
        => _ = _viewModel?.OpenTomlAsync();

    private void RestoreTomlButton_Click(object sender, RoutedEventArgs e)
        => _ = _viewModel?.RestoreTomlAsync();

    private void RestoreWtBackupButton_Click(object sender, RoutedEventArgs e)
        => _ = _viewModel?.RestoreWtBackupAsync();
}
