using System.Text.RegularExpressions;

namespace PAM.Core;

/// <summary>
/// 自定义 Starship 提示符：用户在美化页用「点选片段组合」编辑**一整行** Starship
/// <c>format</c>（例如 <c>[$username](fg:#BBC4B9)[$hostname](fg:#BBC4B9)…</c>），
/// 本服务负责把它写进 starship.toml 的自有标记块，并提供下拉变量清单、色板、预览解析与备份恢复。
///
/// <para>为什么用变量而不是固定文字：用户名、主机名、当前路径是 Starship 运行时读取的值，
/// 写死成 Black / ~ 会导致换个目录提示符还显示 ~。变量清单见 <see cref="StarshipVariables" />。</para>
///
/// <para>为什么不整段覆盖 / 不引入 TOML 解析器：配置里还有注释以及各模块段落（主题预设尤其如此），
/// 应当原样保留。托管块写**顶层 format + add_newline + format 里引用到的模块段落**：顶层 format 的
/// 外层 (fg:…) 只能染色裸文字，模块段落里的 format / 样式会盖过它，且 TOML 不允许重复定义表，
/// 所以被引用的段落整段移走、按段选色内嵌进模块自身 format 重写（模板见
/// <c>StarshipPromptService.Modules.cs</c>），块外的旧顶层 format / add_newline 也一并移走防重复键，
/// 其余段落 / 注释原样保留。add_newline（提示符前空一行）与 use_os_path_sep（/ 分隔符）随块写入；
/// 主题预设由 StarshipService 写入同样的键，两者都可由 StarshipService.ReadCommonOptions() 读回，
/// 不在 settings.json 里另存。</para>
///
/// <para>备份用独立的 <c>starship.toml.pam-prompt.bak</c> 且**只在首次写入时创建**，
/// 不与主题预设的 <c>starship.toml.bak</c> 抢同一个槽位，也不会被连续两次应用覆盖掉原始配置。
/// <c>Restore()</c> 恢复时删除备份，让"恢复应用前配置"按钮的可用性始终等于"有未撤销的备份"。
/// 配置文件本身即数据源，因此不需要在 settings.json 里再存一份。</para>
/// </summary>
public sealed partial class StarshipPromptService
{
    public const string StartMarker = "# >>> PAM Prompt >>>";
    public const string EndMarker = "# <<< PAM Prompt <<<";
    public const string NoticeLine = ProfileScriptSnippets.ManagedSectionNotice;

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
