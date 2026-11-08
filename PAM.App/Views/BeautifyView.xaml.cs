using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using PAM.App.ViewModels;

namespace PAM.App.Views;

/// <summary>
/// "美化"页签的视图壳：事件转发给 <see cref="BeautifyViewModel" />，刷新在进入页签时触发。
/// </summary>
public partial class BeautifyView : UserControl
{
    private BeautifyViewModel? _viewModel;

    public BeautifyView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => _viewModel = DataContext as BeautifyViewModel;
    }

    private async void View_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            await _viewModel.RefreshStateAsync();
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
        => _ = _viewModel?.RefreshStateAsync();

    private void OneClickInstallButton_Click(object sender, RoutedEventArgs e)
        => _ = _viewModel?.InstallStarshipWithScoopAsync();

    private void InstallBundledFontButton_Click(object sender, RoutedEventArgs e)
        => _ = _viewModel?.InstallBundledFontAsync();

    private void ApplyFontButton_Click(object sender, RoutedEventArgs e)
        => _ = _viewModel?.ApplyFontAsync();

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
