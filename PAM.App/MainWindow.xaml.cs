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

    /// <summary>
    /// 界面未定义 Alt 访问键；单独按 Alt 会使 WPF 将键盘焦点框绘制到当前聚焦的容器上，此处予以拦截。
    /// Alt 组合键（Key.System 且 SystemKey 为普通键）不受影响，热键录制保持可用。
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled) return;

        if (e.Key == Key.System && e.SystemKey is Key.LeftAlt or Key.RightAlt)
        {
            e.Handled = true;
        }
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

        // 加载完成后刷新视图并更新空状态，确保 IsLoading 置为 false 前列表状态已就绪，
        // 避免切页与渲染间隙短暂显示“还没有别名”。
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
        // 每次打开编辑条时把高级选项恢复为收起状态。
        AdvancedToggle.IsChecked = false;
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
        // 别名和路径都可编辑，按 ViewModel 标记的出错字段把光标放到对应输入框。
        if (ViewModel.EditorErrorField == "Path") { PathField.Focus(); PathField.SelectAll(); return; }
        AliasField.Focus();
        AliasField.SelectAll();
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
            Title = Loc.T(LocKey.SelectAnApplicationToLaunch),
            Filter = Loc.T(LocKey.ApplicationsExeExeAllFiles),
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

            if (ViewModel.LastConflictingNames.Count > 0)
            {
                ViewModel.SetStatus(
                    StatusType.Warning,
                    LocalizationService.Instance.F(
                        "{0} 与 $PROFILE 中已有命令同名，这些别名不会生效。",
                        string.Join("、", ViewModel.LastConflictingNames)));
            }
        }

        ViewModel.IsLoading = false;
        SaveButton.IsEnabled = true;
        AddButton.IsEnabled = !ViewModel.IsEditorOpen;
        AliasList.IsEnabled = true;
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
            // 快捷键只在“别名启动”页签生效。
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
                // 高级选项开关用 Enter / Space 自行切换，不触发编辑确认。
                if (ReferenceEquals(Keyboard.FocusedElement, AdvancedToggle)) { return; }
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

    /// <summary>当前选中的是否为“别名启动”页签。</summary>
    private bool IsAliasTabSelected => ReferenceEquals(MainTabs.SelectedItem, AliasTab);

    /// <summary>上一次选中的页签，用于无选中项时恢复。</summary>
    private TabViewItem? _lastSelectedTab;

    /// <summary>
    /// 页签选中态由模板中的 ToggleButton 双向绑定：再次点击已选中的页签会取消勾选，
    /// 使 TabView 无选中项并显示空白内容区。此处拦截该类点击，维持“点击即选中”的行为。
    /// </summary>
    private void MainTabs_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;

        // 仅处理模板中名为 HeaderBorder 的标题区域；内容区点击需继续传递，
        // 否则页签内的按钮事件会被一并拦截。
        DependencyObject? current = source;
        var clickedHeader = false;
        TabViewItem? clickedTab = null;

        while (current is not null && !ReferenceEquals(current, MainTabs))
        {
            if (current is FrameworkElement { Name: "HeaderBorder" }) clickedHeader = true;
            if (current is TabViewItem tab) { clickedTab = tab; break; }

            current = current is Visual visual
                ? VisualTreeHelper.GetParent(visual)
                : LogicalTreeHelper.GetParent(current);
        }

        if (clickedHeader && clickedTab is { IsSelected: true } && MainTabs.Items.Contains(clickedTab))
        {
            e.Handled = true;
        }
    }

    /// <summary>切换页签时把状态栏同步成对应页签的上下文，避免显示上一个页签的统计。</summary>
    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_hasLoaded || !ReferenceEquals(e.OriginalSource, MainTabs))
        {
            return;
        }

        // 无论鼠标、键盘或代码导致无选中项，均恢复上一次的页签，避免内容区空白。
        // 常规场景下点击拦截已在 PreviewMouseLeftButtonDown 处理。
        if (MainTabs.SelectedItem is null)
        {
            var restore = _lastSelectedTab ?? AliasTab;
            restore.IsSelected = true;
            MainTabs.SelectedItem = restore;
            return;
        }

        _lastSelectedTab = MainTabs.SelectedItem as TabViewItem;

        // 关于页不显示全局状态栏（与别名/命令/美化/设置无关）。
        StatusBarBorder.Visibility = ReferenceEquals(MainTabs.SelectedItem, AboutTab)
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (IsAliasTabSelected)
        {
            ShowRestingStatus();
        }
        else if (ReferenceEquals(MainTabs.SelectedItem, CustomTab))
        {
            ViewModel.CustomCommands.ShowRestingStatus();
        }
        else if (ReferenceEquals(MainTabs.SelectedItem, BeautifyTab))
        {
            ViewModel.SetStatus(StatusType.Info, "为终端配置 Starship 提示符与 Nerd Font 字体。");
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
    /// 更新空状态文案，并将空状态标记写入 ViewModel.IsListEmpty，
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