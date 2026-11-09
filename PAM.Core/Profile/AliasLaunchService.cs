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
/// <para>开关值在运行时由公共函数读取 settings.json，修改设置无需重写 $PROFILE。
/// 标记块位于 PAM-Custom 之外，不受 CustomCommandService 整块重写影响。</para>
/// </summary>
public static class AliasLaunchService
{
    public const string StartMarker = "# >>> PAM Alias Launcher >>>";
    public const string EndMarker = "# <<< PAM Alias Launcher <<<";

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
        @"    try {",
        @"        $configPath = Join-Path $env:APPDATA 'PAM\settings.json'",
        @"        if (Test-Path -LiteralPath $configPath) {",
        @"            $cfg = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json",
        @"            if ($null -ne $cfg.aliasLaunchEnhanced) { $enhanced = [bool] $cfg.aliasLaunchEnhanced }",
        @"            if ($null -ne $cfg.showAliasRemarkOnLaunch) { $showRemark = [bool] $cfg.showAliasRemarkOnLaunch }",
        @"        }",
        @"    } catch { }",
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
            "# 该区块由 PowerShell 管理器自动维护，请勿手动修改标记行。",
        };

        lines.AddRange(HelperFunctionLines);
        lines.Add(string.Empty);

        foreach (var alias in snapshot)
        {
            var name = alias.Name.Trim();
            var path = NormalizePath(alias.Path);
            var remark = ProfileService.NormalizeRemark(alias.Remark);

            lines.Add(
                $"if (-not (Get-Command -Name {ProfileService.QuoteForPowerShell(name)} " +
                "-CommandType Alias,Function -ErrorAction SilentlyContinue)) {");
            lines.Add(
                $"    function {name} {{ Invoke-PamAlias -Path {ProfileService.QuoteForPowerShell(path)} " +
                $"-Remark {ProfileService.QuoteForPowerShell(remark)} -Rest $args }}");
            lines.Add("}");
        }

        lines.Add(EndMarker);
        return lines;
    }

    private static string NormalizePath(string? path)
    {
        var value = (path ?? string.Empty).Trim().Trim('"');
        return value;
    }
}
