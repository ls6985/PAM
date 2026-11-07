using PAM.Core.Models;

namespace PAM.Core;

/// <summary>
/// 维护 $PROFILE 里的「别名启动器」自有标记块：把 PAM 托管别名的调用改成同名函数包装，
/// 由公共函数 <c>Invoke-PamAlias</c> 统一负责静默日志、独立进程启动与打印备注。
///
/// 为什么保留 ProfileService 写出的 Set-Alias 行：那仍是唯一数据源，也保证旧配置、解析逻辑
/// 与既有测试不变；但 PowerShell 里 Alias 优先级高于 Function，所以块内先 Remove-Item 删掉
/// 同名别名，再定义同名 function 才会生效。
///
/// 静默日志的关键：Start-Process 的子进程仍挂在父 PowerShell 的控制台上，Electron 等程序写到
/// stdout/stderr 的日志照样会刷屏；所以增强模式把两个流重定向到 NUL 设备（PowerShell 不允许
/// 二者同路径，故用 'NUL' 与 '\\.\NUL' 两种写法），失败时退回普通 Start-Process。
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
        "function Invoke-PamAlias {",
        "    param(",
        "        [string] $Path,",
        "        [string] $Remark,",
        "        [string[]] $Rest",
        "    )",
        "    $enhanced = $true",
        "    $showRemark = $true",
        "    try {",
        "        $configPath = Join-Path $env:APPDATA 'PAM\\settings.json'",
        "        if (Test-Path -LiteralPath $configPath) {",
        "            $cfg = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json",
        "            if ($null -ne $cfg.aliasLaunchEnhanced) { $enhanced = [bool] $cfg.aliasLaunchEnhanced }",
        "            if ($null -ne $cfg.showAliasRemarkOnLaunch) { $showRemark = [bool] $cfg.showAliasRemarkOnLaunch }",
        "        }",
        "    } catch { }",
        "    if ($showRemark -and $Remark) { Write-Host $Remark -ForegroundColor Cyan }",
        "    if ($enhanced) {",
        "        # Start-Process 仍是父 PowerShell 的子进程，会继承同一个控制台；Electron 等程序",
        "        # 把自己的日志写到 stdout/stderr，所以必须把这两个流重定向到 NUL 设备才真正静默。",
        "        # PowerShell 不允许 stdout / stderr 指向同一路径，故用 'NUL' 与 '\\\\.\\NUL' 两个写法。",
        "        try {",
        "            if ($Rest) { Start-Process -FilePath $Path -ArgumentList $Rest -RedirectStandardOutput 'NUL' -RedirectStandardError '\\\\.\\NUL' -ErrorAction Stop }",
        "            else { Start-Process -FilePath $Path -RedirectStandardOutput 'NUL' -RedirectStandardError '\\\\.\\NUL' -ErrorAction Stop }",
        "        } catch {",
        "            # 需要 UAC 提升 / 非 exe 目标在 UseShellExecute=false 下会失败，退回普通启动。",
        "            if ($Rest) { Start-Process -FilePath $Path -ArgumentList $Rest }",
        "            else { Start-Process -FilePath $Path }",
        "        }",
        "    } else {",
        "        if ($Rest) { & $Path @Rest }",
        "        else { & $Path }",
        "    }",
        "}",
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
