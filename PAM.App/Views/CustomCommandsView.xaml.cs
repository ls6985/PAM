using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PAM.App.ViewModels;
using PAM.Core.Models;

namespace PAM.App.Views;

/// <summary>
/// “自定义命令”页签：列表 + 内联编辑条，交互模式与别名管理保持一致。
/// 只负责 UI 交互（焦点、空状态文案），读写与校验在 <see cref="CustomCommandsViewModel"/> 中。
/// </summary>
public partial class CustomCommandsView : UserControl
{
    private CustomCommandsViewModel? _viewModel;
    private ICollectionView? _commandsView;

    public CustomCommandsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as CustomCommandsViewModel;

        if (_viewModel is null)
        {
            _commandsView = null;
            return;
        }

        _commandsView = CollectionViewSource.GetDefaultView(_viewModel.Commands);
        _commandsView.Filter = FilterCommand;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        RefreshToolbarState();
        UpdateEmptyState();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _commandsView?.Refresh();
        UpdateEmptyState();
    }

    private bool FilterCommand(object item)
        => item is CustomCommandEntry entry && _viewModel?.MatchesSearch(entry) == true;

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(CustomCommandsViewModel.SearchText):
                ResetDeletePending();
                _commandsView?.Refresh();
                UpdateEmptyState();
                break;

            case nameof(CustomCommandsViewModel.IsLoading):
                RefreshToolbarState();
                _commandsView?.Refresh();
                UpdateEmptyState();
                break;

            case nameof(CustomCommandsViewModel.IsEditorOpen):
                AddButton.IsEnabled = !_viewModel.IsEditorOpen && !_viewModel.IsLoading;
                if (_viewModel.IsEditorOpen)
                {
                    Dispatcher.BeginInvoke(new Action(FocusEditorField), DispatcherPriority.Input);
                }
                break;
        }
    }

    private void RefreshToolbarState()
    {
        if (_viewModel is null)
        {
            return;
        }

        var ready = !_viewModel.IsLoading;
        AddButton.IsEnabled = ready && !_viewModel.IsEditorOpen;
        SaveButton.IsEnabled = ready;
    }

    private void FocusEditorField()
    {
        if (_viewModel is null)
        {
            return;
        }

        if (_viewModel.IsNewEntry)
        {
            NameField.Focus();
            return;
        }

        // 编辑已有命令时名称不可改，直接把焦点放到代码上。
        CodeEditor.Focus();
    }

    // --- 列表与编辑器交互 ---

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || _viewModel.IsLoading)
        {
            return;
        }

        if (_viewModel.IsEditorOpen)
        {
            NameField.Focus();
            return;
        }

        ResetDeletePending();
        _viewModel.BeginCreate();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || !_viewModel.IsEditorOpen)
        {
            return;
        }

        if (_viewModel.TryCommitEditor())
        {
            _commandsView?.Refresh();
            UpdateEmptyState();
            _viewModel.MarkDirty();
            CommandList.SelectedItem = null;
            return;
        }

        if (_viewModel.IsNameFieldEnabled)
        {
            NameField.Focus();
            NameField.SelectAll();
        }
        else
        {
            CodeEditor.Focus();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || !_viewModel.IsEditorOpen)
        {
            return;
        }

        _viewModel.CloseEditor();
        _viewModel.ShowRestingStatus();
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || _viewModel.IsLoading)
        {
            return;
        }

        if ((sender as FrameworkElement)?.DataContext is CustomCommandEntry entry)
        {
            e.Handled = true;
            ResetDeletePending();
            _viewModel.BeginEdit(entry);
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || _viewModel.IsLoading)
        {
            return;
        }

        if ((sender as FrameworkElement)?.DataContext is not CustomCommandEntry entry)
        {
            return;
        }

        e.Handled = true;

        if (ReferenceEquals(_viewModel.EditingEntry, entry))
        {
            _viewModel.CloseEditor();
            _viewModel.ShowRestingStatus();
        }

        if (!entry.DeletePending)
        {
            ResetDeletePending();
            entry.DeletePending = true;
            return;
        }

        _viewModel.Commands.Remove(entry);
        _commandsView?.Refresh();
        UpdateEmptyState();
        _viewModel.MarkDirty();
    }

    private void ToggleStateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || _viewModel.IsLoading)
        {
            return;
        }

        if ((sender as FrameworkElement)?.DataContext is not CustomCommandEntry entry)
        {
            return;
        }

        e.Handled = true;
        _viewModel.ToggleEnabled(entry);
    }

    private void CommandList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null || _viewModel.IsLoading)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source && FindAncestor<Button>(source) is not null)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject originalSource &&
            FindAncestor<ListViewItem>(originalSource)?.DataContext is CustomCommandEntry entry)
        {
            ResetDeletePending();
            _viewModel.BeginEdit(entry);
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || _viewModel.IsLoading)
        {
            return;
        }

        SaveButton.IsEnabled = false;
        AddButton.IsEnabled = false;
        CommandList.IsEnabled = false;

        try
        {
            await _viewModel.SaveAsync();
            _commandsView?.Refresh();
            UpdateEmptyState();
        }
        finally
        {
            CommandList.IsEnabled = true;
            RefreshToolbarState();
        }
    }

    // --- 快捷键 ---

    private void View_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (SaveButton.IsEnabled)
            {
                SaveButton_Click(SaveButton, new RoutedEventArgs());
            }

            e.Handled = true;
            return;
        }

        if (_viewModel.IsEditorOpen)
        {
            if (e.Key == Key.Escape)
            {
                _viewModel.CloseEditor();
                _viewModel.ShowRestingStatus();
                e.Handled = true;
                return;
            }

            // 代码编辑框需要回车换行，所以用 Ctrl+Enter 确认。
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ConfirmButton_Click(ConfirmButton, new RoutedEventArgs());
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (!_viewModel.IsLoading)
            {
                BeginCreateFromShortcut();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && _viewModel.Commands.Any(entry => entry.DeletePending))
        {
            ResetDeletePending();
            e.Handled = true;
        }
    }

    private void BeginCreateFromShortcut()
    {
        ResetDeletePending();
        _viewModel?.BeginCreate();
    }

    // --- 空状态 ---

    private void UpdateEmptyState()
    {
        if (_viewModel is null)
        {
            return;
        }

        if (_viewModel.Commands.Count == 0)
        {
            EmptyTitleText.Text = "还没有自定义命令";
            EmptyDescriptionText.Text = "点击右上角“新建命令”，把常用函数或脚本片段写进 $PROFILE 的受控区块。";
            _viewModel.IsListEmpty = true;
            return;
        }

        if (_commandsView is { IsEmpty: true })
        {
            EmptyTitleText.Text = "没有匹配的命令";
            EmptyDescriptionText.Text = "换个关键字试试，或清空搜索框查看全部命令。";
            _viewModel.IsListEmpty = true;
            return;
        }

        _viewModel.IsListEmpty = false;
    }

    private void ResetDeletePending()
    {
        if (_viewModel is null)
        {
            return;
        }

        foreach (var entry in _viewModel.Commands)
        {
            entry.DeletePending = false;
        }
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = current switch
            {
                ContentElement contentElement => ContentOperations.GetParent(contentElement) ?? (contentElement as FrameworkContentElement)?.Parent,
                Visual => VisualTreeHelper.GetParent(current),
                _ => LogicalTreeHelper.GetParent(current)
            };
        }

        return null;
    }
}
