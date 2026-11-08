namespace PAM.Core;

public sealed partial class StarshipService
{
    // Scoop 的默认安装位置；shim 目录没进 PATH 的环境也能探测到。
    private static string ScoopShimDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "shims");

    /// <summary>探测 Starship 是否可用：先按 PATH 里的可执行文件探测，再查 Scoop shim 目录。</summary>
    public StarshipDetectResult Detect()
    {
        foreach (var exe in EnumerateStarshipExecutables())
        {
            try
            {
                var result = RunProcess(BuildVersionProbe(exe), TimeSpan.FromSeconds(20));
                if (result.ExitCode != 0)
                {
                    continue;
                }

                var version = ParseVersion(result.StandardOutput);
                if (version.Length > 0)
                {
                    return new StarshipDetectResult(true, version);
                }
            }
            catch
            {
                // 单个候选失败继续尝试下一个；全部失败视为未安装。
            }
        }

        return new StarshipDetectResult(false, string.Empty);
    }

    /// <summary>Scoop 是否可用（shim 目录或 PATH）。</summary>
    public bool IsScoopInstalled()
    {
        if (File.Exists(Path.Combine(ScoopShimDirectory, "scoop.cmd")))
        {
            return true;
        }

        try
        {
            var result = RunProcess(BuildWhereScoopProbe(), TimeSpan.FromSeconds(15));
            return result.ExitCode == 0 && result.StandardOutput.Trim().Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>列出系统里的 starship 可执行文件候选：PATH 逐目录 + Scoop shim。</summary>
    private static IEnumerable<string> EnumerateStarshipExecutables()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in GetSearchDirectories())
        {
            var candidate = Path.Combine(directory, "starship.exe");
            var key = Path.GetFullPath(candidate);
            if (File.Exists(candidate) && seen.Add(key))
            {
                yield return candidate;
            }
        }

        var shim = Path.Combine(ScoopShimDirectory, "starship.exe");
        var shimKey = Path.GetFullPath(shim);
        if (File.Exists(shim) && seen.Add(shimKey))
        {
            yield return shim;
        }
    }

    private static IEnumerable<string> GetSearchDirectories()
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVariable))
        {
            yield break;
        }

        foreach (var entry in pathVariable.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = entry.Trim().Trim('"');
            if (directory.Length > 0 && Path.IsPathRooted(directory))
            {
                yield return directory;
            }
        }
    }

    private static string ParseVersion(string output)
    {
        // 首行形如 "starship 1.23.0"。
        var firstLine = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return string.Empty;
        }

        var parts = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[1].Trim() : string.Empty;
    }
}
