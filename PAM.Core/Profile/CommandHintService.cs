namespace PAM.Core;

/// <summary>
/// 维护 $PROFILE 中的「未知命令提示」标记块：接管 <c>CommandNotFoundAction</c>，
/// 在命令查找失败时以一行引导文案替代 PowerShell 的默认错误输出。
///
/// <para>开关与界面语言在每次触发时从 settings.json 读取，修改设置无需重写 $PROFILE。
/// 提示由 <c>CommandScriptBlock</c> 在命令执行阶段输出，只做命令查找的调用不会显示提示。</para>
///
/// <para><see cref="BuildLines"/> 只产出片段文本，由 <c>ProfileRenderer</c> 统一写入托管区。</para>
/// </summary>
public static class CommandHintService
{
    public const string StartMarker = "# >>> PAM Command Hint >>>";
    public const string EndMarker = "# <<< PAM Command Hint <<<";

    private static readonly string[] HandlerLines =
    [
        @"$ExecutionContext.InvokeCommand.CommandNotFoundAction = {",
        @"    param($CommandName, $CommandLookupEventArgs)",
        @"    $pamHintEnabled = $true",
        @"    $pamHintEnglish = $false",
        @"    try {",
        @"        $pamConfigPath = Join-Path $env:APPDATA 'PAM\settings.json'",
        @"        if (Test-Path -LiteralPath $pamConfigPath) {",
        @"            $pamConfig = Get-Content -LiteralPath $pamConfigPath -Raw | ConvertFrom-Json",
        @"            if ($null -ne $pamConfig.friendlyCommandNotFound) {",
        @"                $pamHintEnabled = [bool] $pamConfig.friendlyCommandNotFound",
        @"            }",
        @"            if ($null -ne $pamConfig.language) {",
        @"                $pamHintEnglish = ([string] $pamConfig.language) -eq 'en-US'",
        @"            }",
        @"        }",
        @"    } catch { }",
        @"    if (-not $pamHintEnabled) { return }",
        @"    $pamHintText = '未找到这个命令。输入 pls 查看已有别名，或在「PowerShell 管理器」里新建一个。'",
        @"    if ($pamHintEnglish) {",
        @"        $pamHintText = ""Command not found. Run 'pls' to list aliases, or add one in PowerShell Manager.""",
        @"    }",
        @"    $CommandLookupEventArgs.CommandScriptBlock = {",
        @"        Write-Host $pamHintText -ForegroundColor Cyan",
        @"    }.GetNewClosure()",
        @"    $CommandLookupEventArgs.StopSearch = $true",
        @"}",
    ];

    /// <summary>生成未知命令提示片段的全部文本行，不读写文件。</summary>
    public static List<string> BuildLines()
    {
        var lines = new List<string>
        {
            StartMarker,
            "# 该区块由 PowerShell 管理器自动维护，请勿手动修改标记行。",
        };

        lines.AddRange(HandlerLines);
        lines.Add(EndMarker);
        return lines;
    }
}
