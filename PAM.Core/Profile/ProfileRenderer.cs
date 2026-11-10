using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PAM.Core;

/// <summary>托管区一次渲染的结果。</summary>
public sealed record ProfileRenderResult(
    string ProfilePath,
    bool FileCreated,
    string ManagedHash,
    IReadOnlyList<string> ConflictingNames);

/// <summary>$PROFILE 中现有托管区的状态，用于识别外部修改。</summary>
public sealed record ManagedSectionState(bool HasMarker, string Hash);

/// <summary>
/// $PROFILE 的唯一写入方：边界标记之前为用户区，原样保留；标记之后为托管区，按
/// <see cref="PamData"/> 全量重写。编码、BOM 与换行风格由 ProfileDocument 保持。
/// </summary>
public static class ProfileRenderer
{
    public const string StartMarker = "# >>> PAM Managed >>>";

    private const string WarningLine =
        "# 以下由 PowerShell 管理器自动生成；修改会在下次保存时被覆盖。";

    private static readonly Regex UserAliasRegex = new(
        @"^\s*Set-Alias\s+(?:-Name\s+)?(?<name>[^\s'""]+)\s+(?:-Value\s+)?(?<value>.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex UserFunctionRegex = new(
        @"^\s*function\s+(?<name>[^\s{]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>按数据快照重建托管区，返回本次生成的哈希与同名冲突列表。</summary>
    public static ProfileRenderResult Render(string profilePath, PamData data)
    {
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            throw new ArgumentException("配置文件路径不能为空。", nameof(profilePath));
        }

        ArgumentNullException.ThrowIfNull(data);

        var fileCreated = !File.Exists(profilePath);
        var directory = Path.GetDirectoryName(profilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = fileCreated ? ProfileDocument.CreateNew() : ProfileDocument.Read(profilePath);
        var allLines = document.Lines;

        var markerIndex = allLines.FindIndex(IsStartMarker);
        var userCandidate = markerIndex >= 0 ? allLines.Take(markerIndex).ToList() : [.. allLines];
        var oldManaged = markerIndex >= 0 ? allLines.Skip(markerIndex + 1).ToList() : [];

        var starshipBlock = ExtractStarshipBlock(userCandidate.Concat(oldManaged));
        var userLines = StripGeneratedBlocks(userCandidate);
        TrimTrailingBlankLines(userLines);

        var managedLines = new List<string>
        {
            StartMarker,
            WarningLine,
            string.Empty,
        };

        if (starshipBlock.Count > 0)
        {
            managedLines.AddRange(starshipBlock);
            managedLines.Add(string.Empty);
        }

        var launcherLines = AliasLaunchService.BuildLines(data.Aliases);
        if (launcherLines.Count > 0)
        {
            managedLines.AddRange(launcherLines);
            managedLines.Add(string.Empty);
        }

        managedLines.AddRange(CommandHintService.BuildLines());
        managedLines.Add(string.Empty);

        managedLines.AddRange(CustomCommandService.BuildLines(data.CustomCommands));

        var outputLines = new List<string>(userLines);
        if (outputLines.Count > 0)
        {
            outputLines.Add(string.Empty);
        }

        outputLines.AddRange(managedLines);

        new ProfileDocument(outputLines, document.Encoding, document.NewLine).WriteTo(profilePath);

        return new ProfileRenderResult(
            profilePath,
            fileCreated,
            ComputeHash(managedLines),
            FindConflictingNames(userLines, data.Aliases));
    }

    /// <summary>读取当前托管区的哈希；文件不存在或没有边界标记时返回空哈希。</summary>
    public static ManagedSectionState Inspect(string profilePath)
    {
        if (string.IsNullOrWhiteSpace(profilePath) || !File.Exists(profilePath))
        {
            return new ManagedSectionState(false, string.Empty);
        }

        var lines = ProfileDocument.Read(profilePath).Lines;
        var markerIndex = lines.FindIndex(IsStartMarker);
        if (markerIndex < 0)
        {
            return new ManagedSectionState(false, string.Empty);
        }

        var managedLines = lines.Skip(markerIndex).ToList();
        return new ManagedSectionState(true, ComputeHash(managedLines));
    }

    /// <summary>
    /// 托管区是否含有当前版本代码固定写入的全部标记块。缺任一块（例如旧版代码写入、
    /// 或外部工具改动过托管区）即视为需要重建，避免“数据哈希没变、但代码新增了托管块”
    /// 导致的新块永远写不进去。
    /// </summary>
    public static bool ManagedSectionHasCurrentMarkers(string profilePath)
    {
        if (string.IsNullOrWhiteSpace(profilePath) || !File.Exists(profilePath))
        {
            return false;
        }

        var lines = ProfileDocument.Read(profilePath).Lines;
        var markerIndex = lines.FindIndex(IsStartMarker);
        if (markerIndex < 0)
        {
            return false;
        }

        var managed = lines.Skip(markerIndex);
        return managed.Any(IsCommandHintStart) && managed.Any(PamCustomRegion.IsHeader);
    }

    /// <summary>
    /// 单个托管配置文件是否需要重建：文件缺失、边界标记缺失、缺少当前版本的固定标记块，
    /// 或托管区哈希与预期值不符（外部改动、旧版代码写入、切换到另一终端的 $PROFILE）。
    /// </summary>
    public static bool NeedsRebuild(string profilePath, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(profilePath) || !File.Exists(profilePath))
        {
            return true;
        }

        var state = Inspect(profilePath);
        if (!state.HasMarker)
        {
            return true;
        }

        if (!ManagedSectionHasCurrentMarkers(profilePath))
        {
            return true;
        }

        return !string.Equals(state.Hash, expectedHash, StringComparison.Ordinal);
    }

    /// <summary>按同一份数据快照依次重建多个 $PROFILE 的托管区，返回最后一次渲染的结果。</summary>
    public static ProfileRenderResult RenderToProfiles(IEnumerable<string> profilePaths, PamData data)
    {
        ProfileRenderResult? last = null;
        foreach (var path in profilePaths)
        {
            last = Render(path, data);
        }

        return last ?? throw new ArgumentException("至少需要一个配置文件路径。", nameof(profilePaths));
    }

    /// <summary>托管区文本的稳定哈希，与编码和换行风格无关。</summary>
    public static string ComputeHash(IEnumerable<string> lines)
    {
        var bytes = Encoding.UTF8.GetBytes(string.Join("\n", lines));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    /// <summary>用户区里已声明、会遮蔽 PAM 同名包装函数的命令名。</summary>
    public static IReadOnlyList<string> FindConflictingNames(
        IReadOnlyList<string> userLines,
        IEnumerable<AliasData> aliases)
    {
        var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in userLines)
        {
            var aliasMatch = UserAliasRegex.Match(line);
            if (aliasMatch.Success)
            {
                declared.Add(aliasMatch.Groups["name"].Value);
            }

            var functionMatch = UserFunctionRegex.Match(line);
            if (functionMatch.Success)
            {
                declared.Add(functionMatch.Groups["name"].Value);
            }
        }

        return aliases
            .Where(static alias => !string.IsNullOrWhiteSpace(alias.Name))
            .Select(static alias => alias.Name.Trim())
            .Where(name => declared.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsStartMarker(string line)
        => line.TrimStart().Equals(StartMarker, StringComparison.OrdinalIgnoreCase);

    private static bool IsStarshipStart(string line)
        => line.TrimStart().StartsWith("# >>> PAM Starship", StringComparison.OrdinalIgnoreCase);

    private static bool IsStarshipEnd(string line)
        => line.TrimStart().StartsWith("# <<< PAM Starship", StringComparison.OrdinalIgnoreCase);

    private static bool IsLauncherStart(string line)
        => line.TrimStart().Equals(AliasLaunchService.StartMarker, StringComparison.OrdinalIgnoreCase);

    private static bool IsLauncherEnd(string line)
        => line.TrimStart().Equals(AliasLaunchService.EndMarker, StringComparison.OrdinalIgnoreCase);

    private static bool IsCommandHintStart(string line)
        => line.TrimStart().Equals(CommandHintService.StartMarker, StringComparison.OrdinalIgnoreCase);

    private static bool IsCommandHintEnd(string line)
        => line.TrimStart().Equals(CommandHintService.EndMarker, StringComparison.OrdinalIgnoreCase);

    private static List<string> ExtractStarshipBlock(IEnumerable<string> source)
    {
        var lines = source.ToList();

        for (var end = lines.Count - 1; end >= 0; end--)
        {
            if (!IsStarshipEnd(lines[end]))
            {
                continue;
            }

            for (var start = end - 1; start >= 0; start--)
            {
                if (IsStarshipStart(lines[start]))
                {
                    return lines.GetRange(start, end - start + 1);
                }

                if (IsStarshipEnd(lines[start]))
                {
                    break;
                }
            }
        }

        return [];
    }

    private static List<string> StripGeneratedBlocks(List<string> lines)
    {
        var result = new List<string>(lines);

        foreach (var (start, endExclusive) in PamCustomRegion.FindRanges(result).Reverse())
        {
            result.RemoveRange(start, endExclusive - start);
        }

        var output = new List<string>(result.Count);
        var i = 0;
        while (i < result.Count)
        {
            if (IsStarshipStart(result[i]))
            {
                i = SkipToEnd(result, i, IsStarshipEnd);
                continue;
            }

            if (IsLauncherStart(result[i]))
            {
                i = SkipToEnd(result, i, IsLauncherEnd);
                continue;
            }

            if (IsCommandHintStart(result[i]))
            {
                i = SkipToEnd(result, i, IsCommandHintEnd);
                continue;
            }

            output.Add(result[i]);
            i++;
        }

        return output;
    }

    private static int SkipToEnd(List<string> lines, int start, Func<string, bool> isEnd)
    {
        var i = start + 1;
        while (i < lines.Count)
        {
            if (isEnd(lines[i]))
            {
                return i + 1;
            }

            i++;
        }

        return lines.Count;
    }

    private static void TrimTrailingBlankLines(List<string> lines)
    {
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }
    }
}
