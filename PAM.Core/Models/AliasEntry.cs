using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace PAM.Core.Models;

/// <summary>
/// 表示 $PROFILE 中的一条 Set-Alias 记录。
/// </summary>
public sealed class AliasEntry : INotifyPropertyChanged
{
    private string _name;
    private string _path;
    private string _remark;
    private bool _deletePending;

    public AliasEntry(string name, string path, string remark = "")
    {
        _name = name;
        _path = path;
        _remark = remark;
    }

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

    public string Path
    {
        get => _path;
        set
        {
            if (_path == value)
            {
                return;
            }

            _path = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PathExists));
            OnPropertyChanged(nameof(PathMissing));
        }
    }

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

    public bool PathExists => !string.IsNullOrWhiteSpace(_path) && File.Exists(_path);

    public bool PathMissing => !PathExists;

    public AliasEntry Clone() => new(_name, _path, _remark);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
