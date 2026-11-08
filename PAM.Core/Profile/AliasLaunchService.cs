using PAM.Core.Models;

namespace PAM.Core;

/// <summary>
/// 维护 $PROFILE 里的「别名启动器」自有标记块：把 PAM 托管别名的调用改成同名函数包装，
/// 由公共函数 <c>Invoke-PamAlias</c> 统一负责独立进程启动与打印备注。
///
/// 为什么保留 ProfileService 写出的 Set-Alias 行：那仍是唯一数据源，也保证旧配置、解析逻辑
/// 与既有测试不变；但 PowerShell 里 Alias 优先级高于 Function，所以块内先 Remove-Item 删掉
/// 同名别名，再定义同名 function 才会生效。
///
/// 「关闭终端连带关掉软件」的根因：Start-Process 只保证 PowerShell 不阻塞等待，子进程仍在
/// 终端的控制台 / 进程树里；Electron 等程序还会 AttachConsole(ATTACH_PARENT_PROCESS) 把日志
/// 刷回终端。把 stdout/stderr 重定向到 NUL 只能掐掉日志，控制台归属没变。
///
/// 现在的做法：借 explorer.exe 代启动。explorer 不属于任何终端控制台，由它 ShellExecute 启动
/// 的程序父进程就是 explorer，完全脱离终端进程树 → 关终端无影响、日志自然不显示、UAC 提升
/// 照常，也不需要重定向（回到 ShellExecute 路径，实测启动耗时反而略快）。无参数时直接把
/// exe 路径交给 explorer；带参数时先生成一个临时 .lnk 快捷方式（WScript.Shell COM）再交给
/// explorer，参数原样传入。.lnk 用路径 hash 固定文件名、每次覆盖复用，不删除以免 explorer
/// 还没读取就被删掉；实测删除快捷方式后进程仍稳定存活。
///
/// 开关值不由本服务烘焙：公共函数运行时读 settings.json，改设置即时生效，无需重写 $PROFILE。
/// 标记块放在 PAM-Custom 之外，避免被 CustomCommandService 整块重写。
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
        @"    # 无参数：直接把路径交给 explorer.exe，由它 ShellExecute 启动。",
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
        @"    # 兜底：路径不存在 / COM 不可用 / explorer 失败时仍要能把程序拉起来。",
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
                var remark = NormalizeRemark(entry.Remark);

                lines.Add($"Remove-Item -Path {Quote("Alias:" + name)} -ErrorAction SilentlyContinue");
                lines.Add($"function {name} {{ Invoke-PamAlias -Path {Quote(path)} -Remark {Quote(remark)} -Rest $args }}");
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

    private static string NormalizeRemark(string? remark)
        => (remark ?? string.Empty)
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

    /// <summary>与 ProfileService 一致的转义：反引号、美元符号、双引号。</summary>
    private static string Quote(string value)
    {
        var escaped = value
            .Replace("`", "``", StringComparison.Ordinal)
            .Replace("$", "`$", StringComparison.Ordinal)
            .Replace("\"", "`\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}
