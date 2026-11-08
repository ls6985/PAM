using System.Diagnostics;
using System.IO;
using System.Text;

namespace PAM.Core;

/// <summary>
/// 美化功能的 Starship 侧服务：探测/安装 Starship（优先 Scoop）、把内置主题预设写入
/// <c>starship.toml</c>（写前自动备份）、并保证 $PROFILE 里有 starship init 行。
/// <para>
/// $PROFILE 中的受控区块使用自有标记对，<b>不要</b>放进 PAM-Custom 区块——那里会被
/// CustomCommandService 整块重写：
/// </para>
/// <code>
/// # &gt;&gt;&gt; PAM Starship &gt;&gt;&gt;
/// Invoke-Expression (&amp;starship init powershell)
/// # &lt;&lt;&lt; PAM Starship &lt;&lt;&lt;
/// </code>
/// </summary>
public sealed partial class StarshipService
{
    public const string BlockStartMarker = "# >>> PAM Starship >>>";
    public const string BlockEndMarker = "# <<< PAM Starship <<<";
    public const string InitLine = "Invoke-Expression (&starship init powershell)";

    private const string InitLineKeyword = "starship init";
    private const string ConfigFileName = "starship.toml";
    private const string BackupSuffix = ".bak";

    /// <summary>测试与特殊部署用的配置路径覆盖；为 null 时走 STARSHIP_CONFIG / 默认路径。</summary>
    private readonly string? _configPathOverride;

    /// <summary>测试注入专用：让 starship.toml 的读写落在指定目录，不碰真实用户配置。</summary>
    public StarshipService(string configPathOverride) => _configPathOverride = configPathOverride;

    public StarshipService()
    {
    }
}
