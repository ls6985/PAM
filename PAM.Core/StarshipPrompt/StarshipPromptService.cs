using System.Text.RegularExpressions;

namespace PAM.Core;

/// <summary>
/// 自定义 Starship 提示符：用户在美化页编辑**一整行** Starship <c>format</c> 语法
/// （例如 <c>[$username](fg:#BBC4B9)[$hostname](fg:#BBC4B9)…</c>），本服务负责把它写进
/// starship.toml 的自有标记块，并提供下拉变量清单、色板、预览解析与备份恢复。
///
/// <para>为什么用变量而不是固定文字：用户名、主机名、当前路径是 Starship 运行时读取的值，
/// 写死成 Black / ~ 会导致换个目录提示符还显示 ~。变量清单见 <see cref="StarshipVariables" />。
/// 固定装饰符（@ : $ 空格）仍可当纯文字写进 format，也可以在编辑器里直接改颜色。</para>
///
/// <para>为什么不整段覆盖 / 不引入 TOML 解析器：用户已有的 starship.toml 里还有
/// <c>add_newline</c>、注释以及 <c>[username]</c>/<c>[hostname]</c>/<c>[directory]</c>/
/// <c>[character]</c> 等段落，且顶层 <c>format</c> 是 <c>"""\</c> 多行字符串、
/// 段落内又有同名局部 format。本服务只做「移除顶层 format → 在原位置插入自有标记块」这一处最小改动，
/// 其余所有行原样保留。这些段内 format 自带 @ / " : " / 空格等装饰，所以默认 format 引用四个
/// 变量就能继续渲染出 Black@DESKTOP-J64G5VA :~ $，且路径会随 cd 变化。</para>
///
/// <para>备份用独立的 <c>starship.toml.pam-prompt.bak</c> 且**只在首次写入时创建**，
/// 不与主题预设的 <c>starship.toml.bak</c> 抢同一个槽位，也不会被连续两次应用覆盖掉原始配置。
/// 配置文件本身即数据源，因此不需要在 settings.json 里再存一份。</para>
/// </summary>
public sealed partial class StarshipPromptService
{
    public const string StartMarker = "# >>> PAM Prompt >>>";
    public const string EndMarker = "# <<< PAM Prompt <<<";
    public const string NoticeLine = "# 该区块由 PowerShell 管理器自动维护，请勿手动修改标记行。";

    private const string BackupSuffix = ".pam-prompt.bak";
    private const int MaxFormatLength = 4096;

    // 预览用：匹配 [内容](样式)，样式可能是 fg:#RRGGBB、$style、bold 等。
    private static readonly Regex BlockRegex = new(
        @"\[((?:\\.|[^\[\]\\])*)\]\(([^()]*)\)",
        RegexOptions.Compiled);

    private static readonly Regex HexColorRegex = new(@"#[0-9A-Fa-f]{6}", RegexOptions.Compiled);
    private static readonly Regex FgColorRegex = new(@"fg:([^)\s]+)", RegexOptions.Compiled);
    private static readonly Regex HexValidation = new(@"^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    private readonly string? _configPathOverride;

    public StarshipPromptService()
    {
    }

    /// <summary>测试注入专用：让 starship.toml 的读写落在指定目录，不碰真实用户配置。</summary>
    public StarshipPromptService(string configPathOverride) => _configPathOverride = configPathOverride;

    public string GetConfigPath() => _configPathOverride ?? new StarshipService().GetConfigPath();

    public string GetBackupPath() => GetConfigPath() + BackupSuffix;
}
