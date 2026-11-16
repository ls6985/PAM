using System.Text;

namespace PAM.Core;

/// <summary>
/// 维护 $PROFILE 中的「别名启动器」标记块：将 PAM 托管的别名调用包装为同名函数，
/// 由 <c>Invoke-PamAlias</c> 统一处理独立进程启动与备注输出。
///
/// <para><see cref="BuildLines"/> 只产出片段文本，由 <c>ProfileRenderer</c> 统一写入托管区。
/// 用户区若已定义同名 alias 或 function，包装函数让位，不覆盖用户原有命令。</para>
///
/// <para>由 <c>Start-Process</c> 启动的子进程仍依附终端控制台与进程树；部分程序会通过
/// <c>AttachConsole(ATTACH_PARENT_PROCESS)</c> 回写日志。重定向 stdout/stderr 仅抑制输出，不改变控制台归属。
/// 本实现改由 <c>explorer.exe</c> 经 ShellExecute 代启动：其子进程不属于终端进程树，关闭终端不影响已启动的程序，
/// 且无需重定向，UAC 提升行为保持不变。</para>
///
/// <para>无参数时直接传递 exe 路径；带参数时生成临时 .lnk（WScript.Shell COM）承载参数。
/// .lnk 按路径哈希固定文件名、每次覆盖复用且不删除，以避免 explorer 异步读取时文件已移除。</para>
///
/// <para>别名可配置固定启动参数：生成包装函数时以数组字面量置于 -Rest 之前，
/// 终端调用时临时传入的 $args 接在其后，公共函数无需区分两类参数。</para>
///
/// <para>开关值在运行时由公共函数读取 settings.json，修改设置无需重写 $PROFILE。
/// 标记块位于 PAM-Custom 之外，不受 CustomCommandService 整块重写影响。</para>
///
/// <para>启用「按回车即隐藏」时，公共函数一进入就取当前前台窗口，按类名校验它是
/// Windows Terminal 后以 <c>ShowWindow(SW_HIDE)</c> 隐藏，随后在后台完成启动工作。
/// 会话不结束、窗口不关闭，托盘持有的窗口可由热键唤回。前台窗口按类名校验，
/// 避免误隐藏其它应用的窗口；全部动作在终端进程内完成，不依赖后台托盘。</para>
/// </summary>
public static class AliasLaunchService
{
    public const string StartMarker = "# >>> PAM Alias Launcher >>>";
    public const string EndMarker = "# <<< PAM Alias Launcher <<<";

    /// <summary>「别名进入即隐藏」片段的特征串，供托管区标记检查识别当前版本；生成与检查共用同一串。</summary>
    public const string HideOnAliasMarker = "$hideAfterLaunch = $false";

    private static readonly string[] HelperFunctionLines =
    [
        @"function Invoke-PamAlias {",
        @"    param(",
        @"        [string] $Path,",
        @"        [string] $Remark,",
        @"        [string[]] $Rest",
        @"    )",
        @"    $enhanced = $true",
        @"    $showRemark = $true",
        $@"    {HideOnAliasMarker}",
        @"    try {",
            ..ProfileScriptSnippets.ReadSettingsJsonLines("configPath", "cfg"),
        @"            if ($null -ne $cfg.aliasLaunchEnhanced) { $enhanced = [bool] $cfg.aliasLaunchEnhanced }",
        @"            if ($null -ne $cfg.showAliasRemarkOnLaunch) { $showRemark = [bool] $cfg.showAliasRemarkOnLaunch }",
        @"            if ($null -ne $cfg.hideTerminalAfterAliasLaunch) { $hideAfterLaunch = [bool] $cfg.hideTerminalAfterAliasLaunch }",
        @"        }",
        @"    } catch { }",
        @"    # 别名命令一进入就隐藏当前终端窗口；启动工作在后台继续，会话不结束。",
        @"    if ($hideAfterLaunch) {",
        @"        try {",
        @"            if (-not ('PAM.Win32' -as [type])) {",
        @"                Add-Type -Namespace PAM -Name Win32 -MemberDefinition '[System.Runtime.InteropServices.DllImport(""user32.dll"")] public static extern System.IntPtr GetForegroundWindow(); [System.Runtime.InteropServices.DllImport(""user32.dll"")] public static extern int GetClassName(System.IntPtr hWnd, System.Text.StringBuilder name, int max); [System.Runtime.InteropServices.DllImport(""user32.dll"")] public static extern bool ShowWindow(System.IntPtr hWnd, int cmd);' -ErrorAction Stop",
        @"            }",
        @"            $pamWindow = [PAM.Win32]::GetForegroundWindow()",
        @"            $pamClass = New-Object System.Text.StringBuilder 256",
        @"            [void][PAM.Win32]::GetClassName($pamWindow, $pamClass, $pamClass.Capacity)",
        @"            if ($pamClass.ToString() -eq 'CASCADIA_HOSTING_WINDOW_CLASS') {",
        @"                [void][PAM.Win32]::ShowWindow($pamWindow, 0)",
        @"            }",
        @"        } catch { }",
        @"    }",
        @"    if ($showRemark -and $Remark) { Write-Host $Remark -ForegroundColor Cyan }",
        @"    if (-not $enhanced) {",
        @"        if ($Rest) { & $Path @Rest } else { & $Path }",
        @"        return",
        @"    }",
        @"    # 无参数时将路径直接传给 explorer.exe，由 ShellExecute 启动。",
        @"    if (-not $Rest) {",
        @"        if (Test-Path -LiteralPath $Path) {",
        @"            try {",
        @"                Start-Process -FilePath 'explorer.exe' -ArgumentList ('""' + $Path + '""') -ErrorAction Stop",
        @"                return",
        @"            } catch { }",
        @"        }",
        @"    } elseif (Test-Path -LiteralPath $Path) {",
        @"        # 有参数：临时 .lnk 携带参数，同样由 explorer 代启动。文件名按路径固定，",
        @"        # 每次覆盖复用、不删除，避免 explorer 异步读取时文件已被删掉。",
        @"        try {",
        @"            $lnk = Join-Path $env:TEMP ('pam-alias-' + [Math]::Abs($Path.GetHashCode()) + '.lnk')",
        @"            $arguments = ($Rest | ForEach-Object {",
        @"                if ($_.Contains(' ')) { '""' + $_ + '""' } else { $_ }",
        @"            }) -join ' '",
        @"            $shell = New-Object -ComObject WScript.Shell",
        @"            $shortcut = $shell.CreateShortcut($lnk)",
        @"            $shortcut.TargetPath = $Path",
        @"            $shortcut.Arguments = $arguments",
        @"            $shortcut.WorkingDirectory = Split-Path -Parent $Path",
        @"            $shortcut.Save()",
        @"            Start-Process -FilePath 'explorer.exe' -ArgumentList ('""' + $lnk + '""') -ErrorAction Stop",
        @"            return",
        @"        } catch { }",
        @"    }",
        @"    # 回退路径：路径不存在、COM 不可用或 explorer 启动失败时，以 Start-Process 直接启动。",
        @"    if ($Rest) { Start-Process -FilePath $Path -ArgumentList $Rest }",
        @"    else { Start-Process -FilePath $Path }",
        @"}",
    ];

    /// <summary>
    /// 生成别名启动器片段的全部文本行，不读写文件。
    /// 同名命令由用户区优先：仅当用户尚未定义同名 alias 或 function 时才注册包装函数。
    /// </summary>
    public static List<string> BuildLines(IEnumerable<AliasData> aliases)
    {
        var snapshot = aliases
            .Where(static alias => !string.IsNullOrWhiteSpace(alias.Name))
            .ToList();

        if (snapshot.Count == 0)
        {
            return [];
        }

        var lines = new List<string>
        {
            StartMarker,
            ProfileScriptSnippets.ManagedSectionNotice,
        };

        lines.AddRange(HelperFunctionLines);
        lines.Add(string.Empty);

        foreach (var alias in snapshot)
        {
            var name = alias.Name.Trim();
            var path = NormalizePath(alias.Path);
            var remark = ProfileService.NormalizeRemark(alias.Remark);
            // 固定参数压成单行后再拆分，避免换行破坏生成出的函数定义。
            var arguments = SplitArguments(ProfileService.NormalizeRemark(alias.Arguments));

            // 用 function:/alias: 驱动的 Test-Path 替代 Get-Command，避免启动时为每个别名
            // 走命令发现管线；语义与原 -CommandType Alias,Function 一致，开销约两个数量级更小。
            lines.Add(
                $"if (-not (Test-Path \"function:{name}\") -and -not (Test-Path \"alias:{name}\")) {{");
            lines.Add(
                $"    function {name} {{ Invoke-PamAlias -Path {ProfileService.QuoteForPowerShell(path)} " +
                $"-Remark {ProfileService.QuoteForPowerShell(remark)} -Rest {BuildRestExpression(arguments)} }}");
            lines.Add("}");
        }

        lines.Add(EndMarker);
        return lines;
    }

    /// <summary>
    /// 生成包装函数的 -Rest 表达式。未配置固定参数时保持 $args，
    /// 否则以数组字面量开头，使固定参数排在终端临时参数之前。
    /// </summary>
    private static string BuildRestExpression(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return "$args";
        }

        var literals = string.Join(", ", arguments.Select(ProfileService.QuoteForPowerShell));
        return $"(@({literals}) + $args)";
    }

    /// <summary>
    /// 按命令行习惯拆分固定参数：以空白分隔，双引号内的空白属于同一参数，引号本身不保留。
    /// 参数按字面量生成，不做 PowerShell 变量展开。
    /// </summary>
    private static List<string> SplitArguments(string arguments)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        foreach (var ch in arguments)
        {
            if (ch == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (!quoted && char.IsWhiteSpace(ch))
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result;
    }

    private static string NormalizePath(string? path)
    {
        var value = (path ?? string.Empty).Trim().Trim('"');
        return value;
    }
}
