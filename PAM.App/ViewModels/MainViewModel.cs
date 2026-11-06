using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using PAM.Core;
using PAM.Core.Models;

namespace PAM.App.ViewModels;

/// <summary>
/// MainWindow 的视图模型：承载可绑定状态，供界面绑定与后台流程共同使用。
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private string _searchText = string.Empty;
    private string _profilePath = "正在读取 $PROFILE…";
    private bool _hasUnsavedChanges;
    private string _statusMessage = "正在启动…";

    private bool _isEditorOpen;
    private bool _isNewEntry;
    private AliasEntry? _editingEntry;
    private string _editorAlias = string.Empty;
    private string _editorPath = string.Empty;
    private string _editorRemark = string.Empty;
    private bool _isAliasFieldEnabled = true;
    private string _editorErrorText = string.Empty;
    private Visibility _editorErrorVisibility = Visibility.Collapsed;
    private string _editorPathHintText = string.Empty;
    private Visibility _editorPathHintVisibility = Visibility.Collapsed;
    private bool _isConfirmEnabled;

    /// <summary>
    /// 别名列表；界面通过 ItemsSource 绑定到该集合。
    /// </summary>
    public ObservableCollection<AliasEntry> Aliases { get; } = [];

    /// <summary>
    /// 搜索框内容。按原文存储（不主动修剪），保证与输入框逐键同步；
    /// 实际过滤逻辑见 <see cref="MatchesSearch"/>。
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            var text = value ?? string.Empty;
            if (_searchText == text)
            {
                return;
            }

            _searchText = text;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 当前 $PROFILE 路径；尚未成功解析时为占位或提示文本。
    /// </summary>
    public string ProfilePath
    {
        get => _profilePath;
        set
        {
            if (_profilePath == value)
            {
                return;
            }

            _profilePath = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 是否存在未保存更改。
    /// </summary>
    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        set
        {
            if (_hasUnsavedChanges == value)
            {
                return;
            }

            _hasUnsavedChanges = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 状态栏文本。
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 内联编辑条是否展开（绑定 <c>EditorPanel.Visibility</c>）。
    /// </summary>
    public bool IsEditorOpen
    {
        get => _isEditorOpen;
        set
        {
            if (_isEditorOpen == value)
            {
                return;
            }

            _isEditorOpen = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 当前是否处于“新建”模式（与 <see cref="EditingEntry"/> 互斥）。
    /// </summary>
    public bool IsNewEntry
    {
        get => _isNewEntry;
        set
        {
            if (_isNewEntry == value)
            {
                return;
            }

            _isNewEntry = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EditorTitle));
        }
    }

    /// <summary>
    /// 正在编辑的既有条目；新建时为 <c>null</c>。
    /// </summary>
    public AliasEntry? EditingEntry
    {
        get => _editingEntry;
        set
        {
            if (ReferenceEquals(_editingEntry, value))
            {
                return;
            }

            _editingEntry = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EditorTitle));
        }
    }

    /// <summary>
    /// 编辑条标题；由 <see cref="IsNewEntry"/> 与 <see cref="EditingEntry"/> 动态生成。
    /// </summary>
    public string EditorTitle => IsNewEntry || EditingEntry is null
        ? "新建别名"
        : $"编辑别名 · {EditingEntry.Name}";

    /// <summary>
    /// 别名输入框内容（绑定 <c>AliasField.Text</c>）；变更时重新校验。
    /// </summary>
    public string EditorAlias
    {
        get => _editorAlias;
        set
        {
            var text = value ?? string.Empty;
            if (_editorAlias == text)
            {
                return;
            }

            _editorAlias = text;
            OnPropertyChanged();
            UpdateValidation();
        }
    }

    /// <summary>
    /// 应用程序路径输入框内容（绑定 <c>PathField.Text</c>）；变更时重新校验。
    /// </summary>
    public string EditorPath
    {
        get => _editorPath;
        set
        {
            var text = value ?? string.Empty;
            if (_editorPath == text)
            {
                return;
            }

            _editorPath = text;
            OnPropertyChanged();
            UpdateValidation();
        }
    }

    /// <summary>
    /// 备注输入框内容（绑定 <c>RemarkField.Text</c>）。
    /// </summary>
    public string EditorRemark
    {
        get => _editorRemark;
        set
        {
            var text = value ?? string.Empty;
            if (_editorRemark == text)
            {
                return;
            }

            _editorRemark = text;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 别名输入框是否可编辑（绑定 <c>AliasField.IsEnabled</c>）；编辑既有条目时锁定主键。
    /// </summary>
    public bool IsAliasFieldEnabled
    {
        get => _isAliasFieldEnabled;
        set
        {
            if (_isAliasFieldEnabled == value)
            {
                return;
            }

            _isAliasFieldEnabled = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 校验错误文本（绑定 <c>EditorErrorText.Text</c>）。
    /// </summary>
    public string EditorErrorText
    {
        get => _editorErrorText;
        set
        {
            if (_editorErrorText == value)
            {
                return;
            }

            _editorErrorText = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 校验错误文本可见性（绑定 <c>EditorErrorText.Visibility</c>）。
    /// </summary>
    public Visibility EditorErrorVisibility
    {
        get => _editorErrorVisibility;
        set
        {
            if (_editorErrorVisibility == value)
            {
                return;
            }

            _editorErrorVisibility = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 路径提示文本（绑定 <c>EditorPathHintText.Text</c>）。
    /// </summary>
    public string EditorPathHintText
    {
        get => _editorPathHintText;
        set
        {
            if (_editorPathHintText == value)
            {
                return;
            }

            _editorPathHintText = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 路径提示文本可见性（绑定 <c>EditorPathHintText.Visibility</c>）。
    /// </summary>
    public Visibility EditorPathHintVisibility
    {
        get => _editorPathHintVisibility;
        set
        {
            if (_editorPathHintVisibility == value)
            {
                return;
            }

            _editorPathHintVisibility = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// “确定”按钮是否可用（绑定 <c>ConfirmButton.IsEnabled</c>）。
    /// </summary>
    public bool IsConfirmEnabled
    {
        get => _isConfirmEnabled;
        set
        {
            if (_isConfirmEnabled == value)
            {
                return;
            }

            _isConfirmEnabled = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 进入“新建”状态：清空输入、放开别名输入框并展开编辑条。
    /// </summary>
    public void BeginCreate()
    {
        IsNewEntry = true;
        EditingEntry = null;
        IsAliasFieldEnabled = true;

        EditorAlias = string.Empty;
        EditorPath = string.Empty;
        EditorRemark = string.Empty;

        IsEditorOpen = true;
        UpdateValidation();

        // 编辑条可能已经展开，这里再次通知，驱动界面重新聚焦输入框。
        OnPropertyChanged(nameof(IsEditorOpen));
    }

    /// <summary>
    /// 进入“编辑”状态：载入既有条目并锁定别名输入框。
    /// </summary>
    /// <param name="entry">要编辑的条目。</param>
    public void BeginEdit(AliasEntry entry)
    {
        IsNewEntry = false;
        EditingEntry = entry;
        IsAliasFieldEnabled = false;

        EditorAlias = entry.Name;
        EditorPath = entry.Path;
        EditorRemark = entry.Remark;

        IsEditorOpen = true;
        UpdateValidation();

        // 编辑条可能已经展开，这里再次通知，驱动界面重新聚焦路径输入框。
        OnPropertyChanged(nameof(IsEditorOpen));
    }

    /// <summary>
    /// 关闭编辑器并复位编辑态（面板显隐、提示与按钮状态由绑定同步）。
    /// </summary>
    public void CloseEditor()
    {
        IsEditorOpen = false;
        EditingEntry = null;
        IsNewEntry = false;

        IsAliasFieldEnabled = true;
        IsConfirmEnabled = false;

        EditorErrorText = string.Empty;
        EditorErrorVisibility = Visibility.Collapsed;

        EditorPathHintText = string.Empty;
        EditorPathHintVisibility = Visibility.Collapsed;
    }

    /// <summary>
    /// 重新校验编辑条内容，同步错误文本、路径提示与“确定”按钮状态。
    /// </summary>
    /// <returns>当前输入是否合法。</returns>
    public bool UpdateValidation()
    {
        if (!IsEditorOpen)
        {
            return true;
        }

        var rawAlias = EditorAlias;
        var alias = rawAlias.Trim();
        var path = ProfileService.NormalizePath(EditorPath);

        string? error = null;

        if (alias.Length == 0)
        {
            error = "别名不能为空。";
        }
        else if (rawAlias.Any(char.IsWhiteSpace))
        {
            error = "别名不能包含空格。";
        }
        else if (IsAliasDuplicate(alias))
        {
            error = "该别名已存在，请换一个。";
        }

        if (error is null && path.Length == 0)
        {
            error = "请选择或填写应用程序路径。";
        }

        if (error is null)
        {
            EditorErrorText = string.Empty;
            EditorErrorVisibility = Visibility.Collapsed;
        }
        else
        {
            EditorErrorText = error;
            EditorErrorVisibility = Visibility.Visible;
        }

        if (path.Length > 0 && !File.Exists(path))
        {
            EditorPathHintText = "⚠ 文件不存在，仍可保存。";
            EditorPathHintVisibility = Visibility.Visible;
        }
        else
        {
            EditorPathHintText = string.Empty;
            EditorPathHintVisibility = Visibility.Collapsed;
        }

        var isValid = error is null;
        IsConfirmEnabled = isValid;
        return isValid;
    }

    /// <summary>
    /// 判断别名是否与既有条目重复（忽略大小写，排除正在编辑的条目）。
    /// </summary>
    /// <param name="alias">已修剪的别名。</param>
    public bool IsAliasDuplicate(string alias)
    {
        return Aliases.Any(entry =>
            !ReferenceEquals(entry, _editingEntry) &&
            string.Equals(entry.Name, alias, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 校验通过后提交编辑器：新建条目或更新既有条目的路径与备注，随后关闭编辑器。
    /// 列表刷新与“有未保存更改”状态由界面在返回 <c>true</c> 后触发。
    /// </summary>
    /// <returns>是否提交成功；编辑器未打开时视为成功。</returns>
    public bool TryCommitEditor()
    {
        if (!IsEditorOpen)
        {
            return true;
        }

        if (!UpdateValidation())
        {
            return false;
        }

        var name = EditorAlias.Trim();
        var path = ProfileService.NormalizePath(EditorPath);
        var remark = EditorRemark.Trim();

        if (IsNewEntry)
        {
            Aliases.Add(new AliasEntry(name, path, remark));
        }
        else if (EditingEntry is not null)
        {
            EditingEntry.Path = path;
            EditingEntry.Remark = remark;
        }

        CloseEditor();
        return true;
    }

    /// <summary>
    /// 按别名、路径或备注判断条目是否匹配当前搜索文本（忽略大小写，比较前去掉搜索词首尾空白）。
    /// </summary>
    public bool MatchesSearch(AliasEntry entry)
    {
        var text = SearchText.Trim();
        if (text.Length == 0)
        {
            return true;
        }

        return entry.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
               entry.Path.Contains(text, StringComparison.OrdinalIgnoreCase) ||
               entry.Remark.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
