using PAM.Core.Models;

namespace PAM.Core;

/// <summary>
/// 维护 $PROFILE 中的「别名启动器」标记块：将 PAM 托管的别名调用包装为同名函数，
/// 由 <c>Invoke-PamAlias</c> 统一处理独立进程启动与备注输出。
///
/// <para><c>ProfileService</c> 写出的 <c>Set-Alias</c> 行仍是数据源，用于兼容既有配置、解析逻辑与测试。
/// PowerShell 中 Alias 优先于 Function，因此块内先移除同名别名，再定义同名函数。</para>
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

    /// <summary>按当前别名快照重写启动器块；空列表时整体移除。</summary>
    public static void Sync(string profilePath, IEnumerable<AliasEntry> entries)
    {
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            throw new ArgumentException("配置文件路径不能为空。", nameof(profilePath));
        }

        var snapshot = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .ToList();

        var document = File.Exists(profilePath)
            ? ProfileDocument.Read(profilePath)
            : ProfileDocument.CreateNew();

        // 移除旧的启动器块（含标记行）；未闭合的块一直删到文件末尾，和 PAM-Custom 的容错一致。
        var lines = new List<string>(document.Lines.Count);
        var inside = false;
        foreach (var line in document.Lines)
        {
            if (IsStart(line))
            {
                inside = true;
                continue;
            }

            if (inside)
            {
                if (IsEnd(line)) inside = false;
                continue;
            }

            lines.Add(line);
        }

        // 反复保存不累积尾部空行。
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (snapshot.Count > 0)
        {
            if (lines.Count > 0) lines.Add(string.Empty);
            lines.Add(StartMarker);
            lines.Add("# 该区块由 PowerShell 管理器自动维护，请勿手动修改标记行。");
            lines.AddRange(HelperFunctionLines);
            lines.Add(string.Empty);

            foreach (var entry in snapshot)
            {
                var name = entry.Name.Trim();
                var path = NormalizePath(entry.Path);
                var remark = ProfileService.NormalizeRemark(entry.Remark);

                lines.Add($"Remove-Item -Path {ProfileService.QuoteForPowerShell("Alias:" + name)} -ErrorAction SilentlyContinue");
                lines.Add($"function {name} {{ Invoke-PamAlias -Path {ProfileService.QuoteForPowerShell(path)} -Remark {ProfileService.QuoteForPowerShell(remark)} -Rest $args }}");
            }

            lines.Add(EndMarker);
        }

        new ProfileDocument(lines, document.Encoding, document.NewLine).WriteTo(profilePath);
    }

    private static bool IsStart(string line)
        => line.TrimStart().Equals(StartMarker, StringComparison.OrdinalIgnoreCase);

    private static bool IsEnd(string line)
        => line.TrimStart().Equals(EndMarker, StringComparison.OrdinalIgnoreCase);

    private static string NormalizePath(string? path)
    {
        var value = (path ?? string.Empty).Trim().Trim('"');
        return value;
    }
}
