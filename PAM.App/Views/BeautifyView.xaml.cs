using System;
using System.Windows;
using System.Windows.Controls;
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

    private void InsertVariableButton_Click(object sender, RoutedEventArgs e)
    {
        var snippet = _viewModel?.BuildVariableSnippet();
        if (!string.IsNullOrEmpty(snippet)) ReplaceSelection(snippet);
    }

    private void ColorizeButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = PromptFormatBox.SelectedText;
        if (string.IsNullOrEmpty(selected) || _viewModel is null) return;
        ReplaceSelection(_viewModel.ColorizeSelection(selected));
    }

    private void SymbolButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || _viewModel is null) return;
        var tag = button.Tag as string ?? string.Empty;
        var literal = tag == "__space__" ? " " : tag;
        if (literal.Length == 0) return;
        ReplaceSelection(_viewModel.BuildLiteralSnippet(literal));
    }

    /// <summary>在当前光标处替换选区，并把光标放到插入内容之后；改 Text 会自动回写绑定并重算预览。</summary>
    private void ReplaceSelection(string snippet)
    {
        var text = PromptFormatBox.Text ?? string.Empty;
        var start = Math.Min(PromptFormatBox.SelectionStart, text.Length);
        var length = Math.Min(PromptFormatBox.SelectionLength, text.Length - start);
        PromptFormatBox.Text = text[..start] + snippet + text[(start + length)..];
        PromptFormatBox.SelectionStart = start + snippet.Length;
        PromptFormatBox.SelectionLength = 0;
        PromptFormatBox.Focus();
    }

    private void OpenTomlButton_Click(object sender, RoutedEventArgs e)
        => _ = _viewModel?.OpenTomlAsync();

    private void RestoreTomlButton_Click(object sender, RoutedEventArgs e)
        => _ = _viewModel?.RestoreTomlAsync();

    private void RestoreWtBackupButton_Click(object sender, RoutedEventArgs e)
        => _ = _viewModel?.RestoreWtBackupAsync();
}
