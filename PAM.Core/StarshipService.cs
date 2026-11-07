using System.Diagnostics;
using System.IO;
using System.Text;

namespace PAM.Core;

/// <summary>内置主题预设：Id 对应 EmbeddedResource，DisplayNameSource 为本地化词表键。</summary>
public sealed record StarshipPreset(string Id, string DisplayNameSource, bool RequiresNerdFont);

/// <summary>Starship 探测结果：Installed 为 false 时 Version 为空。</summary>
public sealed record StarshipDetectResult(bool Installed, string Version);

/// <summary>安装类操作的结果：Success 为 false 时 Message 携带可供界面展示的原因。</summary>
public sealed record StarshipInstallResult(bool Success, string Message);

/// <summary>starship.toml 与 $PROFILE init 行的当前状态，供设置页展示。</summary>
public sealed record StarshipConfigState(
    string ConfigPath,
    bool ConfigExists,
    bool BackupExists,
    string ProfilePath,
    bool ProfileHasInitLine);

/// <summary>打开 starship.toml 的结果。</summary>
public sealed record StarshipOpenResult(string Path, bool FileCreated);

/// <summary>确保 $PROFILE init 行的结果：FileExisted 表示写入前文件是否已存在。</summary>
public sealed record StarshipInitResult(bool FileExisted, bool Updated);

/// <summary>应用主题预设的结果。</summary>
public sealed record StarshipApplyResult(bool ConfigWritten, bool ProfileUpdated);

/// <summary>
/// 美化功能的 Starship 侧服务：探测/安装 Starship（优先 Scoop）、把内置主题预设写入
/// <c>starship.toml</c>（写前自动备份）、并保证 $PROFILE 里有 starship init 行。
/// <para>
/// $PROFILE 中的受控区块使用自有标记对，<b>不要</b>放进 PAM-Custom 区块——那里会被
/// CustomCommandService 整块重写：
/// </para>
/// <code>
/// # &gt;&gt;&gt; PAM Starship &gt;&gt;&gt;
/// Invoke-Expression (&amp;starship init powershell)
/// # &lt;&lt;&lt; PAM Starship &lt;&lt;&lt;
/// </code>
/// </summary>
public sealed class StarshipService
{
    public const string BlockStartMarker = "# >>> PAM Starship >>>";
    public const string BlockEndMarker = "# <<< PAM Starship <<<";
    public const string InitLine = "Invoke-Expression (&starship init powershell)";

    private const string InitLineKeyword = "starship init";
    private const string ConfigFileName = "starship.toml";
    private const string BackupSuffix = ".bak";

    /// <summary>测试与特殊部署用的配置路径覆盖；为 null 时走 STARSHIP_CONFIG / 默认路径。</summary>
    private readonly string? _configPathOverride;

    /// <summary>测试注入专用：让 starship.toml 的读写落在指定目录，不碰真实用户配置。</summary>
    public StarshipService(string configPathOverride) => _configPathOverride = configPathOverride;

    public StarshipService()
    {
    }

    /// <summary>内置预设清单。RequiresNerdFont 的预设需要先安装 Nerd Font，界面会提示。</summary>
    private static readonly StarshipPreset[] KnownPresets =
    [
        new("nerd-font-symbols", "Nerd Font 符号（需 Nerd Font）", true),
        new("pastel-powerline", "粉彩电力线（需 Nerd Font）", true),
        new("gruvbox-rainbow", "Gruvbox 彩虹（需 Nerd Font）", true),
        new("tokyo-night", "Tokyo Night（需 Nerd Font）", true),
        new("plain-text-symbols", "纯文本符号（无需特殊字体）", false),
        new("no-nerd-font", "无 Nerd Font 模式（无需特殊字体）", false),
    ];

    private static readonly string ResourcePrefix = typeof(StarshipService).Namespace + ".Presets.";

    // Scoop 的默认安装位置；shim 目录没进 PATH 的环境也能探测到。
    private static string ScoopShimDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "shims");

    // --- 探测 ---

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

    // --- 安装 ---

    /// <summary>
    /// 用 Scoop 安装 Starship。要求 Scoop 已安装；被一键安装流程在最末尾调用。
    /// </summary>
    public StarshipInstallResult InstallStarship(Action<string>? progress = null)
    {
        if (!IsScoopInstalled())
        {
            return new StarshipInstallResult(false, "未检测到 Scoop，请先安装 Scoop。");
        }

        var scoop = Path.Combine(ScoopShimDirectory, "scoop.cmd");
        if (!File.Exists(scoop))
        {
            scoop = "scoop.cmd";
        }

        var result = RunProcess(BuildScoopInstall(scoop), TimeSpan.FromMinutes(15), progress);
        if (result.ExitCode == 0)
        {
            return new StarshipInstallResult(true, "Starship 安装完成。");
        }

        return new StarshipInstallResult(false, $"scoop install starship 失败（退出码 {result.ExitCode}）。{BuildErrorTail(result.StandardError)}");
    }

    /// <summary>
    /// 一键安装：Scoop 不存在时先执行官方引导脚本（需要联网），再安装 Starship。
    /// </summary>
    public StarshipInstallResult InstallScoopAndStarship(Action<string>? progress = null)
    {
        if (!IsScoopInstalled())
        {
            progress?.Invoke("正在安装 Scoop…");
            var bootstrap = RunProcess(
                BuildScoopBootstrap(),
                TimeSpan.FromMinutes(15),
                progress);

            if (bootstrap.ExitCode != 0 || !IsScoopInstalled())
            {
                return new StarshipInstallResult(
                    false,
                    $"Scoop 安装失败（退出码 {bootstrap.ExitCode}）。{BuildErrorTail(bootstrap.StandardError)}");
            }

            progress?.Invoke("Scoop 安装完成。");
        }

        return InstallStarship(progress);
    }

    // --- 配置文件 ---

    /// <summary>starship.toml 路径：注入路径 → STARSHIP_CONFIG → ~/.config/starship.toml。</summary>
    public string GetConfigPath()
    {
        if (_configPathOverride is not null)
        {
            return _configPathOverride;
        }

        var overridePath = Environment.GetEnvironmentVariable("STARSHIP_CONFIG");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            ConfigFileName);
    }

    public string GetBackupPath() => GetConfigPath() + BackupSuffix;

    /// <summary>当前配置状态：toml 是否存在、有无备份、$PROFILE 里有没有 init 行。</summary>
    public StarshipConfigState GetState(string profilePath)
    {
        var configPath = GetConfigPath();
        return new StarshipConfigState(
            configPath,
            File.Exists(configPath),
            File.Exists(GetBackupPath()),
            profilePath,
            HasInitLine(profilePath));
    }

    /// <summary>写配置前备份现有 starship.toml（单槽覆盖）。文件不存在时不做任何事。</summary>
    public bool BackupConfig()
    {
        var configPath = GetConfigPath();
        if (!File.Exists(configPath))
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.Copy(configPath, GetBackupPath(), overwrite: true);
        return true;
    }

    /// <summary>用备份恢复 starship.toml。没有备份时返回 false。</summary>
    public bool RestoreConfig()
    {
        var backupPath = GetBackupPath();
        if (!File.Exists(backupPath))
        {
            return false;
        }

        var configPath = GetConfigPath();
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.Copy(backupPath, configPath, overwrite: true);
        return true;
    }

    /// <summary>应用内置主题预设：备份 → 写 starship.toml → 确保 $PROFILE 有 init 行。</summary>
    public StarshipApplyResult ApplyPreset(string presetId, string profilePath)
    {
        var configPath = GetConfigPath();
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

        BackupConfig();
        var content = ExtractPreset(presetId)
            ?? throw new InvalidOperationException($"未找到内置主题预设：{presetId}");

        // TOML 规范不建议带 BOM，Starship 直接按 UTF-8 读取。
        ProfileFileStore.Write(configPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var profileUpdated = EnsureProfileInit(profilePath);
        return new StarshipApplyResult(true, profileUpdated.Updated);
    }

    /// <summary>打开 starship.toml（不存在则先创建空文件），交给系统默认编辑器。</summary>
    public StarshipOpenResult OpenConfig()
    {
        var configPath = GetConfigPath();
        var created = false;

        if (!File.Exists(configPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            File.WriteAllText(configPath, string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            created = true;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = configPath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(configPath),
            });
        }
        catch
        {
            // 没有 .toml 文件关联时回退到记事本，保证按钮始终能打开配置文件。
            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                ArgumentList = { configPath },
                UseShellExecute = true,
            });
        }

        return new StarshipOpenResult(configPath, created);
    }

    // --- $PROFILE init 行 ---

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

    // --- 预设资源 ---

    /// <summary>内置预设列表（资源确实存在的才返回）。</summary>
    public IReadOnlyList<StarshipPreset> GetPresets()
    {
        var assembly = typeof(StarshipService).Assembly;
        var names = assembly.GetManifestResourceNames();

        return KnownPresets
            .Where(preset => names.Contains(ResourcePrefix + preset.Id + ".toml", StringComparer.Ordinal))
            .ToList();
    }

    /// <summary>解出内置预设 toml 内容。</summary>
    public string? ExtractPreset(string presetId)
    {
        var assembly = typeof(StarshipService).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + presetId + ".toml");
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    // --- 进程执行 ---

    private static ProcessStartInfo BuildVersionProbe(string executable)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("--version");
        return startInfo;
    }

    private static ProcessStartInfo BuildWhereScoopProbe()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "where.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("scoop");
        return startInfo;
    }

    private static ProcessStartInfo BuildScoopInstall(string scoopCommand)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = scoopCommand,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("install");
        startInfo.ArgumentList.Add("starship");
        return startInfo;
    }

    private static ProcessStartInfo BuildScoopBootstrap()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add("irm get.scoop.sh | iex");
        return startInfo;
    }

    /// <summary>
    /// 与 ProfileService 的 RunShell 同一套纪律：重定向输出、超时杀进程树、
    /// 退出码非 0 交由调用方决定（安装类命令需要拿到输出而不是直接抛异常）。
    /// </summary>
    private static (string StandardOutput, string StandardError, int ExitCode) RunProcess(
        ProcessStartInfo startInfo,
        TimeSpan timeout,
        Action<string>? progress = null)
    {
        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"无法启动 {startInfo.FileName}。");

        using (process)
        {
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    return;
                }

                lock (outputBuilder)
                {
                    outputBuilder.AppendLine(e.Data);
                }

                progress?.Invoke(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    return;
                }

                lock (errorBuilder)
                {
                    errorBuilder.AppendLine(e.Data);
                }

                progress?.Invoke(e.Data);
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // 进程可能已经退出。
                }

                throw new TimeoutException($"命令执行超时：{startInfo.FileName}");
            }

            return (outputBuilder.ToString(), errorBuilder.ToString(), process.ExitCode);
        }
    }

    private static string BuildErrorTail(string standardError)
    {
        var error = standardError.Trim();
        if (error.Length == 0)
        {
            return string.Empty;
        }

        if (error.Length > 300)
        {
            error = error[..300] + "…";
        }

        return " " + error;
    }
}
