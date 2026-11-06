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

    public CustomCommandEntry(string name, string code, bool isEnabled = true, string remark = "")
    {
        _name = name;
        _code = NormalizeCode(code);
        _isEnabled = isEnabled;
        _remark = remark;
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
            OnPropertyChanged(nameof(PreviewText));
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

    public string StateText => _isEnabled ? "已启用" : "已禁用";

    /// <summary>列表里显示的第一行有效代码，用于快速辨认这段命令。</summary>
    public string PreviewText
    {
        get
        {
            foreach (var line in _code.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    return trimmed;
                }
            }

            return "（空代码）";
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

    public CustomCommandEntry Clone() => new(_name, _code, _isEnabled, _remark);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
