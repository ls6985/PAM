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
    private bool _isSaving;
    private bool _isLoading = true;
    private bool _hasLoaded;
    private bool _isPolicyRestricted;

    /// <summary>
    /// 窗口视图模型；同时作为 <see cref="DataContext"/> 供界面绑定。
    /// </summary>
    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        DataContext = ViewModel;

        _entriesView = CollectionViewSource.GetDefaultView(ViewModel.Aliases);
        _entriesView.Filter = FilterEntry;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateEmptyState();
    }

    private enum StatusKind
    {
        Info,
        Saved,
        Dirty,
        Written,
        Warning,
        Error
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await LoadProfileAsync();
    }

    private async Task LoadProfileAsync()
    {
        _isLoading = true;
        SetStatus(StatusKind.Info, "正在读取 $PROFILE…", SymbolRegular.ArrowClockwise24);

        try
        {
            var profilePath = await Task.Run(_profileService.GetProfilePath);

            ViewModel.ProfilePath = profilePath;

            var result = await Task.Run(() => _profileService.Load(profilePath));

            ViewModel.Aliases.Clear();
            foreach (var entry in result.Entries)
            {
                ViewModel.Aliases.Add(entry);
            }

            _profileExists = result.FileExists;
            ViewModel.HasUnsavedChanges = false;
            _entriesView?.Refresh();
            UpdateEmptyState();

            if (_profileExists)
            {
                SetStatus(
                    StatusKind.Saved,
                    $"已保存 · 共 {ViewModel.Aliases.Count} 个别名",
                    SymbolRegular.CheckmarkCircle24);
            }
            else
            {
                SetStatus(
                    StatusKind.Warning,
                    "配置文件不存在，点击“保存并生效”时将自动创建。",
                    SymbolRegular.Warning24);
            }
        }
        catch (Exception exception)
        {
            SetStatus(
                StatusKind.Error,
                $"错误：读取 $PROFILE 失败：{exception.Message}",
                SymbolRegular.ErrorCircle24);

            ViewModel.ProfilePath = "无法读取 $PROFILE 路径";
        }
        finally
        {
            await RefreshPolicyAsync(silent: true);
            _isLoading = false;
            SaveButton.IsEnabled = true;
            AddButton.IsEnabled = !ViewModel.IsEditorOpen;
        }
    }

    private async Task RefreshPolicyAsync(bool silent)
    {
        try
        {
            var policy = await Task.Run(_profileService.GetExecutionPolicy);
            _isPolicyRestricted = ProfileService.IsExecutionPolicyRestricted(policy);

            PolicyWarningText.Text = $"执行策略为 {policy}，$PROFILE 可能无法加载。";
            PolicyWarningPanel.Visibility = _isPolicyRestricted
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            PolicyWarningPanel.Visibility = Visibility.Collapsed;

            if (!silent)
            {
                SetStatus(
                    StatusKind.Error,
                    $"错误：检查执行策略失败：{exception.Message}",
                    SymbolRegular.ErrorCircle24);
            }
        }
    }

    private bool FilterEntry(object item)
    {
        return item is AliasEntry entry && ViewModel.MatchesSearch(entry);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SearchText))
        {
            ResetDeletePending();
            _entriesView?.Refresh();
            UpdateEmptyState();
        }

        if (e.PropertyName == nameof(MainViewModel.IsEditorOpen))
        {
            // 编辑条展开时禁用“新建别名”，收起时恢复（同时考虑加载/保存中的禁用状态）。
            AddButton.IsEnabled = !ViewModel.IsEditorOpen && !_isLoading && !_isSaving;

            if (ViewModel.IsEditorOpen)
            {
                // 焦点操作留在界面层：等编辑条完成布局后再把焦点移到对应输入框。
                Dispatcher.BeginInvoke(
                    new Action(FocusEditorField),
                    DispatcherPriority.Input);
            }
        }
    }

    /// <summary>
    /// 编辑条展开后按模式聚焦：新建聚焦别名框，编辑聚焦并全选路径框。
    /// </summary>
    private void FocusEditorField()
    {
        if (ViewModel.IsNewEntry)
        {
            AliasField.Focus();
            return;
        }

        PathField.Focus();
        PathField.SelectAll();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _isSaving)
        {
            return;
        }

        if (ViewModel.IsEditorOpen)
        {
            AliasField.Focus();
            return;
        }

        ResetDeletePending();
        ViewModel.BeginCreate();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsEditorOpen)
        {
            return;
        }

        if (ViewModel.TryCommitEditor())
        {
            _entriesView?.Refresh();
            MarkDirty();
            UpdateEmptyState();
            return;
        }

        FocusInvalidEditorField();
    }

    /// <summary>
    /// 校验未通过时把焦点移回出错的输入框并全选内容。
    /// </summary>
    private void FocusInvalidEditorField()
    {
        if (ViewModel.IsAliasFieldEnabled)
        {
            AliasField.Focus();
            AliasField.SelectAll();
            return;
        }

        PathField.Focus();
        PathField.SelectAll();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsEditorOpen)
        {
            return;
        }

        ViewModel.CloseEditor();
        ShowRestingStatus();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择要启动的应用程序",
            Filter = "应用程序 (*.exe)|*.exe|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            DefaultExt = ".exe",
            AddExtension = true
        };

        var currentPath = ProfileService.NormalizePath(ViewModel.EditorPath);

        if (File.Exists(currentPath))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(currentPath);
        }
        else if (currentPath.Length > 0)
        {
            var directory = Path.GetDirectoryName(currentPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }
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
        if (_isLoading || _isSaving)
        {
            return;
        }

        if ((sender as FrameworkElement)?.DataContext is AliasEntry entry)
        {
            e.Handled = true;
            ResetDeletePending();
            ViewModel.BeginEdit(entry);
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _isSaving)
        {
            return;
        }

        if ((sender as FrameworkElement)?.DataContext is not AliasEntry entry)
        {
            return;
        }

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
        if (_isLoading || _isSaving)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source && FindAncestor<System.Windows.Controls.Button>(source) is not null)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject originalSource &&
            FindAncestor<System.Windows.Controls.ListViewItem>(originalSource)?.DataContext is AliasEntry entry)
        {
            ResetDeletePending();
            ViewModel.BeginEdit(entry);
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isSaving || _isLoading)
        {
            return;
        }

        if (ViewModel.IsEditorOpen)
        {
            // 提交成功后编辑器会关闭；仍处于打开状态说明校验未通过，取消保存。
            ConfirmButton_Click(ConfirmButton, new RoutedEventArgs());

            if (ViewModel.IsEditorOpen)
            {
                return;
            }
        }

        // 尚未解析出真实 $PROFILE 路径（仍在读取或读取失败）时，保存前重新解析一次。
        if (!Path.IsPathRooted(ViewModel.ProfilePath))
        {
            try
            {
                ViewModel.ProfilePath = await Task.Run(_profileService.GetProfilePath);
            }
            catch (Exception exception)
            {
                SetStatus(
                    StatusKind.Error,
                    $"错误：无法定位 $PROFILE：{exception.Message}",
                    SymbolRegular.ErrorCircle24);
                return;
            }
        }

        _isSaving = true;
        SaveButton.IsEnabled = false;
        AddButton.IsEnabled = false;
        AliasList.IsEnabled = false;
        SetStatus(StatusKind.Info, "正在写入 $PROFILE…", SymbolRegular.Save24);

        try
        {
            var profilePath = ViewModel.ProfilePath;
            var snapshot = ViewModel.Aliases.ToList();

            var result = await Task.Run(() => _profileService.Save(profilePath, snapshot));

            _profileExists = true;
            ViewModel.HasUnsavedChanges = false;
            ViewModel.ProfilePath = profilePath;

            if (_isPolicyRestricted)
            {
                SetStatus(
                    StatusKind.Warning,
                    "已写入 · 执行策略受限，未启动新的 PowerShell 窗口。请点击“尝试修复”。",
                    SymbolRegular.Warning24);
                return;
            }

            try
            {
                _profileService.LaunchPowerShell(profilePath);
                SetStatus(
                    StatusKind.Written,
                    result.FileCreated
                        ? $"已创建配置文件并写入 · 已启动新的 PowerShell 窗口 · 共 {snapshot.Count} 个别名"
                        : $"已写入 · 已启动新的 PowerShell 窗口 · 共 {snapshot.Count} 个别名",
                    SymbolRegular.CheckmarkCircle24);
            }
            catch (Exception exception)
            {
                SetStatus(
                    StatusKind.Error,
                    $"错误：已写入，但启动 PowerShell 失败：{exception.Message}",
                    SymbolRegular.ErrorCircle24);
            }
        }
        catch (Exception exception)
        {
            SetStatus(
                StatusKind.Error,
                $"错误：写入失败：{exception.Message}",
                SymbolRegular.ErrorCircle24);
        }
        finally
        {
            _isSaving = false;
            SaveButton.IsEnabled = true;
            AddButton.IsEnabled = !ViewModel.IsEditorOpen;
            AliasList.IsEnabled = true;
        }
    }

    private async void FixPolicyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isSaving || _isLoading)
        {
            return;
        }

        FixPolicyButton.IsEnabled = false;
        SetStatus(
            StatusKind.Info,
            "正在将执行策略设为 RemoteSigned (CurrentUser)…",
            SymbolRegular.Wrench24);

        try
        {
            await Task.Run(_profileService.RepairExecutionPolicy);
            var policy = await Task.Run(_profileService.GetExecutionPolicy);
            _isPolicyRestricted = ProfileService.IsExecutionPolicyRestricted(policy);

            if (_isPolicyRestricted)
            {
                PolicyWarningText.Text = $"执行策略仍为 {policy}，请检查系统策略。";
                PolicyWarningPanel.Visibility = Visibility.Visible;
                SetStatus(
                    StatusKind.Warning,
                    "执行策略仍受限，请手动检查系统策略。",
                    SymbolRegular.Warning24);
            }
            else
            {
                PolicyWarningPanel.Visibility = Visibility.Collapsed;
                SetStatus(
                    StatusKind.Written,
                    "执行策略已设为 RemoteSigned (CurrentUser)。再次保存即可在新窗口生效。",
                    SymbolRegular.CheckmarkCircle24);
            }
        }
        catch (Exception exception)
        {
            SetStatus(
                StatusKind.Error,
                $"错误：修复执行策略失败：{exception.Message}",
                SymbolRegular.ErrorCircle24);
        }
        finally
        {
            FixPolicyButton.IsEnabled = true;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SaveButton_Click(SaveButton, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (ViewModel.IsEditorOpen)
        {
            if (e.Key == Key.Escape)
            {
                ViewModel.CloseEditor();
                ShowRestingStatus();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                if (Keyboard.FocusedElement is System.Windows.Controls.Button focusedButton)
                {
                    if (ReferenceEquals(focusedButton, CancelButton))
                    {
                        ViewModel.CloseEditor();
                        ShowRestingStatus();
                        e.Handled = true;
                        return;
                    }

                    if (ReferenceEquals(focusedButton, BrowseButton))
                    {
                        BrowseButton_Click(BrowseButton, new RoutedEventArgs());
                        e.Handled = true;
                        return;
                    }
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
            if (!_isLoading && !_isSaving)
            {
                ViewModel.BeginCreate();
            }

            e.Handled = true;
        }
    }

    private void MarkDirty()
    {
        ViewModel.HasUnsavedChanges = true;
        SetStatus(
            StatusKind.Dirty,
            $"有未保存更改 · 共 {ViewModel.Aliases.Count} 个别名",
            SymbolRegular.Warning24);
    }

    private void ShowRestingStatus()
    {
        if (ViewModel.HasUnsavedChanges)
        {
            SetStatus(
                StatusKind.Dirty,
                $"有未保存更改 · 共 {ViewModel.Aliases.Count} 个别名",
                SymbolRegular.Warning24);
            return;
        }

        // 与保存流程一致：尚未解析出真实 $PROFILE 路径时不刷新状态。
        if (!Path.IsPathRooted(ViewModel.ProfilePath))
        {
            return;
        }

        if (!_profileExists)
        {
            SetStatus(
                StatusKind.Warning,
                "配置文件不存在，点击“保存并生效”时将自动创建。",
                SymbolRegular.Warning24);
            return;
        }

        SetStatus(
            StatusKind.Saved,
            $"已保存 · 共 {ViewModel.Aliases.Count} 个别名",
            SymbolRegular.CheckmarkCircle24);
    }

    private void SetStatus(StatusKind kind, string message, SymbolRegular icon)
    {
        ViewModel.StatusMessage = message;
        StatusIcon.Symbol = icon;

        var brushKey = kind switch
        {
            StatusKind.Saved or StatusKind.Written => "SystemFillColorSuccessBrush",
            StatusKind.Dirty or StatusKind.Warning => "SystemFillColorCautionBrush",
            StatusKind.Error => "SystemFillColorCriticalBrush",
            _ => "TextFillColorSecondaryBrush"
        };

        StatusText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, brushKey);
        StatusIcon.SetResourceReference(TextElement.ForegroundProperty, brushKey);
    }

    private void UpdateEmptyState()
    {
        if (ViewModel.Aliases.Count == 0)
        {
            EmptyTitleText.Text = "还没有别名";
            EmptyDescriptionText.Text = "点击右上角“新建别名”，为常用应用添加一个 PowerShell 快捷命令。";
            EmptyState.Visibility = Visibility.Visible;
            return;
        }

        if (_entriesView is { IsEmpty: true })
        {
            EmptyTitleText.Text = "没有匹配的别名";
            EmptyDescriptionText.Text = "换个关键字试试，或清空搜索框查看全部别名。";
            EmptyState.Visibility = Visibility.Visible;
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
    }

    private void ResetDeletePending()
    {
        foreach (var entry in ViewModel.Aliases)
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
                ContentElement contentElement =>
                    ContentOperations.GetParent(contentElement) ??
                    (contentElement as FrameworkContentElement)?.Parent,
                Visual => VisualTreeHelper.GetParent(current),
                _ => LogicalTreeHelper.GetParent(current)
            };
        }

        return null;
    }
}
