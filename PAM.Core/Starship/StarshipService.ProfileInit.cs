namespace PAM.Core;

public sealed partial class StarshipService
{
    /// <summary>$PROFILE 里是否已存在 starship init 行（受控区块或手写的都算）。</summary>
    public bool HasInitLine(string profilePath)
    {
        if (string.IsNullOrWhiteSpace(profilePath) || !File.Exists(profilePath))
        {
            return false;
        }

        return ProfileDocument.Read(profilePath).Lines
            .Any(static line => line.Contains(InitLineKeyword, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 确保 $PROFILE 含有受控的 starship init 区块：移除旧区块与游离的 init 行
    /// （PAM-Custom 区块内的不动），再在文件末尾追加标准区块。
    /// 编码、BOM 与换行风格保持不变；文件不存在时按 UTF-8 BOM + CRLF 新建。
    /// </summary>
    public StarshipInitResult EnsureProfileInit(string profilePath)
    {
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            throw new ArgumentException("配置文件路径不能为空。", nameof(profilePath));
        }

        var existed = File.Exists(profilePath);
        var document = existed ? ProfileDocument.Read(profilePath) : ProfileDocument.CreateNew();

        var regionMask = PamCustomRegion.BuildMask(document.Lines);
        var outputLines = new List<string>(document.Lines.Count + 6);

        var insideBlock = false;
        for (var i = 0; i < document.Lines.Count; i++)
        {
            var line = document.Lines[i];

            if (insideBlock)
            {
                if (IsEndMarker(line))
                {
                    insideBlock = false;
                }

                // 区块内所有内容（含标记行）都不进入输出。
                continue;
            }

            if (IsStartMarker(line))
            {
                insideBlock = true;
                continue;
            }

            // 手写的 init 行也一并归拢进受控区块；PAM-Custom 区块内的内容不属于这里。
            if (!regionMask[i] &&
                line.Contains(InitLineKeyword, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            outputLines.Add(line);
        }

        if (outputLines.Count > 0 && !string.IsNullOrWhiteSpace(outputLines[^1]))
        {
            outputLines.Add(string.Empty);
        }

        outputLines.Add(BlockStartMarker);
        outputLines.Add(InitLine);
        outputLines.Add(BlockEndMarker);

        var output = new ProfileDocument(outputLines, document.Encoding, document.NewLine);
        output.WriteTo(profilePath);

        return new StarshipInitResult(existed, true);
    }

    private static bool IsStartMarker(string line)
        => line.TrimStart().StartsWith("# >>> pam starship", StringComparison.OrdinalIgnoreCase);

    private static bool IsEndMarker(string line)
        => line.TrimStart().StartsWith("# <<< pam starship", StringComparison.OrdinalIgnoreCase);
}
