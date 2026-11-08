using System.ComponentModel;
using System.Runtime.CompilerServices;
using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>变量下拉的一项：中文名 + 分组 + 变量名，语言切换时刷新显示名；同时作为片段「内容」可选项。</summary>
public sealed class VariableOption : ISegmentChoice
{
    private readonly StarshipVariableDefinition _definition;

    public VariableOption(StarshipVariableDefinition definition) => _definition = definition;

    public string Variable => _definition.Variable;

    public PromptSegmentKind Kind => PromptSegmentKind.Variable;

    public string Value => Variable;

    public string DisplayName => IsEnglish
        ? $"{_definition.GroupEn} · {_definition.NameEn}"
        : $"{_definition.GroupZh} · {_definition.NameZh}";

    /// <summary>收起时只显示名称（· 后面的部分），不显示分类。</summary>
    public string ShortName => IsEnglish ? _definition.NameEn : _definition.NameZh;

    public string ToolTipText => $"{_definition.Variable} · {_definition.Sample}";

    public bool Matches(string query)
        => string.IsNullOrEmpty(query)
           || _definition.NameZh.Contains(query, StringComparison.OrdinalIgnoreCase)
           || _definition.NameEn.Contains(query, StringComparison.OrdinalIgnoreCase)
           || _definition.GroupZh.Contains(query, StringComparison.OrdinalIgnoreCase)
           || _definition.GroupEn.Contains(query, StringComparison.OrdinalIgnoreCase)
           || _definition.Variable.Contains(query, StringComparison.OrdinalIgnoreCase);

    public void Refresh() => OnPropertyChanged(nameof(DisplayName));

    private static bool IsEnglish => string.Equals(
        Loc.Service.Language, AppSettings.LanguageEnUS, StringComparison.OrdinalIgnoreCase);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
