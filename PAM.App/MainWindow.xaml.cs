using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using PAM.App.ViewModels;
using PAM.Core;
using PAM.Core.Models;
using Wpf.Ui.Controls;

namespace PAM.App;

public partial class MainWindow : FluentWindow
{
    private readonly ProfileService _profileService = new();
    private ICollectionView? _entriesView;
    private bool _profileExists;
    private bool _hasLoaded;
    private bool _isPolicyRestricted;

    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = ViewModel;

        _entriesView = CollectionViewSource.GetDefaultView(ViewModel.Aliases);
        _entriesView.Filter = FilterEntry;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        AppServices.SettingsChanged += OnSettingsChanged;
        UpdateEmptyState();
    }

    /// <summary>默认终端变更后，$PROFILE 换成了另一个文件，需要重新载入两个列表。</summary>
    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.TerminalChanged) _ = ReloadForTerminalAsync();
    }

    private async Task ReloadForTerminalAsync()
    {
        var hadChanges = ViewModel.HasUnsavedChanges || ViewModel.CustomCommands.HasUnsavedChanges;

        await ViewModel.InitializeAsync();
        _entriesView?.Refresh();
        UpdateEmptyState();
        _profileExists = ViewModel.HasProfilePath;

        if (hadChanges)
        {
            ViewModel.SetStatus(StatusType.Warning, "切换默认终端后已重新读取 $PROFILE，未保存的更改已丢弃。");
        }
        else if (IsAliasTabSelected)
        {
            ShowRestingStatus();
        }
        else
        {
            ViewModel.CustomCommands.ShowRestingStatus();
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasLoaded) return;
        _hasLoaded = true;

        SaveButton.IsEnabled = false;
        AddButton.IsEnabled = false;

        await ViewModel.InitializeAsync();

        // 加载一结束就刷新视图并更新空状态，确保 IsLoading 变 false 时列表状态已就绪，
        // 不会在切页/渲染间隙闪现“还没有别名”。
        _entriesView?.Refresh();
        UpdateEmptyState();

        _profileExists = ViewModel.HasProfilePath;

        await RefreshPolicyAsync(silent: true);

        SaveButton.IsEnabled = true;
        AddButton.IsEnabled = !ViewModel.IsEditorOpen;
    }

    private async Task RefreshPolicyAsync(bool silent)
    {
        try
        {
            var policy = await Task.Run(_profileService.GetExecutionPolicy);
            _isPolicyRestricted = ProfileService.IsExecutionPolicyRestricted(policy);
            ViewModel.IsExecutionPolicyRestricted = _isPolicyRestricted;

            UiLocalizer.SetTranslated(
                PolicyWarningText,
                System.Windows.Controls.TextBlock.TextProperty,
                $"执行策略为 {policy}，$PROFILE 可能无法加载。");
            PolicyWarningPanel.Visibility = _isPolicyRestricted ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            PolicyWarningPanel.Visibility = Visibility.Collapsed;
            if (!silent)
            {
                ViewModel.SetStatus(StatusType.Error, $"错误：检查执行策略失败：{exception.Message}");
            }
        }
    }

    private bool FilterEntry(object item) => item is AliasEntry entry && ViewModel.MatchesSearch(entry);

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {        if (e.PropertyName == nameof(MainViewModel.SearchText))
        {
            ResetDeletePending();
            _entriesView?.Refresh();
            UpdateEmptyState();
        }

        if (e.PropertyName == nameof(MainViewModel.IsEditorOpen))
        {
            AddButton.IsEnabled = !ViewModel.IsEditorOpen && !ViewModel.IsLoading;
            if (ViewModel.IsEditorOpen)
            {
                Dispatcher.BeginInvoke(new Action(FocusEditorField), DispatcherPriority.Input);
            }
        }
    }

    private void FocusEditorField()
    {
        if (ViewModel.IsNewEntry) { AliasField.Focus(); return; }
        PathField.Focus();
        PathField.SelectAll();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLoading) return;
        if (ViewModel.IsEditorOpen) { AliasField.Focus(); return; }
        ResetDeletePending();
        ViewModel.BeginCreate();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsEditorOpen) return;
        if (ViewModel.TryCommitEditor())
        {
            _entriesView?.Refresh();
            MarkDirty();
            UpdateEmptyState();
            return;
        }
        FocusInvalidEditorField();
    }

    private void FocusInvalidEditorField()
    {
        if (ViewModel.IsAliasFieldEnabled) { AliasField.Focus(); AliasField.SelectAll(); return; }
        PathField.Focus();
        PathField.SelectAll();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsEditorOpen) return;
        ViewModel.CloseEditor();
        ShowRestingStatus();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.T("选择要启动的应用程序"),
            Filter = Loc.T("应用程序 (*.exe)|*.exe|所有文件 (*.*)|*.*"),
            CheckFileExists = true,
            Multiselect = false,
            DefaultExt = ".exe",
            AddExtension = true
        };

        var currentPath = ProfileService.NormalizePath(ViewModel.EditorPath);
        if (File.Exists(currentPath)) dialog.InitialDirectory = Path.GetDirectoryName(currentPath);
        else if (currentPath.Length > 0)
        {
            var directory = Path.GetDirectoryName(currentPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)) dialog.InitialDirectory = directory;
        }

        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.EditorPath = dialog.FileName;
            PathField.CaretIndex = PathField.Text.Length;
            ViewModel.UpdateValidation();
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLoading) return;

        if ((sender as FrameworkElement)?.DataContext is AliasEntry entry)
        {
            e.Handled = true;
            ResetDeletePending();
            ViewModel.BeginEdit(entry);
        }
    }
    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLoading) return;

        if ((sender as FrameworkElement)?.DataContext is not AliasEntry entry) return;

        e.Handled = true;

        if (ViewModel.EditingEntry == entry)
        {
            ViewModel.CloseEditor();
            ShowRestingStatus();
        }

        if (!entry.DeletePending)
        {
            ResetDeletePending();
            entry.DeletePending = true;
            return;
        }

        ViewModel.Aliases.Remove(entry);
        _entriesView?.Refresh();
        UpdateEmptyState();
        MarkDirty();
    }

    private void AliasList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.IsLoading) return;
        if (e.OriginalSource is DependencyObject source && FindAncestor<System.Windows.Controls.Button>(source) is not null) return;
        if (e.OriginalSource is DependencyObject originalSource && FindAncestor<System.Windows.Controls.ListViewItem>(originalSource)?.DataContext is AliasEntry entry)
        {
            ResetDeletePending();
            ViewModel.BeginEdit(entry);
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLoading) return;

        SaveButton.IsEnabled = false;
        AddButton.IsEnabled = false;
        AliasList.IsEnabled = false;

        var success = await ViewModel.SaveAsync();

        if (success)
        {
            _profileExists = true;
            if (_isPolicyRestricted)
            {
                ViewModel.SetStatus(StatusType.Warning, "已写入 · 执行策略受限，未启动新的 PowerShell 窗口。请点击“尝试修复”。");
            }
            else
            {
                try
                {
                    _profileService.LaunchPowerShell(ViewModel.ProfilePathRaw, AppServices.Settings.TerminalType);
                    ViewModel.SetStatus(StatusType.Written, $"已写入 · 已启动新的 PowerShell 窗口 · 共 {ViewModel.Aliases.Count} 个别名");
                }
                catch (Exception ex)
                {
                    ViewModel.SetStatus(StatusType.Error, $"错误：已写入，但启动 PowerShell 失败：{ex.Message}");
                }
            }
        }

        ViewModel.IsLoading = false;
        SaveButton.IsEnabled = true;
        AddButton.IsEnabled = !ViewModel.IsEditorOpen;
        AliasList.IsEnabled = true;
    }

    /// <summary>
    /// 把 $PROFILE 路径复制到剪贴板，并在状态栏提示“路径已复制”。
    /// </summary>
    private void CopyPathButton_Click(object sender, RoutedEventArgs e)
    {
        var profilePath = ViewModel.ProfilePathRaw;

        if (!ViewModel.HasProfilePath)
        {
            ViewModel.SetStatus(StatusType.Warning, "尚未读取到 $PROFILE 路径，无法复制。");
            return;
        }

        try
        {
            // copy: true 让内容在窗口关闭后仍留在剪贴板。
            Clipboard.SetDataObject(profilePath, copy: true);
            ViewModel.SetStatus(StatusType.Saved, "路径已复制");
        }
        catch (Exception ex)
        {
            ViewModel.SetStatus(StatusType.Error, $"错误：复制路径失败：{ex.Message}");
        }
    }

    private async void FixPolicyButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLoading) return;
        FixPolicyButton.IsEnabled = false;
        ViewModel.SetStatus(StatusType.Info, "正在将执行策略设为 RemoteSigned (CurrentUser)…");

        try
        {
            await Task.Run(_profileService.RepairExecutionPolicy);
            var policy = await Task.Run(_profileService.GetExecutionPolicy);
            _isPolicyRestricted = ProfileService.IsExecutionPolicyRestricted(policy);
            ViewModel.IsExecutionPolicyRestricted = _isPolicyRestricted;

            if (_isPolicyRestricted)
            {
                PolicyWarningText.Text = $"执行策略仍为 {policy}，请检查系统策略。";
                PolicyWarningPanel.Visibility = Visibility.Visible;
                ViewModel.SetStatus(StatusType.Warning, "执行策略仍受限，请手动检查系统策略。");
            }
            else
            {
                PolicyWarningPanel.Visibility = Visibility.Collapsed;
                ViewModel.SetStatus(StatusType.Written, "执行策略已设为 RemoteSigned (CurrentUser)。再次保存即可在新窗口生效。");
            }
        }
        catch (Exception exception)
        {
            ViewModel.SetStatus(StatusType.Error, $"错误：修复执行策略失败：{exception.Message}");
        }
        finally
        {
            FixPolicyButton.IsEnabled = true;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 设置页正在录制全局热键时，不能让 Ctrl+S / Ctrl+N 等窗口快捷键抢走按键。
        if (ViewModel.Settings.IsCapturing)
        {
            return;
        }

        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            // 快捷键只在“别名管理”页签生效。
            if (IsAliasTabSelected)
            {
                SaveButton_Click(SaveButton, new RoutedEventArgs());
                e.Handled = true;
            }
            return;
        }

        if (ViewModel.IsEditorOpen)
        {
            if (e.Key == Key.Escape) { ViewModel.CloseEditor(); ShowRestingStatus(); e.Handled = true; return; }
            if (e.Key == Key.Enter)
            {
                if (Keyboard.FocusedElement is System.Windows.Controls.Button focusedButton)
                {
                    if (ReferenceEquals(focusedButton, CancelButton)) { ViewModel.CloseEditor(); ShowRestingStatus(); e.Handled = true; return; }
                    if (ReferenceEquals(focusedButton, BrowseButton)) { BrowseButton_Click(BrowseButton, new RoutedEventArgs()); e.Handled = true; return; }
                }
                ConfirmButton_Click(ConfirmButton, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
            return;
        }

        if (e.Key == Key.Escape && ViewModel.Aliases.Any(entry => entry.DeletePending))
        {
            ResetDeletePending();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (IsAliasTabSelected && !ViewModel.IsLoading) ViewModel.BeginCreate();
            e.Handled = true;
        }
    }

    /// <summary>当前选中的是否为“别名管理”页签。</summary>
    private bool IsAliasTabSelected => ReferenceEquals(MainTabs.SelectedItem, AliasTab);

    /// <summary>切换页签时把状态栏同步成对应页签的上下文，避免显示上一个页签的统计。</summary>
    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_hasLoaded || !ReferenceEquals(e.OriginalSource, MainTabs))
        {
            return;
        }

        if (IsAliasTabSelected)
        {
            ShowRestingStatus();
        }
        else if (ReferenceEquals(MainTabs.SelectedItem, CustomTab))
        {
            ViewModel.CustomCommands.ShowRestingStatus();
        }
        else if (ReferenceEquals(MainTabs.SelectedItem, SettingsTab))
        {
            ViewModel.Settings.RefreshTrayState();
            ViewModel.SetStatus(StatusType.Info, "所有更改都会立即写入 settings.json。");
        }
    }

    /// <summary>窗口关闭时按"无托盘行为"设置决定是否结束后台托盘。</summary>
    private void Window_Closed(object? sender, EventArgs e)
    {
        AppServices.SettingsChanged -= OnSettingsChanged;
        TrayBridge.HandleManagerClosing(AppServices.Settings);
    }

    private void MarkDirty()
    {
        ViewModel.HasUnsavedChanges = true;
        ViewModel.SetStatus(StatusType.Dirty, $"有未保存更改 · 共 {ViewModel.Aliases.Count} 个别名");
    }

    private void ShowRestingStatus()
    {
        if (ViewModel.HasUnsavedChanges)
        {
            ViewModel.SetStatus(StatusType.Dirty, $"有未保存更改 · 共 {ViewModel.Aliases.Count} 个别名");
            return;
        }

        if (!Path.IsPathRooted(ViewModel.ProfilePath)) return;

        if (!_profileExists)
        {
            ViewModel.SetStatus(StatusType.Warning, "配置文件不存在，点击“保存并生效”时将自动创建。");
            return;
        }

        ViewModel.SetStatus(StatusType.Saved, $"已保存 · 共 {ViewModel.Aliases.Count} 个别名");
    }

    /// <summary>
    /// 更新空状态文案，并把“是否为空”交给 ViewModel.IsListEmpty，
    /// 由 XAML 的 MultiBinding（IsLoading + IsListEmpty）决定显隐。
    /// </summary>
    private void UpdateEmptyState()
    {
        if (ViewModel.Aliases.Count == 0)
        {
            UiLocalizer.SetTranslated(EmptyTitleText, System.Windows.Controls.TextBlock.TextProperty, "还没有别名");
            UiLocalizer.SetTranslated(
                EmptyDescriptionText,
                System.Windows.Controls.TextBlock.TextProperty,
                "点击右上角“新建别名”，为常用应用添加一个 PowerShell 快捷命令。");
            ViewModel.IsListEmpty = true;
            return;
        }

        if (_entriesView is { IsEmpty: true })
        {
            UiLocalizer.SetTranslated(EmptyTitleText, System.Windows.Controls.TextBlock.TextProperty, "没有匹配的别名");
            UiLocalizer.SetTranslated(
                EmptyDescriptionText,
                System.Windows.Controls.TextBlock.TextProperty,
                "换个关键字试试，或清空搜索框查看全部别名。");
            ViewModel.IsListEmpty = true;
            return;
        }

        ViewModel.IsListEmpty = false;
    }

    private void ResetDeletePending()
    {
        foreach (var entry in ViewModel.Aliases) entry.DeletePending = false;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
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