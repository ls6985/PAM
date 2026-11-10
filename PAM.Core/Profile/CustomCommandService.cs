using PAM.Core.Models;

namespace PAM.Core;

/// <summary>
/// 生成 $PROFILE 托管区中的 PAM-Custom 片段，并维护内置命令定义表与名称校验。
/// 片段文本由 <see cref="ProfileRenderer"/> 统一写入，本类不读写文件。
/// </summary>
public sealed class CustomCommandService
{
    private const string EntryHeaderPrefix = "# @pam-custom";
    private const string EntryStatePrefix = "# @pam-custom-state";
    private const string EntryEndMarker = "# @pam-custom-end";

    // 去除 Invoke-PamAlias 对路径/备注做的 PowerShell 转义：反引号后跟任意字符时只保留该字符。
    // pls 解析别名路径/备注、sudo 解析别名路径时共用，避免三处重复同一段正则。
    // 用单行字符串数组，与 HelperFunctionLines / HandlerLines 保持一致，保证托管区始终为 LF 换行。
    private static readonly string[] UnescapeHelperLines =
    [
        "function __PamUnescape {",
        "    param([string] $Value)",
        "    if ($null -eq $Value -or $Value.Length -eq 0) { return '' }",
        "    return [regex]::Replace($Value, '`(.)', '$1')",
        "}",
    ];

    /// <summary>PowerShell 函数名里不允许出现的字符（空白单独判断）。</summary>
    private static readonly char[] ForbiddenNameChars =
    [
        '"', '\'', '`', '$', '{', '}', '(', ')', '[', ']', ';', ',', '|', '&',
        '#', '\\', '/', ':', '*', '?', '<', '>', '=', '+', '@'
    ];

    private sealed record BuiltinDefinition(string Name, string Remark, string Code);

    /// <summary>
    /// 内置命令定义表：新增内置命令只需在这里加一项，Save / Parse / 名称校验 /
    /// EnsureBuiltinPresent 会自动跟上。数组顺序即写入 $PROFILE 与界面列表的顺序。
    /// </summary>
    private static readonly BuiltinDefinition[] BuiltinCommands =
    [
        new("pls", "内置命令：列出全部别名", PlsCode),
        new("ongit", "内置命令：暂存全部改动并提交", OngitCode),
        new("sudo", "内置命令：以管理员身份启动程序或别名", SudoCode),
    ];

    private static readonly HashSet<string> BuiltinNameSet =
        new(BuiltinCommands.Select(static command => command.Name), StringComparer.OrdinalIgnoreCase);

    /// <summary>全部内置命令名（只读）。</summary>
    public static IReadOnlyCollection<string> BuiltinCommandNames => BuiltinNameSet;

    /// <summary>判断名称是否被内置命令占用（大小写不敏感）。</summary>
    public static bool IsBuiltinName(string? name)
        => !string.IsNullOrWhiteSpace(name) && BuiltinNameSet.Contains(name);

    // 运行时枚举别名启动器生成的包装函数，从 -Path 参数提取目标路径，
    // 按别名 / 路径 / 备注 三列输出；pls 与 Invoke-PamAlias 自身不匹配该模式。
    private const string PlsCode = """
        function pls {
            # PAM 内置命令：列出别名启动里注册的全部别名
            $rows = foreach ($cmd in (Get-Command -CommandType Function)) {
                $body = "$($cmd.ScriptBlock)"
                if (-not ($body -match 'Invoke-PamAlias\s+-Path\s+"(?<path>[^"]*)"')) { continue }
                $path = __PamUnescape($Matches['path'])
                $remark = ''
                if ($body -match '-Remark\s+"(?<remark>[^"]*)"') { $remark = __PamUnescape($Matches['remark']) }
                [pscustomobject]@{
                    别名 = $cmd.Name
                    路径 = $path
                    备注 = $remark
                }
            }
            if (-not $rows) {
                Write-Host '当前没有已注册的别名。' -ForegroundColor Yellow
                return
            }
            Write-Host ('共 {0} 个别名' -f @($rows).Count) -ForegroundColor Cyan
            $rows | Sort-Object -Property '别名' | Format-Table -AutoSize | Out-Host
        }
        """;

    // ongit <提交信息>：将全部参数合并为一条 -m 消息，先执行 git add -A，再执行 git commit。
    // 消息来自命令行参数；$PROFILE 由 ProfileFileStore 统一写成 UTF-8 BOM，PowerShell 5.1 可正确读取。
    private const string OngitCode = """
        function ongit {
            # PAM 内置命令：暂存全部改动，并把剩余参数合成一条提交信息
            $message = ($args -join ' ').Trim()
            if (-not $message) {
                Write-Host '用法：ongit <提交信息>，例如 ongit 首次提交' -ForegroundColor Yellow
                return
            }
            if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
                Write-Host '未找到 git，请先安装 Git for Windows。' -ForegroundColor Red
                return
            }
            git add -A
            if ($LASTEXITCODE -ne 0) {
                Write-Host 'git add 未成功，已中止提交（当前目录可能不是 git 仓库）。' -ForegroundColor Red
                return
            }
            git commit -m $message
            if ($LASTEXITCODE -ne 0) {
                Write-Host 'git commit 未成功，请查看上面的输出。' -ForegroundColor Yellow
            }
        }
        """;

    // sudo [-w] <程序或别名> [参数...]：通过 Start-Process -Verb RunAs 触发 UAC 提权。
    // 目标支持 PATH 上的 exe、完整路径、Set-Alias 别名，以及别名启动器生成的同名包装函数
    // （后者从函数体提取 -Path，与 pls 的扫描方式一致）。
    // 别名路径正则以字符串拼接方式构造，避免 sudo 函数体被 pls 的别名扫描模式匹配。
    private const string SudoCode = """
        function sudo {
            # PAM 内置命令：以管理员身份启动程序或 PAM 别名（UAC 由系统弹出）
            $items = @($args)
            $wait = $false
            if ($items.Count -gt 0 -and $items[0] -in @('-w', '--wait')) {
                $wait = $true
                $items = @($items | Select-Object -Skip 1)
            }
            if ($items.Count -eq 0) {
                Write-Host '用法：sudo [-w] <程序或别名> [参数...]' -ForegroundColor Yellow
                Write-Host '示例：sudo notepad  /  sudo blbl  /  sudo -w setup.exe' -ForegroundColor Yellow
                return
            }

            $name = [string] $items[0]
            $rest = @($items | Select-Object -Skip 1)

            $targetPath = $null
            $command = Get-Command -Name $name -ErrorAction SilentlyContinue
            if ($null -ne $command) {
                $kind = [string] $command.CommandType
                if ($kind -eq 'Function') {
                    # 正则以字符串拼接构造，避免本函数体被 pls 的别名扫描模式匹配。
                    $pattern = 'Invoke-PamAlias' + '\s+-Path\s+"(?<path>[^"]*)"'
                    $match = [regex]::Match("$($command.ScriptBlock)", $pattern)
                    if ($match.Success) {
                        $targetPath = __PamUnescape($match.Groups['path'].Value)
                    }
                }
                elseif ($kind -eq 'Alias') {
                    $targetPath = [string] $command.Definition
                }
                elseif ($kind -eq 'Application') {
                    $targetPath = [string] $command.Source
                }
            }
            if (-not $targetPath -and (Test-Path -LiteralPath $name)) {
                $targetPath = (Resolve-Path -LiteralPath $name).Path
            }
            if (-not $targetPath) {
                Write-Host ("找不到 {0}，也无法解析成程序路径。" -f $name) -ForegroundColor Red
                return
            }

            # -ArgumentList 数组会被空格拼接并丢掉外层引号，所以自己拼成单个字符串。
            $argLine = ($rest | ForEach-Object {
                $value = [string] $_
                if ($value -match '[\s"]') { '"' + ($value -replace '"', '\"') + '"' } else { $value }
            }) -join ' '

            $parameters = @{
                FilePath         = $targetPath
                Verb             = 'RunAs'
                WorkingDirectory = (Get-Location).Path
            }
            if ($argLine) { $parameters['ArgumentList'] = $argLine }
            if ($wait) { $parameters['Wait'] = $true }

            try {
                Start-Process @parameters
            }
            catch {
                Write-Host ("提权启动失败：{0}" -f $_.Exception.Message) -ForegroundColor Red
            }
        }
        """;


    /// <summary>
    /// 生成 PAM-Custom 受控区块的全部文本行，不读写文件。
    /// 内置命令按定义表固定追加最新版本，不来自数据快照。
    /// </summary>
    public static List<string> BuildLines(IEnumerable<CustomCommandData> commands)
    {
        var snapshot = commands
            .Where(static command => !string.IsNullOrWhiteSpace(command.Name) && !IsBuiltinName(command.Name))
            .Select(static command => new CustomCommandEntry(
                command.Name.Trim(),
                string.Join("\n", command.CodeLines ?? []),
                command.IsEnabled,
                command.Remark))
            .ToList();

        var lines = new List<string>
        {
            PamCustomRegion.Header,
            ProfileScriptSnippets.ManagedSectionNotice,
            string.Empty,
        };

        lines.AddRange(UnescapeHelperLines);

        var allEntries = snapshot
            .Concat(BuiltinCommands.Select(static command =>
                new CustomCommandEntry(command.Name, command.Code, true, command.Remark, true)))
            .ToList();

        for (var i = 0; i < allEntries.Count; i++)
        {
            if (i > 0)
            {
                lines.Add(string.Empty);
            }

            lines.AddRange(BuildEntryLines(allEntries[i]));
        }

        lines.Add(PamCustomRegion.Footer);
        return lines;
    }

    /// <summary>内置命令的只读展示条目，供界面列表使用；这些条目不写入数据文件。</summary>
    public static IReadOnlyList<CustomCommandEntry> GetBuiltinEntries()
        => BuiltinCommands
            .Select(static command =>
                new CustomCommandEntry(command.Name, command.Code, true, command.Remark, true))
            .ToList();

    /// <summary>校验命令名是否可作为 PowerShell 函数名使用。</summary>
    public static bool TryValidateName(string? name, out string error)
    {
        var value = (name ?? string.Empty).Trim();

        if (value.Length == 0)
        {
            error = LocalizationService.Instance.T("命令名称不能为空。");
            return false;
        }

        if (value.Any(char.IsWhiteSpace))
        {
            error = LocalizationService.Instance.T("命令名称不能包含空格。");
            return false;
        }

        if (value.IndexOfAny(ForbiddenNameChars) >= 0)
        {
            error = LocalizationService.Instance.T("命令名称包含 PowerShell 不支持的字符。");
            return false;
        }

        if (IsBuiltinName(value))
        {
            error = LocalizationService.Instance.F("{0} 是内置命令，请换一个名称。", value);
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static IEnumerable<string> BuildEntryLines(CustomCommandEntry entry)
    {
        var name = entry.Name.Trim();
        var remark = ProfileService.NormalizeRemark(entry.Remark);

        yield return remark.Length == 0
            ? $"{EntryHeaderPrefix} {name}"
            : $"{EntryHeaderPrefix} {name} {remark}";

        if (!entry.IsEnabled)
        {
            yield return $"{EntryStatePrefix} disabled";
        }

        var codeLines = CustomCommandEntry.NormalizeCode(entry.Code).Split('\n').ToList();
        TrimBlankEdges(codeLines);

        foreach (var line in codeLines)
        {
            if (entry.IsEnabled)
            {
                yield return line;
            }
            else
            {
                // 禁用状态逐行注释：空行写成 "#"，其余写成 "# 原行"。
                yield return line.Length == 0 ? "#" : "# " + line;
            }
        }

        yield return EntryEndMarker;
    }

    private static void TrimBlankEdges(List<string> lines)
    {
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }
    }
}
