namespace PAM.Core;

/// <summary>
/// 识别 $PROFILE 中由 PAM 独占维护的受控区块：<c>#region PAM-Custom</c> … <c>#endregion</c>。
/// 别名与自定义命令的读写都依赖它，确保两种写入互不覆盖。
/// </summary>
internal static class PamCustomRegion
{
    /// <summary>区块起始标记。</summary>
    public const string Header = "#region PAM-Custom";

    /// <summary>PowerShell 区块结束标记。</summary>
    public const string Footer = "#endregion";

    public static bool IsHeader(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("#region", StringComparison.OrdinalIgnoreCase) &&
               trimmed.Contains("PAM-Custom", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFooter(string line)
        => line.TrimStart().StartsWith("#endregion", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 标记每一行是否位于受控区块内（区块首尾标记行也算在内）。
    /// 未闭合的区块会一直延伸到文件末尾，避免残留内容被当成普通配置处理。
    /// </summary>
    public static bool[] BuildMask(IReadOnlyList<string> lines)
    {
        var mask = new bool[lines.Count];
        var inside = false;

        for (var i = 0; i < lines.Count; i++)
        {
            if (!inside && IsHeader(lines[i]))
            {
                inside = true;
                mask[i] = true;
                continue;
            }

            if (!inside)
            {
                continue;
            }

            mask[i] = true;

            if (IsFooter(lines[i]))
            {
                inside = false;
            }
        }

        return mask;
    }

    /// <summary>返回所有受控区块的行区间（EndExclusive），未闭合区块的终点为行数。</summary>
    public static IReadOnlyList<(int Start, int EndExclusive)> FindRanges(IReadOnlyList<string> lines)
    {
        var ranges = new List<(int Start, int EndExclusive)>();
        var i = 0;

        while (i < lines.Count)
        {
            if (!IsHeader(lines[i]))
            {
                i++;
                continue;
            }

            var end = lines.Count;
            for (var j = i + 1; j < lines.Count; j++)
            {
                if (IsFooter(lines[j]))
                {
                    end = j + 1;
                    break;
                }
            }

            ranges.Add((i, end));
            i = end;
        }

        return ranges;
    }
}
