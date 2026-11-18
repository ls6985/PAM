using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using UiFluentWindow = Wpf.Ui.Controls.FluentWindow;

namespace PAM.App.Views;

/// <summary>
/// 帮助文档窗口：与「示例 / 指南」同款二级弹窗，左侧可折叠分组目录 + 右侧章节详情。
/// 文档全部写在下面的 Sections 静态表里：增删章节 / 改文案只动这一处，
/// 目录分组按 Section.Group 自动归类，排版由 XAML 统一负责。
/// TODO: 这些中文文案尚未注册 LocKey，做英文切换时需同步 PAM.Core/LocKey.cs。
/// </summary>
public partial class HelpDocWindow : UiFluentWindow
{
    private static readonly HelpSection[] Sections =
    [
        // ---------- 快速上手 ----------
        new("快速上手", "第一次打开",
            "从打开软件到在终端里用上别名，只需要三步。",
            [
                new("启动程序", "双击 PAM.App.exe 打开主界面，程序会静默拉起同目录的 PAM.Tray.exe 后台托盘。托盘负责全局热键，平时不需要手动启动，也不用管它。"),
                new("运行环境", "需要 Windows 10/11、.NET 10 桌面运行时、系统自带的 Windows PowerShell 5.1，以及 Windows Terminal（wt.exe）。PowerShell 7 是可选的，没装会自动回退到 5.1 并在设置页提示。"),
                new("改动何时生效", "别名、命令、美化都会写进 PowerShell $PROFILE 或对应配置文件，必须新开一个终端窗口才生效；已经开着的窗口不会自动刷新。"),
                new("先做什么", "建议先在「别名启动」里加一条常用程序，保存后新开终端输入别名试一下；再按需要配自定义命令和美化。"),
            ]),
        new("快速上手", "五个页签",
            "顶部页签按用途分工，切换页签不会丢失当前编辑状态。",
            [
                new("别名启动", "把常用程序登记成短别名，支持搜索、新建、改名、删除与路径浏览；编辑条里的「高级选项」可设置每次启动固定附带的参数。"),
                new("自定义命令", "给常用操作起一个命令名，写命令体即可。工具栏「示例 / 指南」里有六条可直接复制的常用命令。"),
                new("美化", "配置 Starship 提示符主题、安装内置 Nerd Font 字体，以及 Windows Terminal 的字体、透明度、背景图等外观。"),
                new("设置", "主题、语言、默认终端、全局热键、开机自启、托盘行为与终端输入相关开关；「恢复默认」会把设置还原。"),
                new("关于", "版本信息与更新检测、帮助文档、问题反馈、开源许可与赞助入口。"),
            ]),
        new("快速上手", "快捷键与热键",
            "编辑时少用鼠标，全局唤起终端也不用找图标。",
            [
                new("编辑快捷键", "Ctrl+N 新建、Ctrl+S 保存、Enter 确认、Esc 取消；自定义命令页的内联编辑条用 Ctrl+Enter 确认（因为内容允许多行）。"),
                new("全局热键", "默认 Ctrl+Alt+T，按下后由托盘拉起 Windows Terminal。可在设置页修改，修改前会做系统保留、真实占用与常见软件冲突检测，通过后再点「应用热键」。"),
                new("热键没反应", "先确认设置页里托盘进程是否在运行，或换个组合键试试——被别的软件占用时注册会失败。"),
            ]),

        // ---------- 功能说明 ----------
        new("功能说明", "别名启动",
            "用短名字启动程序，本质是往 $PROFILE 托管区生成包装函数。",
            [
                new("新建一条别名", "填「别名」「程序路径」「备注（可选）」，点保存即可。路径可以点浏览按钮选择，列表里过长路径会中间省略显示。保存时同名冲突会在底部状态栏提示。"),
                new("高级选项：启动参数", "编辑条底部默认收起，展开后可填每次启动都固定附带的参数；这些参数排在终端临时参数之前。每次重新打开编辑条都会恢复为收起状态。"),
                new("命名限制", "pls / ongit / sudo 是内置命令名，不能用作别名或自定义命令名，避免被区块末尾的内置函数覆盖。"),
                new("启动方式与开关", "别名默认借 explorer.exe 脱离终端进程树启动，可在设置页关闭；还可以选择启动前打印一行备注、以及用友好提示替代 PowerShell 的「命令未找到」红色报错。"),
                new("路径失效与修复", "程序被移动或删除后，列表里会显示感叹号提示。若提示执行策略受限，用别名页或设置页的「尝试修复」即可。删除别名需要二次确认。"),
            ]),
        new("功能说明", "自定义命令",
            "命令名就是终端里的调用入口，只写命令体，不用写 function 外壳。",
            [
                new("怎么写", "例如命令名填 gst，代码填 git status，保存后在终端输入 gst 即可。程序会自动把它包成同名函数；旧数据里完整的 function 外壳会被剥掉。"),
                new("内置命令", "pls / ongit / sudo 只读展示，带「内置」徽章，不能编辑、删除或停用，随每次保存自动维护，没有独立开关。"),
                new("启用状态", "列表里的状态胶囊点一下即可切换启用 / 停用；列表按名称、状态、备注、操作四列展示。"),
                new("高级选项：命令参数", "默认收起，展开后有「允许在命令名后传入参数」开关和「固定参数」输入框。关闭开关时函数体里仍然可以直接用 $args。打开已配置参数的命令时会自动展开。"),
                new("示例 / 指南", "工具栏按钮打开一个二级窗口，内含六条常用命令示例，每条都能一键复制；复制结果写在窗口底部状态文字里。"),
            ]),
        new("功能说明", "终端美化",
            "提示符与终端外观分开管，主题预设和自定义提示符是显式二选一。",
            [
                new("Starship 主题", "可以选择六个官方主题预设，也可以自己写自定义提示符片段；两者不会叠加，界面上同一时间只出现一套设置。"),
                new("字体", "一键安装内置的 Cascadia Code NF 字体，避免提示符里的图标显示成方块。字体下拉位于「高级选项」的外观子卡里。"),
                new("终端外观", "Windows Terminal 的字体、复古效果、光标、背景图、透明度、亚克力等属性写在 profiles.defaults 里；高级选项默认收起，只提交与读盘快照不同的项。"),
                new("通用设置与按钮", "「通用设置」里的「提示符前空一行」「路径分隔符用 /」对两种方案都生效；底部「恢复默认」「放弃更改」「保存设置」对两种方案共用。"),
                new("备份语义", "starship.toml 的 .bak 是最近一次应用主题前的单槽备份，恢复后保留；Windows Terminal 的 settings.json.bak 是本软件首次修改前的原始快照，永不覆盖，可反复恢复。"),
                new("配置维护", "第三张卡片展示 starship.toml 的完整路径，并提供「打开 starship.toml」「恢复上次主题设置」「恢复终端备份」三个入口。"),
            ]),
        new("功能说明", "设置与托盘",
            "主界面写配置、后台托盘读配置，两边各司其职。",
            [
                new("主题与语言", "亮 / 暗 / 跟随系统；简体中文 / English 立即生效。"),
                new("默认终端", "pwsh / powershell / auto，决定热键唤醒哪个 shell。数据文件始终是同一份；保存时 PowerShell 7 与 5.1 两份 $PROFILE 会各写一份托管区，所以任意终端里都能用。"),
                new("热键与托盘", "热键需通过冲突检测后点「应用热键」；其余设置改动即时落盘并通知托盘。关闭主窗口后托盘与热键默认保留，可在设置页改；也能在那里查看 / 启动 / 结束托盘。"),
                new("开机自启", "默认开启，写的是 HKCU Run 项里的 PAM.Tray 值，指向同目录的 PAM.Tray.exe。程序目录移动后，打开一次主界面就会自动修正路径。"),
                new("终端输入", "默认把新开的终端窗口切成英文输入模式（不换输入法与键盘布局）；相关日志在 %AppData%\\PAM\\ime-switch.log。"),
                new("恢复默认", "二次确认后把整份 settings.json 恢复为默认值；别名与自定义命令的数据不受影响。"),
            ]),

        // ---------- 数据与排障 ----------
        new("数据与排障", "数据与文件",
            "所有数据都在本机，知道位置就能自己备份或检查。",
            [
                new("主数据", "别名与自定义命令存于 %AppData%\\PAM\\pam-data.json，是唯一数据源；设置存于 %AppData%\\PAM\\settings.json，由主界面唯一写入。"),
                new("$PROFILE 托管区", "写在「# >>> PAM Managed >>>」两个标记之间，由程序按 pam-data.json 单向全量生成。区块外你自己写的内容（starship init、手动命令等）原样保留，不会被覆盖。"),
                new("两份配置文件", "PowerShell 7 的 Documents\\PowerShell\\ 与 5.1 的 Documents\\WindowsPowerShell\\ 是两个不同文件，程序会给已安装的版本各写一份。"),
                new("美化相关文件", "Starship 主题在 ~\\.config\\starship.toml（应用前备份为 .bak）；终端外观在 Windows Terminal 的 settings.json（首次修改前备份为 settings.json.bak）。"),
                new("手工改配置", "托盘对 settings.json 有文件时间戳轮询兜底，手工编辑后也会生效。"),
            ]),
        new("数据与排障", "常见问题",
            "遇到问题时，按下面顺序排查基本都能解决。",
            [
                new("改了不生效", "保存后新开一个终端窗口。$PROFILE 只在终端启动时读取，已开着的窗口不会自动更新。"),
                new("提示执行策略受限", "用别名页或设置页的「尝试修复」。"),
                new("别名启动失败 / 报命令未找到", "先检查程序路径是否还存在（列表会有感叹号提示），再确认新开了终端窗口。"),
                new("提示符图标是方块", "在美化页安装内置的 Cascadia Code NF 字体，然后重开终端。"),
                new("热键或托盘异常", "在设置页查看托盘状态；热键可能被其他软件占用，换个组合键再应用。"),
                new("移动过程序目录", "打开一次主界面即可自动修正登录启动项里的路径。"),
            ]),
        new("数据与排障", "隐私与清理",
            "本地运行、开源免费、不收集任何隐私数据。",
            [
                new("数据不上传", "程序不向任何服务器发送你的别名、命令、路径或输入内容；所有读写都发生在 %AppData%\\PAM、$PROFILE 与终端配置文件这些本机位置。"),
                new("只想重置设置", "用设置页的「恢复默认」，它只还原 settings.json，别名和命令数据会保留。"),
                new("想彻底清理", "手动删除 %AppData%\\PAM、$PROFILE 里两个标记之间的托管区、starship.toml 里的托管块，以及需要时恢复 Windows Terminal 的 settings.json.bak。区块外的自写内容不会被程序动过。"),
                new("还能怎么帮忙", "关于页里有帮助文档、问题反馈、开源许可与赞助入口；提交反馈或把工具推荐给朋友，同样很有帮助。"),
            ]),
    ];

    private static readonly HelpGroup[] Groups = Sections
        .GroupBy(s => s.Group)
        .Select(g => new HelpGroup(g.Key, g.ToArray()))
        .ToArray();

    private readonly List<Button> _tocButtons = [];
    private Button? _selectedButton;

    public HelpDocWindow()
    {
        InitializeComponent();
        BuildToc();
        SectionCountText.Text = $"共 {Groups.Length} 组 · {Sections.Length} 个章节，点击左侧目录切换";
        if (_tocButtons.Count > 0)
            SelectTocButton(_tocButtons[0]);
    }

    /// <summary>按分组生成左侧目录：分组标题可折叠，条目点击切换右侧内容。</summary>
    private void BuildToc()
    {
        foreach (var group in Groups)
        {
            var label = new TextBlock
            {
                Text = group.Name,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");

            var header = new ToggleButton
            {
                Content = label,
                IsChecked = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Style = (Style)FindResource("TocGroupToggleStyle")
            };

            var items = new StackPanel { Margin = new Thickness(8, 2, 0, 10) };
            foreach (var section in group.Sections)
            {
                var button = new Button
                {
                    Content = section.Title,
                    Tag = section,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Style = (Style)FindResource("TocItemButtonStyle")
                };
                button.SetResourceReference(Control.ForegroundProperty, "TextFillColorSecondaryBrush");
                button.Click += TocItem_Click;
                items.Children.Add(button);
                _tocButtons.Add(button);
            }

            header.Checked += (_, _) => items.Visibility = Visibility.Visible;
            header.Unchecked += (_, _) => items.Visibility = Visibility.Collapsed;

            TocPanel.Children.Add(header);
            TocPanel.Children.Add(items);
        }
    }

    private void TocItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is HelpSection)
            SelectTocButton(button);
    }

    private void SelectTocButton(Button button)
    {
        if (_selectedButton is not null)
        {
            _selectedButton.Background = Brushes.Transparent;
            _selectedButton.SetResourceReference(Control.ForegroundProperty, "TextFillColorSecondaryBrush");
            _selectedButton.FontWeight = FontWeights.Normal;
        }

        _selectedButton = button;
        button.SetResourceReference(Control.BackgroundProperty, "ControlFillColorSecondaryBrush");
        button.SetResourceReference(Control.ForegroundProperty, "TextFillColorPrimaryBrush");
        button.FontWeight = FontWeights.SemiBold;

        ShowSection((HelpSection)button.Tag);
    }

    private void ShowSection(HelpSection section)
    {
        DetailTitle.Text = section.Title;
        DetailSummary.Text = section.Summary;
        DetailList.ItemsSource = section.Items;
        DetailScroll.ScrollToTop();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>一条要点：小标题 + 说明。</summary>
    public sealed record HelpItem(string Heading, string Text);

    /// <summary>一个章节：所属分组、标题、摘要与要点列表。</summary>
    public sealed record HelpSection(string Group, string Title, string Summary, HelpItem[] Items);

    /// <summary>一个可折叠的目录分组。</summary>
    public sealed record HelpGroup(string Name, HelpSection[] Sections);
}
