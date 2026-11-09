using System.IO;
using System.Text.RegularExpressions;
using PAM.Core.Models;

namespace PAM.Core;

public sealed record CustomCommandLoadResult(
    string ProfilePath,
    bool FileExists,
    IReadOnlyList<CustomCommandEntry> Entries);

public sealed record CustomCommandSaveResult(
    bool FileCreated,
    int EntryCount);

/// <summary>
/// 负责读写 $PROFILE 中由 PAM 独占维护的自定义命令区块。
/// <para>
/// 区块格式（受控，保存时整块重写，区块外的内容原样保留）：
/// </para>
/// <code>
/// #region PAM-Custom
/// # @pam-custom hello 打招呼
/// function hello { Write-Host "hi" }
/// # @pam-custom-end
/// #endregion
/// </code>
/// <para>
/// 禁用状态会写入 <c># @pam-custom-state disabled</c>，并把代码逐行注释掉；
/// 重新启用时去掉注释前缀即可完整还原，不会破坏缩进与空行。
/// </para>
/// <para>
/// 内置命令（<c>pls</c> / <c>ongit</c> / <c>sudo</c>）由定义表统一维护，每次保存固定追加最新版本；
/// 界面里只读展示、不可编辑或删除，名称均保留，不会被用户命令或别名占用。
/// </para>
/// </summary>
public sealed class CustomCommandService
{
    private const string EntryHeaderPrefix = "# @pam-custom";
    private const string EntryStatePrefix = "# @pam-custom-state";
    private const string EntryEndMarker = "# @pam-custom-end";

    /// <summary>PowerShell 函数名里不允许出现的字符（空白单独判断）。</summary>
    private static readonly char[] ForbiddenNameChars =
    [
        '"', '\'', '`', '$', '{', '}', '(', ')', '[', ']', ';', ',', '|', '&',
        '#', '\\', '/', ':', '*', '?', '<', '>', '=', '+', '@'
    ];

    private static readonly Regex HeaderRegex = new(
        @"^\s*#\s*@pam-custom\s+(?<name>\S+)(?:\s+(?<remark>.*))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex StateRegex = new(
        @"^\s*#\s*@pam-custom-state\s+(?<state>\w+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

    // 运行时枚举"把目标路径写在 -Path 参数里"的函数（即别名启动器写出的包装函数），
    // 按别名 / 路径 / 备注 三列输出；pls 自身与 Invoke-PamAlias 的函数体不会命中该模式。
    private const string PlsCode = """
        function pls {
            # PAM 内置命令：列出别名启动里注册的全部别名
            $rows = foreach ($cmd in (Get-Command -CommandType Function)) {
                $body = "$($cmd.ScriptBlock)"
                if (-not ($body -match 'Invoke-PamAlias\s+-Path\s+"(?<path>[^"]*)"')) { continue }
                $path = [regex]::Replace($Matches['path'], '`(.)', '$1')
                $remark = ''
                if ($body -match '-Remark\s+"(?<remark>[^"]*)"') { $remark = [regex]::Replace($Matches['remark'], '`(.)', '$1') }
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

    // ongit <提交信息>：所有参数合成一条 -m 消息，先 add 再 commit。
    // 中文消息来自命令行参数；$PROFILE 由 ProfileFileStore 统一写成 UTF-8 BOM，5.1 也能正确读取。
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

    // sudo [-w] <程序或别名> [参数...]：借 Start-Process -Verb RunAs 弹 UAC 提权。
    // 目标可为 PATH 上的 exe、完整路径、Set-Alias 别名，或别名启动器生成的同名包装函数
    // （后者从函数体里取 -Path，与 pls 的扫描方式一致）。
    // 注意：函数体里不要出现字面的 Invoke-PamAlias -Path "…"，否则会被 pls 当成别名列出。
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
                    # 模式拆开拼接：避免本函数体被 pls 的别名扫描误判。
                    $pattern = 'Invoke-PamAlias' + '\s+-Path\s+"(?<path>[^"]*)"'
                    $match = [regex]::Match("$($command.ScriptBlock)", $pattern)
                    if ($match.Success) {
                        $targetPath = [regex]::Replace($match.Groups['path'].Value, '`(.)', '$1')
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


    public CustomCommandLoadResult Load(string profilePath)
    {
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            throw new ArgumentException("配置文件路径不能为空。", nameof(profilePath));
        }

        if (!File.Exists(profilePath))
        {
            return new CustomCommandLoadResult(profilePath, false, Array.Empty<CustomCommandEntry>());
        }

        var document = ProfileDocument.Read(profilePath);
        return new CustomCommandLoadResult(profilePath, true, Parse(document.Lines));
    }

    /// <summary>
    /// 重写整个受控区块：先移除已有的 PAM-Custom 区块，再按当前列表追加新内容
    /// （内置命令不来自快照，最后固定追加定义表里的最新版本；列表为空时也会写入仅含内置命令的区块）。
    /// 区块之外的配置、编码、BOM 与换行风格保持不变。
    /// </summary>
    public CustomCommandSaveResult Save(string profilePath, IEnumerable<CustomCommandEntry> entries)
    {
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            throw new ArgumentException("配置文件路径不能为空。", nameof(profilePath));
        }

        var snapshot = entries
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.Name) && !entry.IsBuiltin)
            .ToList();

        var existed = File.Exists(profilePath);
        var document = existed ? ProfileDocument.Read(profilePath) : ProfileDocument.CreateNew();

        var lines = new List<string>(document.Lines);
        RemoveRegions(lines);

        // 区块始终写入：即使用户没有自定义命令，全部内置命令也随区块一起写入，
        // 保存过一次自定义命令（或别名）后终端里就能使用。
        if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.Add(string.Empty);
        }

        lines.Add(PamCustomRegion.Header);
        lines.Add("# 该区块由 PowerShell 管理器自动维护，请勿手动修改标记行。");

        var allEntries = snapshot
            .Concat(BuiltinCommands.Select(CreateBuiltinEntry))
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

        var output = new ProfileDocument(lines, document.Encoding, document.NewLine);
        output.WriteTo(profilePath);

        return new CustomCommandSaveResult(!existed, snapshot.Count);
    }

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

    internal static List<CustomCommandEntry> Parse(IReadOnlyList<string> lines)
    {
        var mask = PamCustomRegion.BuildMask(lines);
        var entries = new List<CustomCommandEntry>();
        var indexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var i = 0;
        while (i < lines.Count)
        {
            if (!mask[i] || !TryParseHeader(lines[i], out var name, out var remark))
            {
                i++;
                continue;
            }

            i++;

            var isEnabled = true;
            if (i < lines.Count && TryParseState(lines[i], out var state))
            {
                isEnabled = state;
                i++;
            }

            var codeLines = new List<string>();

            while (i < lines.Count &&
                   mask[i] &&
                   !IsEntryEnd(lines[i]) &&
                   !PamCustomRegion.IsFooter(lines[i]) &&
                   !HeaderRegex.IsMatch(lines[i]))
            {
                codeLines.Add(isEnabled ? lines[i] : Uncomment(lines[i]));
                i++;
            }

            if (i < lines.Count && IsEntryEnd(lines[i]))
            {
                i++;
            }

            TrimBlankEdges(codeLines);

            // 内置命令带标记进入列表（界面只读展示）；保存时总是重写为最新版本，快照里不带它们。
            var isBuiltin = IsBuiltinName(name);

            var entry = new CustomCommandEntry(
                name,
                string.Join("\n", codeLines),
                isEnabled,
                remark,
                isBuiltin);

            // 同名命令以最后一条为准，保持首次出现的位置（与别名解析策略一致）。
            if (indexByName.TryGetValue(name, out var existingIndex))
            {
                entries[existingIndex] = entry;
            }
            else
            {
                indexByName[name] = entries.Count;
                entries.Add(entry);
            }
        }

        return entries;
    }

    private static void RemoveRegions(List<string> lines)
    {
        foreach (var (start, endExclusive) in PamCustomRegion.FindRanges(lines).Reverse())
        {
            lines.RemoveRange(start, endExclusive - start);
        }
    }

    /// <summary>把定义表转换成随区块固定写入的只读条目。</summary>
    private static CustomCommandEntry CreateBuiltinEntry(BuiltinDefinition definition)
        => new(definition.Name, definition.Code, isEnabled: true, remark: definition.Remark, isBuiltin: true);

    /// <summary>
    /// 确保受控区块里已有全部内置命令：别名启动页保存后也会调用，
    /// 让用户不用专门去自定义命令页保存一次就能用上这些命令。
    /// 文件不存在时不动（由保存流程负责创建）；旧配置里只写了部分内置命令时会补齐，
    /// 已有用户命令全部保留；全部齐备时原样返回、不重写文件。
    /// </summary>
    public bool EnsureBuiltinPresent(string profilePath)
    {
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            throw new ArgumentException("配置文件路径不能为空。", nameof(profilePath));
        }

        if (!File.Exists(profilePath))
        {
            return false;
        }

        var document = ProfileDocument.Read(profilePath);

        var mask = PamCustomRegion.BuildMask(document.Lines);
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < document.Lines.Count; i++)
        {
            if (mask[i] && TryParseHeader(document.Lines[i], out var name, out _) && IsBuiltinName(name))
            {
                present.Add(name);
            }
        }

        if (BuiltinCommands.All(command => present.Contains(command.Name)))
        {
            return false;
        }

        // 缺任一内置命令：按文件现状重建（旧的用户命令原样保留），Save 会固定追加全部内置命令。
        Save(profilePath, Parse(document.Lines));
        return true;
    }

    private static IEnumerable<string> BuildEntryLines(CustomCommandEntry entry)
    {
        var name = entry.Name.Trim();
        var remark = NormalizeRemark(entry.Remark);

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

    private static string NormalizeRemark(string? remark)
        => (remark ?? string.Empty)
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

    private static bool TryParseHeader(string line, out string name, out string remark)
    {
        name = string.Empty;
        remark = string.Empty;

        var match = HeaderRegex.Match(line);
        if (!match.Success)
        {
            return false;
        }

        name = match.Groups["name"].Value.Trim();
        remark = match.Groups["remark"].Value.Trim();
        return name.Length > 0;
    }

    private static bool TryParseState(string line, out bool isEnabled)
    {
        isEnabled = true;

        var match = StateRegex.Match(line);
        if (!match.Success)
        {
            return false;
        }

        isEnabled = !string.Equals(
            match.Groups["state"].Value,
            "disabled",
            StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static bool IsEntryEnd(string line)
        => line.TrimStart().StartsWith(EntryEndMarker, StringComparison.OrdinalIgnoreCase);

    private static string Uncomment(string line)
    {
        if (line.Length == 0)
        {
            return line;
        }

        if (line == "#")
        {
            return string.Empty;
        }

        return line.StartsWith("# ", StringComparison.Ordinal) ? line[2..] : line;
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
