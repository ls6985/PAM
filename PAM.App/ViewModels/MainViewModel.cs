using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using PAM.Core;
using PAM.Core.Models;

namespace PAM.App.ViewModels;

public enum StatusType { Info, Saved, Dirty, Written, Warning, Error }

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ProfileService _profileService = new();
    
    private string _searchText = string.Empty;
    private string _profilePath = "正在读取 $PROFILE…";
    private bool _hasUnsavedChanges;
    private string _statusMessage = "正在启动…";
    private StatusType _statusType = StatusType.Info;

    private bool _isEditorOpen;
    private bool _isNewEntry;
    private AliasEntry? _editingEntry;
    private string _editorAlias = string.Empty;
    private string _editorPath = string.Empty;
    private string _editorRemark = string.Empty;
    private bool _isAliasFieldEnabled = true;
    private string _editorErrorText = string.Empty;
    private string _editorPathHintText = string.Empty;
    private bool _isConfirmEnabled;

    public ObservableCollection<AliasEntry> Aliases { get; } = [];
    public string SearchText { get => _searchText; set { if (_searchText == value) return; _searchText = value ?? ""; OnPropertyChanged(); } }
    public string ProfilePath { get => _profilePath; set { if (_profilePath == value) return; _profilePath = value; OnPropertyChanged(); } }
    public bool HasUnsavedChanges { get => _hasUnsavedChanges; set { if (_hasUnsavedChanges == value) return; _hasUnsavedChanges = value; OnPropertyChanged(); } }
    public string StatusMessage { get => _statusMessage; set { if (_statusMessage == value) return; _statusMessage = value; OnPropertyChanged(); } }
    public StatusType StatusType { get => _statusType; set { if (_statusType == value) return; _statusType = value; OnPropertyChanged(); } }

    public bool IsEditorOpen { get => _isEditorOpen; set { if (_isEditorOpen == value) return; _isEditorOpen = value; OnPropertyChanged(); } }
    public bool IsNewEntry { get => _isNewEntry; set { if (_isNewEntry == value) return; _isNewEntry = value; OnPropertyChanged(); OnPropertyChanged(nameof(EditorTitle)); } }
    public AliasEntry? EditingEntry { get => _editingEntry; set { if (ReferenceEquals(_editingEntry, value)) return; _editingEntry = value; OnPropertyChanged(); OnPropertyChanged(nameof(EditorTitle)); } }
    public string EditorTitle => IsNewEntry || EditingEntry is null ? "新建别名" : $"编辑别名 · {EditingEntry.Name}";
    public string EditorAlias { get => _editorAlias; set { if (_editorAlias == value) return; _editorAlias = value ?? ""; OnPropertyChanged(); UpdateValidation(); } }
    public string EditorPath { get => _editorPath; set { if (_editorPath == value) return; _editorPath = value ?? ""; OnPropertyChanged(); UpdateValidation(); } }
    public string EditorRemark { get => _editorRemark; set { if (_editorRemark == value) return; _editorRemark = value ?? ""; OnPropertyChanged(); } }
    public bool IsAliasFieldEnabled { get => _isAliasFieldEnabled; set { if (_isAliasFieldEnabled == value) return; _isAliasFieldEnabled = value; OnPropertyChanged(); } }
    public string EditorErrorText { get => _editorErrorText; set { if (_editorErrorText == value) return; _editorErrorText = value; OnPropertyChanged(); } }
    public string EditorPathHintText { get => _editorPathHintText; set { if (_editorPathHintText == value) return; _editorPathHintText = value; OnPropertyChanged(); } }
    public bool IsConfirmEnabled { get => _isConfirmEnabled; set { if (_isConfirmEnabled == value) return; _isConfirmEnabled = value; OnPropertyChanged(); } }

    // --- 初始化与保存（从 Window 移入） ---

    public async Task InitializeAsync()
    {
        IsLoading = true;
        StatusMessage = "正在读取 $PROFILE…";
        StatusType = StatusType.Info;

        try
        {
            ProfilePath = await Task.Run(_profileService.GetProfilePath);
            var result = await Task.Run(() => _profileService.Load(ProfilePath));

            Aliases.Clear();
            foreach (var entry in result.Entries) Aliases.Add(entry);

            HasUnsavedChanges = false;
            StatusMessage = result.FileExists ? $"已保存 · 共 {Aliases.Count} 个别名" : "配置文件不存在，点击“保存并生效”时将自动创建。";
            StatusType = result.FileExists ? StatusType.Saved : StatusType.Warning;
        }
        catch (Exception ex)
        {
            StatusMessage = $"错误：读取 $PROFILE 失败：{ex.Message}";
            StatusType = StatusType.Error;
            ProfilePath = "无法读取 $PROFILE 路径";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<bool> SaveAsync()
    {
        if (IsEditorOpen && !TryCommitEditor()) return false;

        if (!Path.IsPathRooted(ProfilePath))
        {
            try { ProfilePath = await Task.Run(_profileService.GetProfilePath); }
            catch (Exception ex)
            {
                StatusMessage = $"错误：无法定位 $PROFILE：{ex.Message}";
                StatusType = StatusType.Error;
                return false;
            }
        }

        StatusMessage = "正在写入 $PROFILE…";
        StatusType = StatusType.Info;

        try
        {
            var snapshot = Aliases.ToList();
            var result = await Task.Run(() => _profileService.Save(ProfilePath, snapshot));

            HasUnsavedChanges = false;
            
            // 返回 true 表示写入成功，窗口可以启动 Powershell 了
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"错误：写入失败：{ex.Message}";
            StatusType = StatusType.Error;
            return false;
        }
    }

    // 用于 Window 调用后同步界面的状态（例如启动终端失败时）
    public void SetStatus(StatusType type, string message)
    {
        StatusMessage = message;
        StatusType = type;
    }

    public bool IsLoading { get; set; } // 在 Window 中用于禁用按钮

    // --- 编辑器逻辑 ---

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
        OnPropertyChanged(nameof(IsEditorOpen));
    }

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
        OnPropertyChanged(nameof(IsEditorOpen));
    }

    public void CloseEditor()
    {
        IsEditorOpen = false;
        EditingEntry = null;
        IsNewEntry = false;
        IsAliasFieldEnabled = true;
        IsConfirmEnabled = false;
        EditorErrorText = string.Empty;
        EditorPathHintText = string.Empty;
    }

    public bool UpdateValidation()
    {
        if (!IsEditorOpen) return true;

        var rawAlias = EditorAlias;
        var alias = rawAlias.Trim();
        var path = ProfileService.NormalizePath(EditorPath);
        string? error = null;

        if (alias.Length == 0) error = "别名不能为空。";
        else if (rawAlias.Any(char.IsWhiteSpace)) error = "别名不能包含空格。";
        else if (IsAliasDuplicate(alias)) error = "该别名已存在，请换一个。";
        if (error is null && path.Length == 0) error = "请选择或填写应用程序路径。";

        EditorErrorText = error ?? string.Empty;
        EditorPathHintText = (path.Length > 0 && !File.Exists(path)) ? "⚠ 文件不存在，仍可保存。" : string.Empty;

        var isValid = error is null;
        IsConfirmEnabled = isValid;
        return isValid;
    }

    public bool IsAliasDuplicate(string alias)
    {
        return Aliases.Any(entry => !ReferenceEquals(entry, _editingEntry) && string.Equals(entry.Name, alias, StringComparison.OrdinalIgnoreCase));
    }

    public bool TryCommitEditor()
    {
        if (!IsEditorOpen) return true;
        if (!UpdateValidation()) return false;

        var name = EditorAlias.Trim();
        var path = ProfileService.NormalizePath(EditorPath);
        var remark = EditorRemark.Trim();

        if (IsNewEntry) Aliases.Add(new AliasEntry(name, path, remark));
        else if (EditingEntry is not null) { EditingEntry.Path = path; EditingEntry.Remark = remark; }

        CloseEditor();
        return true;
    }

    public bool MatchesSearch(AliasEntry entry)
    {
        var text = SearchText.Trim();
        if (text.Length == 0) return true;
        return entry.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
               entry.Path.Contains(text, StringComparison.OrdinalIgnoreCase) ||
               entry.Remark.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}