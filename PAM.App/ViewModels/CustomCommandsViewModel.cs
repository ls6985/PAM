using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using PAM.App;
using PAM.Core;
using PAM.Core.Models;

namespace PAM.App.ViewModels;

/// <summary>
/// “自定义命令”页签的视图模型：列表、搜索、内联编辑与受控区块读写。
/// 交互与校验策略与别名启动保持一致。
/// </summary>
public sealed class CustomCommandsViewModel : INotifyPropertyChanged
{
    private readonly ProfileService _profileService = new();
    private readonly MainViewModel _host;

    private string _searchText = string.Empty;
    private bool _isLoading = true;
    private bool _isListEmpty = true;
    private bool _hasUnsavedChanges;

    private bool _isEditorOpen;
    private bool _isNewEntry;
    private CustomCommandEntry? _editingEntry;
    private string _editorName = string.Empty;
    private string _editorCode = string.Empty;
    private string _editorRemark = string.Empty;
    private bool _editorEnabled = true;
    private bool _isNameFieldEnabled = true;
    private string _editorErrorText = string.Empty;
    private string _editorErrorTextRaw = string.Empty;
    private bool _isConfirmEnabled;

    public CustomCommandsViewModel(MainViewModel host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<CustomCommandEntry> Commands { get; } = [];

    /// <summary>用户命令数（不含内置命令），用于状态栏文案。</summary>
    public int UserCommandCount => Commands.Count(static entry => !entry.IsBuiltin);

    public string SearchText
    {
        get => _searchText;
        set
        {
            var text = value ?? string.Empty;
            if (_searchText == text) return;
            _searchText = text;
            OnPropertyChanged();
        }
    }

    /// <summary>加载中为 true：界面此时隐藏列表与空状态，避免文字闪现。</summary>
    public bool IsLoading
    {
        get => _isLoading;
        set { if (_isLoading == value) return; _isLoading = value; OnPropertyChanged(); }
    }

    /// <summary>列表当前是否为空（含搜索无匹配），由视图在刷新后写入。</summary>
    public bool IsListEmpty
    {
        get => _isListEmpty;
        set { if (_isListEmpty == value) return; _isListEmpty = value; OnPropertyChanged(); }
    }

    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        set { if (_hasUnsavedChanges == value) return; _hasUnsavedChanges = value; OnPropertyChanged(); }
    }

    public bool IsEditorOpen
    {
        get => _isEditorOpen;
        set { if (_isEditorOpen == value) return; _isEditorOpen = value; OnPropertyChanged(); }
    }

    public bool IsNewEntry
    {
        get => _isNewEntry;
        set
        {
            if (_isNewEntry == value) return;
            _isNewEntry = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EditorTitle));
        }
    }

    public CustomCommandEntry? EditingEntry
    {
        get => _editingEntry;
        set
        {
            if (ReferenceEquals(_editingEntry, value)) return;
            _editingEntry = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EditorTitle));
        }
    }

    public string EditorTitle => IsNewEntry || EditingEntry is null
        ? Loc.T("新建命令")
        : Loc.T($"编辑命令 · {EditingEntry.Name}");

    public string EditorName
    {
        get => _editorName;
        set
        {
            var text = value ?? string.Empty;
            if (_editorName == text) return;
            _editorName = text;
            OnPropertyChanged();
            UpdateValidation();
        }
    }

    public string EditorCode
    {
        get => _editorCode;
        set
        {
            var text = value ?? string.Empty;
            if (_editorCode == text) return;
            _editorCode = text;
            OnPropertyChanged();
            UpdateValidation();
        }
    }

    public string EditorRemark
    {
        get => _editorRemark;
        set
        {
            var text = value ?? string.Empty;
            if (_editorRemark == text) return;
            _editorRemark = text;
            OnPropertyChanged();
        }
    }

    public bool EditorEnabled
    {
        get => _editorEnabled;
        set { if (_editorEnabled == value) return; _editorEnabled = value; OnPropertyChanged(); }
    }

    /// <summary>编辑已有命令时名称是唯一键，禁止改名（与别名规则一致）。</summary>
    public bool IsNameFieldEnabled
    {
        get => _isNameFieldEnabled;
        set { if (_isNameFieldEnabled == value) return; _isNameFieldEnabled = value; OnPropertyChanged(); }
    }

    public string EditorErrorText
    {
        get => _editorErrorText;
        set
        {
            _editorErrorTextRaw = value ?? string.Empty;
            var translated = Loc.T(_editorErrorTextRaw);
            if (_editorErrorText == translated) return;
            _editorErrorText = translated;
            OnPropertyChanged();
        }
    }

    public bool IsConfirmEnabled
    {
        get => _isConfirmEnabled;
        set { if (_isConfirmEnabled == value) return; _isConfirmEnabled = value; OnPropertyChanged(); }
    }

    // --- 初始化与保存 ---

    public async Task InitializeAsync()
    {
        IsLoading = true;

        try
        {
            var data = await Task.Run(() => AppServices.Data.Clone());

            Commands.Clear();
            foreach (var command in data.CustomCommands)
            {
                Commands.Add(new CustomCommandEntry(
                    command.Name,
                    string.Join("\n", command.CodeLines),
                    command.IsEnabled,
                    command.Remark));
            }

            foreach (var builtin in CustomCommandService.GetBuiltinEntries())
            {
                Commands.Add(builtin);
            }

            HasUnsavedChanges = false;
            IsListEmpty = Commands.Count == 0;
        }
        catch (Exception ex)
        {
            _host.SetStatus(StatusType.Error, $"错误：读取自定义命令失败：{ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>把当前列表写入 $PROFILE 的受控区块，并像别名页一样启动新窗口验证。</summary>
    public async Task<bool> SaveAsync()
    {
        if (IsEditorOpen && !TryCommitEditor())
        {
            return false;
        }

        var path = _host.ProfilePathRaw;

        if (!Path.IsPathRooted(path))
        {
            try
            {
                var terminalType = AppServices.Settings.TerminalType;
                path = await Task.Run(() => _profileService.GetProfilePath(terminalType));
                _host.ProfilePath = path;
            }
            catch (Exception ex)
            {
                _host.SetStatus(StatusType.Error, $"错误：无法定位 $PROFILE：{ex.Message}");
                return false;
            }
        }

        _host.SetStatus(StatusType.Info, "正在写入自定义命令…");

        try
        {
            // 内置命令不进入数据文件：托管区渲染时按定义表固定追加最新版本。
            var data = AppServices.Data.Clone();
            data.CustomCommands = Commands
                .Where(static entry => !entry.IsBuiltin)
                .Select(static entry => new CustomCommandData
                {
                    Name = entry.Name.Trim(),
                    CodeLines = entry.Code.Split('\n'),
                    IsEnabled = entry.IsEnabled,
                    Remark = entry.Remark,
                })
                .ToList();

            var commandCount = data.CustomCommands.Count;
            var result = await Task.Run(() =>
            {
                var render = ProfileRenderer.Render(path, data);
                data.ManagedHash = render.ManagedHash;
                AppServices.SaveData(data);
                return render;
            });

            HasUnsavedChanges = false;

            var prefix = result.FileCreated ? "已创建 $PROFILE 并写入" : "已写入";

            if (_host.IsExecutionPolicyRestricted)
            {
                _host.SetStatus(
                    StatusType.Warning,
                    $"{prefix} · 共 {commandCount} 个自定义命令 · 执行策略受限，未启动新的 PowerShell 窗口。");
                return true;
            }

            try
            {
                await Task.Run(() => _profileService.LaunchPowerShell(path, AppServices.Settings.TerminalType));
                _host.SetStatus(
                    StatusType.Written,
                    $"{prefix} · 已启动新的 PowerShell 窗口 · 共 {commandCount} 个自定义命令");
            }
            catch (Exception ex)
            {
                _host.SetStatus(StatusType.Error, $"错误：已写入，但启动 PowerShell 失败：{ex.Message}");
            }

            return true;
        }
        catch (Exception ex)
        {
            _host.SetStatus(StatusType.Error, $"错误：写入自定义命令失败：{ex.Message}");
            return false;
        }
    }

    // --- 编辑器 ---

    public void BeginCreate()
    {
        IsNewEntry = true;
        EditingEntry = null;
        IsNameFieldEnabled = true;
        EditorName = string.Empty;
        EditorCode = string.Empty;
        EditorRemark = string.Empty;
        EditorEnabled = true;
        IsEditorOpen = true;
        UpdateValidation();
    }

    public void BeginEdit(CustomCommandEntry entry)
    {
        if (entry.IsBuiltin)
        {
            return;
        }

        IsNewEntry = false;
        EditingEntry = entry;
        IsNameFieldEnabled = false;
        EditorName = entry.Name;
        EditorCode = ToEditorText(entry.Code);
        EditorRemark = entry.Remark;
        EditorEnabled = entry.IsEnabled;
        IsEditorOpen = true;
        UpdateValidation();
    }

    public void CloseEditor()
    {
        IsEditorOpen = false;
        EditingEntry = null;
        IsNewEntry = false;
        IsNameFieldEnabled = true;
        IsConfirmEnabled = false;
        EditorErrorText = string.Empty;
    }

    public bool UpdateValidation()
    {
        if (!IsEditorOpen)
        {
            return true;
        }

        string? error = null;

        if (!CustomCommandService.TryValidateName(EditorName, out var nameError))
        {
            error = nameError;
        }
        else if (IsNameDuplicate(EditorName.Trim()))
        {
            error = "该名称已存在，请换一个。";
        }

        if (error is null && string.IsNullOrWhiteSpace(EditorCode))
        {
            error = "命令代码不能为空。";
        }

        EditorErrorText = error ?? string.Empty;
        IsConfirmEnabled = error is null;
        return error is null;
    }

    public bool IsNameDuplicate(string name)
    {
        return Commands.Any(entry =>
            !ReferenceEquals(entry, _editingEntry) &&
            string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));
    }

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

        var name = EditorName.Trim();
        var code = CustomCommandEntry.NormalizeCodeBlock(EditorCode);
        var remark = EditorRemark.Trim();

        if (IsNewEntry)
        {
            Commands.Add(new CustomCommandEntry(name, code, EditorEnabled, remark));
        }
        else if (EditingEntry is not null)
        {
            EditingEntry.Code = code;
            EditingEntry.Remark = remark;
            EditingEntry.IsEnabled = EditorEnabled;
        }

        CloseEditor();
        return true;
    }

    public bool MatchesSearch(CustomCommandEntry entry)
    {
        var text = SearchText.Trim();
        if (text.Length == 0)
        {
            return true;
        }

        return entry.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
               entry.Code.Contains(text, StringComparison.OrdinalIgnoreCase) ||
               entry.Remark.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>在列表行上直接切换启用状态（无需打开编辑条）；内置命令不可切换。</summary>
    public void ToggleEnabled(CustomCommandEntry entry)
    {
        if (entry.IsBuiltin)
        {
            return;
        }

        entry.IsEnabled = !entry.IsEnabled;
        MarkDirty();
    }

    public void MarkDirty()
    {
        HasUnsavedChanges = true;
        _host.SetStatus(StatusType.Dirty, $"有未保存更改 · 共 {UserCommandCount} 个自定义命令");
    }

    public void ShowRestingStatus()
    {
        if (IsLoading)
        {
            return;
        }

        if (HasUnsavedChanges)
        {
            _host.SetStatus(StatusType.Dirty, $"有未保存更改 · 共 {UserCommandCount} 个自定义命令");
            return;
        }

        _host.SetStatus(StatusType.Saved, $"已保存 · 共 {UserCommandCount} 个自定义命令");
    }

    /// <summary>把模型里以 \n 存储的代码转成 TextBox 使用的 CRLF。</summary>
    private static string ToEditorText(string code) => code.Replace("\n", "\r\n", StringComparison.Ordinal);

    /// <summary>语言切换后重新翻译编辑器提示、标题与列表里的状态胶囊。</summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        EditorErrorText = _editorErrorTextRaw;
        OnPropertyChanged(nameof(EditorTitle));
        if (IsEditorOpen) UpdateValidation();

        foreach (var command in Commands) command.RefreshLocalizedText();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
