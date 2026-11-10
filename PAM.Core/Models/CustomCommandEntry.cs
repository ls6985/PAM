using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PAM.Core.Models;

/// <summary>
/// 表示 $PROFILE 受控区块（<c>#region PAM-Custom</c>）中的一段自定义命令代码。
/// </summary>
public sealed class CustomCommandEntry : INotifyPropertyChanged
{
    private string _name;
    private string _code;
    private string _remark;
    private bool _isEnabled;
    private bool _deletePending;

    public CustomCommandEntry(
        string name,
        string code,
        bool isEnabled = true,
        string remark = "",
        bool isBuiltin = false)
    {
        _name = name;
        _code = NormalizeCode(code);
        _isEnabled = isEnabled;
        _remark = remark;
        IsBuiltin = isBuiltin;
    }

    /// <summary>命令名（写入区块头部行的第一个字段），同时用作唯一键。</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            OnPropertyChanged();
        }
    }

    /// <summary>命令代码，统一使用 \n 作为换行符。</summary>
    public string Code
    {
        get => _code;
        set
        {
            var normalized = NormalizeCode(value);
            if (_code == normalized)
            {
                return;
            }

            _code = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CodeSizeText));
        }
    }

    /// <summary>可选的单行说明。</summary>
    public string Remark
    {
        get => _remark;
        set
        {
            if (_remark == value)
            {
                return;
            }

            _remark = value;
            OnPropertyChanged();
        }
    }

    /// <summary>禁用时保存为注释行，重新启用后原样恢复，不会丢失代码格式。</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StateText));
        }
    }

    /// <summary>
    /// 删除按钮是否处于“确认删除？”状态（仅 UI 状态，不写入配置文件）。
    /// </summary>
    public bool DeletePending
    {
        get => _deletePending;
        set
        {
            if (_deletePending == value)
            {
                return;
            }

            _deletePending = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDeleteIdle));
        }
    }

    public bool IsDeleteIdle => !_deletePending;

    /// <summary>
    /// 是否为随区块固定写入的内置命令（如 pls / ongit / sudo）：在列表里展示，但不可编辑、删除或切换启停，
    /// 保存时也不会写入快照（内置命令总是追加最新版本）。
    /// </summary>
    public bool IsBuiltin { get; internal set; }

    public string StateText => LocalizationService.Instance.T(_isEnabled ? "已启用" : "已禁用");

    /// <summary>语言切换后由 ViewModel 调用，通知界面重新读取本地化文本。</summary>
    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(CodeSizeText));
    }

    /// <summary>列表里显示的非空代码行数，用于快速辨认这段命令的规模。</summary>
    public string CodeSizeText
    {
        get
        {
            var count = _code.Split('\n').Count(static line => !string.IsNullOrWhiteSpace(line));
            return LocalizationService.Instance.F("{0} 行代码", count);
        }
    }

    public static string NormalizeCode(string? code)
        => (code ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

    /// <summary>规范化并去掉首尾空行，保证内存中的代码与写回文件的形态一致。</summary>
    public static string NormalizeCodeBlock(string? code)
    {
        var lines = NormalizeCode(code).Split('\n').ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join("\n", lines);
    }

    public CustomCommandEntry Clone() => new(_name, _code, _isEnabled, _remark, IsBuiltin);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
