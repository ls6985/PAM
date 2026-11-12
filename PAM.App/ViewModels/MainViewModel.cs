using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using PAM.App;
using PAM.Core;
using PAM.Core.Models;

namespace PAM.App.ViewModels;

public enum StatusType { Info, Saved, Dirty, Written, Warning, Error }

public sealed class MainViewModel : INotifyPropertyChanged
{
    // 状态栏按中文原文保存消息，LocTable 的 zh → en 映射负责给出对应语言的文案。
    private const string LanguageSwitchStatus = "语言切换成功，部分设置重启后生效。";

    private readonly ProfileService _profileService = new();

    private string _searchText = string.Empty;
    private string _profilePathRaw = "正在读取 $PROFILE…";
    private string _profilePath = "正在读取 $PROFILE…";
    private bool _hasProfilePath;
    private bool _hasUnsavedChanges;
    private string _statusMessageRaw = "正在启动…";
    private string _statusMessage = "正在启动…";
    private StatusType _statusType = StatusType.Info;

    // 初始为 true：数据加载完成前不显示空状态，避免空文字闪现覆盖列表。
    private bool _isLoading = true;
    private bool _isListEmpty = true;

    private bool _isEditorOpen;
    private bool _isNewEntry;
    private AliasEntry? _editingEntry;
    private string _editorAlias = string.Empty;
    private string _editorPath = string.Empty;
    private string _editorRemark = string.Empty;
    private string _editorArguments = string.Empty;
    // 校验失败时对应的输入字段（"Alias" / "Path"），窗口据此决定焦点落点。
    private string _editorErrorField = string.Empty;
    private string _editorErrorTextRaw = string.Empty;
    private string _editorErrorText = string.Empty;
    private string _editorPathHintTextRaw = string.Empty;
    private string _editorPathHintText = string.Empty;
    private bool _isConfirmEnabled;
    private bool _isExecutionPolicyRestricted;

    public ObservableCollection<AliasEntry> Aliases { get; } = [];

    /// <summary>“自定义命令”页签的数据源，与别名页共用同一个 $PROFILE。</summary>
    public CustomCommandsViewModel CustomCommands { get; }

    /// <summary>“设置”页签的数据源。</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>“美化”页签的数据源（Starship / 字体 / 主题）。</summary>
    public BeautifyViewModel Beautify { get; }

    public string SearchText { get => _searchText; set { if (_searchText == value) return; _searchText = value ?? ""; OnPropertyChanged(); } }

    /// <summary>
    /// 当前管理的 $PROFILE 路径。加载中/失败时是一句提示文字，由本地化服务翻译；
    /// 是否为真实路径请用 <see cref="HasProfilePath"/> 判断，不要靠字符串内容猜测。
    /// </summary>
    public string ProfilePath
    {
        get => _profilePath;
        set
        {
            var raw = value ?? string.Empty;
            if (_profilePathRaw == raw) return;
            _profilePathRaw = raw;
            _profilePath = Loc.T(raw);
            OnPropertyChanged();
        }
    }

    /// <summary>未经本地化处理的原始路径，供读写文件、复制与启动终端使用。</summary>
    public string ProfilePathRaw => _profilePathRaw;

    /// <summary>是否已经拿到可用的 $PROFILE 完整路径。</summary>
    public bool HasProfilePath
    {
        get => _hasProfilePath;
        private set { if (_hasProfilePath == value) return; _hasProfilePath = value; OnPropertyChanged(); }
    }

    public bool HasUnsavedChanges { get => _hasUnsavedChanges; set { if (_hasUnsavedChanges == value) return; _hasUnsavedChanges = value; OnPropertyChanged(); } }

    /// <summary>最近一次保存时，用户区里会遮蔽 PAM 同名包装函数的命令名。</summary>
    public IReadOnlyList<string> LastConflictingNames { get; private set; } = [];

    /// <summary>状态栏文本：保存中文原文，写入界面时按当前语言翻译。</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            var raw = value ?? string.Empty;
            _statusMessageRaw = raw;
            var translated = Loc.T(raw);
            if (_statusMessage == translated) return;
            _statusMessage = translated;
            OnPropertyChanged();
        }
    }

    public StatusType StatusType { get => _statusType; set { if (_statusType == value) return; _statusType = value; OnPropertyChanged(); } }

    /// <summary>是否正在加载数据：为 true 时隐藏空状态与列表，避免加载闪烁。</summary>
    public bool IsLoading { get => _isLoading; set { if (_isLoading == value) return; _isLoading = value; OnPropertyChanged(); } }

    /// <summary>列表当前是否为空（含搜索无匹配），由窗口在刷新视图后更新。</summary>
    public bool IsListEmpty { get => _isListEmpty; set { if (_isListEmpty == value) return; _isListEmpty = value; OnPropertyChanged(); } }

    public bool IsEditorOpen { get => _isEditorOpen; set { if (_isEditorOpen == value) return; _isEditorOpen = value; OnPropertyChanged(); } }
    public bool IsNewEntry { get => _isNewEntry; set { if (_isNewEntry == value) return; _isNewEntry = value; OnPropertyChanged(); OnPropertyChanged(nameof(EditorTitle)); } }
    public AliasEntry? EditingEntry { get => _editingEntry; set { if (ReferenceEquals(_editingEntry, value)) return; _editingEntry = value; OnPropertyChanged(); OnPropertyChanged(nameof(EditorTitle)); } }
    public string EditorTitle => IsNewEntry || EditingEntry is null ? Loc.T(LocKey.NewAlias) : Loc.F(LocKey.EditAlias0, EditingEntry.Name);
    public string EditorAlias { get => _editorAlias; set { if (_editorAlias == value) return; _editorAlias = value ?? ""; OnPropertyChanged(); UpdateValidation(); } }
    public string EditorPath { get => _editorPath; set { if (_editorPath == value) return; _editorPath = value ?? ""; OnPropertyChanged(); UpdateValidation(); } }
    public string EditorRemark { get => _editorRemark; set { if (_editorRemark == value) return; _editorRemark = value ?? ""; OnPropertyChanged(); } }
    public string EditorArguments { get => _editorArguments; set { if (_editorArguments == value) return; _editorArguments = value ?? ""; OnPropertyChanged(); } }

    /// <summary>校验失败对应的输入字段："Alias" / "Path"，无错误时为空。</summary>
    public string EditorErrorField => _editorErrorField;

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

    public string EditorPathHintText
    {
        get => _editorPathHintText;
        set
        {
            _editorPathHintTextRaw = value ?? string.Empty;
            var translated = Loc.T(_editorPathHintTextRaw);
            if (_editorPathHintText == translated) return;
            _editorPathHintText = translated;
            OnPropertyChanged();
        }
    }

    public bool IsConfirmEnabled { get => _isConfirmEnabled; set { if (_isConfirmEnabled == value) return; _isConfirmEnabled = value; OnPropertyChanged(); } }

    /// <summary>执行策略是否受限（由窗口在检查后同步），保存时据此决定是否拉起终端。</summary>
    public bool IsExecutionPolicyRestricted { get => _isExecutionPolicyRestricted; set { if (_isExecutionPolicyRestricted == value) return; _isExecutionPolicyRestricted = value; OnPropertyChanged(); } }

    // --- 初始化与保存 ---

    public MainViewModel()
    {
        CustomCommands = new CustomCommandsViewModel(this);
        Settings = new SettingsViewModel();
        Beautify = new BeautifyViewModel();
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        SetStatus(StatusType.Info, "正在读取 $PROFILE…");
        HasProfilePath = false;

        try
        {
            var terminalType = AppServices.Settings.TerminalType;
            var path = await Task.Run(() => _profileService.GetProfilePath(terminalType));
            ProfilePath = path;
            HasProfilePath = true;

            var data = AppServices.Data;
            var profileExists = await Task.Run(() => File.Exists(path));

            // 仅在“代码结构版本变更”或“受管 $PROFILE 文件被改动”时才读取并校验，
            // 否则跳过读取/哈希/重建，避免每次启动都扫描 $PROFILE。
            var managedPaths = await Task.Run(() => GetManagedProfilePaths());
            var versionChanged = !string.Equals(data.LastBuiltVersion, ProfileRenderer.ManagedStructureVersion, StringComparison.Ordinal);
            var metadataChanged = managedPaths.Any(p => ProfileMetadataChanged(data, p));
            var needsCheck = versionChanged || metadataChanged;

            var needsRebuild = false;
            if (needsCheck)
            {
                needsRebuild = managedPaths.Any(p => ProfileRenderer.NeedsRebuild(p, data.ManagedHash ?? string.Empty));
            }

            if (needsRebuild)
            {
                // 让“当前终端”对应的配置文件最后渲染，使冲突名称列表以它为准。
                var ordered = managedPaths
                    .OrderBy(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                    .ToList();
                var fresh = await Task.Run(() =>
                {
                    var render = ProfileRenderer.RenderToProfiles(ordered, data);
                    data.ManagedHash = render.ManagedHash;
                    return render;
                });

                LastConflictingNames = fresh.ConflictingNames;
            }

            // 仅在确实需要检测时才落盘记录状态，常态下不写盘。
            if (needsCheck || needsRebuild)
            {
                data.LastBuiltVersion = ProfileRenderer.ManagedStructureVersion;
                RecordProfileStates(data, managedPaths);
                AppServices.SaveData(data);
            }

            Aliases.Clear();
            foreach (var alias in data.Aliases)
            {
                Aliases.Add(new AliasEntry(alias.Name, alias.Path, alias.Remark, alias.Arguments));
            }

            HasUnsavedChanges = false;
            SetStatus(
                needsRebuild ? StatusType.Warning : (profileExists ? StatusType.Saved : StatusType.Warning),
                needsRebuild
                    ? "已按 pam-data.json 重建 $PROFILE 托管区。"
                    : profileExists
                        ? $"已保存 · 共 {Aliases.Count} 个别名"
                        : "配置文件不存在，点击“保存并生效”时将自动创建。");

            // 别名与自定义命令共用同一份 pam-data.json 快照。
            await CustomCommands.InitializeAsync();
        }
        catch (Exception ex)
        {
            SetStatus(StatusType.Error, $"错误：读取 $PROFILE 失败：{ex.Message}");
            ProfilePath = "无法读取 $PROFILE 路径";
            HasProfilePath = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 返回 PAM 应管理的全部 $PROFILE 路径：已安装的 Windows PowerShell 5.1 与 PowerShell 7 各一份。
    /// 托管区内容两版一致（同一份数据快照），因此“当前 PowerShell”设置只决定 PAM 启动哪个终端，
    /// 而友好提示等托管块会同时注入两个版本，避免用户开错终端时功能失效。
    /// </summary>
    internal List<string> GetManagedProfilePaths()
    {
        var paths = new List<string>();
        if (ShellResolver.IsInstalled(ShellResolver.PowerShellFileName))
        {
            paths.Add(_profileService.GetProfilePath(AppSettings.TerminalTypePowerShell));
        }

        if (ShellResolver.IsInstalled(ShellResolver.PwshFileName))
        {
            paths.Add(_profileService.GetProfilePath(AppSettings.TerminalTypePwsh));
        }

        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// 受管 $PROFILE 自上次检测后是否可能被改动：文件不存在、或写入时间与大小与记录不符。
    /// 仅做 O(1) 的元数据比对，不读取文件内容。
    /// </summary>
    private static bool ProfileMetadataChanged(PamData data, string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            return true;
        }

        var stored = data.ProfileStates
            .FirstOrDefault(s => string.Equals(s.ProfilePath, path, StringComparison.OrdinalIgnoreCase));
        if (stored is null)
        {
            return true;
        }

        return stored.LastWriteUtc != info.LastWriteTimeUtc || stored.Length != info.Length;
    }

    /// <summary>把受管 $PROFILE 的当前元数据写入数据快照，供下次启动跳过未改动的文件。</summary>
    private static void RecordProfileStates(PamData data, List<string> paths)
    {
        var next = data.ProfileStates
            .Where(s => paths.Any(p => string.Equals(p, s.ProfilePath, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var path in paths)
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                continue;
            }

            var existing = next.FirstOrDefault(s => string.Equals(s.ProfilePath, path, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                next.Add(new ProfileState { ProfilePath = path, LastWriteUtc = info.LastWriteTimeUtc, Length = info.Length });
            }
            else
            {
                existing.LastWriteUtc = info.LastWriteTimeUtc;
                existing.Length = info.Length;
            }
        }

        data.ProfileStates = next;
    }

    public async Task<bool> SaveAsync()
    {
        if (IsEditorOpen && !TryCommitEditor()) return false;

        if (!HasProfilePath)
        {
            try
            {
                var terminalType = AppServices.Settings.TerminalType;
                var path = await Task.Run(() => _profileService.GetProfilePath(terminalType));
                ProfilePath = path;
                HasProfilePath = true;
            }
            catch (Exception ex)
            {
                SetStatus(StatusType.Error, $"错误：无法定位 $PROFILE：{ex.Message}");
                return false;
            }
        }

        SetStatus(StatusType.Info, "正在写入 $PROFILE…");

        try
        {
            var data = AppServices.Data.Clone();
            data.Aliases = Aliases.Select(static entry => new AliasData
            {
                Name = entry.Name.Trim(),
                Path = entry.Path,
                Remark = entry.Remark,
                Arguments = entry.Arguments,
            }).ToList();

            var render = await Task.Run(() =>
            {
                var paths = GetManagedProfilePaths();
                var result = ProfileRenderer.RenderToProfiles(paths, data);
                data.ManagedHash = result.ManagedHash;
                data.LastBuiltVersion = ProfileRenderer.ManagedStructureVersion;
                RecordProfileStates(data, paths);
                AppServices.SaveData(data);
                return result;
            });

            LastConflictingNames = render.ConflictingNames;
            HasUnsavedChanges = false;

            // 返回 true 表示写入成功，窗口可以启动 PowerShell 了
            return true;
        }
        catch (Exception ex)
        {
            SetStatus(StatusType.Error, $"错误：写入失败：{ex.Message}");
            return false;
        }
    }

    // 用于 Window 调用后同步界面的状态（例如启动终端失败时）
    public void SetStatus(StatusType type, string message)
    {
        StatusMessage = message;
        StatusType = type;
    }

    // --- 编辑器逻辑 ---

    public void BeginCreate()
    {
        IsNewEntry = true;
        EditingEntry = null;
        EditorAlias = string.Empty;
        EditorPath = string.Empty;
        EditorRemark = string.Empty;
        EditorArguments = string.Empty;
        IsEditorOpen = true;
        UpdateValidation();
        OnPropertyChanged(nameof(IsEditorOpen));
    }

    public void BeginEdit(AliasEntry entry)
    {
        IsNewEntry = false;
        EditingEntry = entry;
        // 别名也允许修改：写回时由 TryCommitEditor 更新 EditingEntry.Name。
        EditorAlias = entry.Name;
        EditorPath = entry.Path;
        EditorRemark = entry.Remark;
        EditorArguments = entry.Arguments;
        IsEditorOpen = true;
        UpdateValidation();
        OnPropertyChanged(nameof(IsEditorOpen));
    }

    public void CloseEditor()
    {
        IsEditorOpen = false;
        EditingEntry = null;
        IsNewEntry = false;
        _editorErrorField = string.Empty;
        IsConfirmEnabled = false;
        EditorErrorText = string.Empty;
        EditorPathHintText = string.Empty;
        EditorArguments = string.Empty;
    }

    public bool UpdateValidation()
    {
        if (!IsEditorOpen) return true;

        var rawAlias = EditorAlias;
        var alias = rawAlias.Trim();
        var path = ProfileService.NormalizePath(EditorPath);
        string? error = null;
        var errorField = "Alias";

        if (alias.Length == 0) error = "别名不能为空。";
        else if (rawAlias.Any(char.IsWhiteSpace)) error = "别名不能包含空格。";
        else if (CustomCommandService.IsBuiltinName(alias)) error = "该名称是内置命令，请换一个。";
        else if (!CustomCommandService.TryValidateName(alias, out var nameError)) error = nameError;
        else if (IsAliasDuplicate(alias)) error = "该别名已存在，请换一个。";
        if (error is null && path.Length == 0) { error = "请选择或填写应用程序路径。"; errorField = "Path"; }

        _editorErrorField = error is null ? string.Empty : errorField;
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
        var arguments = EditorArguments.Trim();

        if (IsNewEntry) Aliases.Add(new AliasEntry(name, path, remark, arguments));
        else if (EditingEntry is not null)
        {
            // 编辑时别名也可改名；IsAliasDuplicate 已排除当前项，所以同名不会被误判为冲突。
            EditingEntry.Name = name;
            EditingEntry.Path = path;
            EditingEntry.Remark = remark;
            EditingEntry.Arguments = arguments;
        }

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

    /// <summary>语言切换后按中文原文重新翻译所有由代码生成的文本。</summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        StatusMessage = _statusMessageRaw;
        var translatedPath = Loc.T(_profilePathRaw);
        if (_profilePath != translatedPath)
        {
            _profilePath = translatedPath;
            OnPropertyChanged(nameof(ProfilePath));
        }
        EditorErrorText = _editorErrorTextRaw;
        EditorPathHintText = _editorPathHintTextRaw;
        OnPropertyChanged(nameof(EditorTitle));
        SetStatus(StatusType.Saved, LanguageSwitchStatus);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
