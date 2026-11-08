using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using PAM.Core;

namespace PAM.App.ViewModels;

/// <summary>
/// 字体功能：Windows Terminal 默认字体下拉、内置 Cascadia Code NF 安装、把选中字体写入 WT 配置。
/// 修改字体逻辑只看本文件。
/// </summary>
public sealed partial class BeautifyViewModel
{
    /// <summary>
    /// 字体候选。整体替换而不是 Clear+Add：ComboBox 对"原地改动"的集合不会重算
    /// 显示用 <c>SelectionBoxItem</c>（SelectedItem 正确、显示却空白），换新集合强制它重建。
    /// </summary>
    public ObservableCollection<string> FontOptions
    {
        get => _fontOptions;
        private set
        {
            if (ReferenceEquals(_fontOptions, value)) return;
            _fontOptions = value;
            OnPropertyChanged(nameof(FontOptions));
        }
    }

    public string? SelectedFont
    {
        get => _selectedFont;
        set
        {
            if (SetField(ref _selectedFont, value))
            {
                OnPropertyChanged(nameof(CanApplyFont));
                OnPropertyChanged(nameof(HasFontSelection));
                OnPropertyChanged(nameof(CanOneClickInstall));
            }
        }
    }

    public bool HasFontSelection => !string.IsNullOrWhiteSpace(_selectedFont);
    public bool IsBundledFontInstalled => _isBundledFontInstalled;

    public string CurrentFontText => _currentWtFont is null
        ? Loc.T("Windows Terminal 当前字体：未设置（跟随 WT 默认）")
        : Loc.F("Windows Terminal 当前字体：{0}", _currentWtFont);

    public bool CanInstallBundledFont => !IsBusy && !_isBundledFontInstalled;
    public bool CanApplyFont => !IsBusy && _hasWtSettings && !string.IsNullOrWhiteSpace(_selectedFont);

    public async Task InstallBundledFontAsync()
    {
        await RunBusyAsync(
            "正在安装内置字体…",
            () =>
            {
                var ttfPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", BundledFontFileName);
                return FontInstaller.InstallFont(ttfPath);
            },
            static result => new[]
            {
                (result.Success ? StatusType.Saved : StatusType.Error, result.Message),
            });
    }

    public async Task ApplyFontAsync()
    {
        var face = SelectedFont;
        if (string.IsNullOrWhiteSpace(face))
        {
            SetFeedback(StatusType.Warning, "请先选择一个字体。");
            return;
        }

        await RunBusyAsync(
            "正在写入 Windows Terminal 字体设置…",
            () => _terminalService.ApplyFont(face),
            static result => new[]
            {
                (result.Success ? StatusType.Saved : StatusType.Error, result.Message),
            });
    }
}
