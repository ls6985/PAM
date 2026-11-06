namespace PAM.Core;

/// <summary>
/// 终端（shell）选择：把设置里的 <c>pwsh</c> / <c>powershell</c> / <c>auto</c>
/// 解析为实际可执行文件名，并判断对应 shell 是否安装。PAM.App 与 PAM.Tray 共用。
/// </summary>
public static class ShellResolver
{
    public const string WindowsTerminalFileName = "wt.exe";
    public const string PwshFileName = "pwsh.exe";
    public const string PowerShellFileName = "powershell.exe";

    /// <summary>返回该终端类型实际会使用的 shell 可执行文件名。</summary>
    public static string Resolve(string? terminalType, out bool usedFallback)
    {
        usedFallback = false;

        if (string.Equals(terminalType, AppSettings.TerminalTypePowerShell, StringComparison.OrdinalIgnoreCase))
        {
            return PowerShellFileName;
        }

        if (string.Equals(terminalType, AppSettings.TerminalTypeAuto, StringComparison.OrdinalIgnoreCase))
        {
            return IsInstalled(PwshFileName) ? PwshFileName : PowerShellFileName;
        }

        // 默认（pwsh）以及未知值：优先 PowerShell 7，未安装时回退 5.1。
        if (IsInstalled(PwshFileName)) return PwshFileName;

        usedFallback = true;
        return PowerShellFileName;
    }

    /// <summary>返回用于打开 $PROFILE 的可执行文件名（不回退，便于给出准确提示）。</summary>
    public static string ResolveForProfile(string? terminalType, out bool usedFallback)
    {
        usedFallback = false;

        if (string.Equals(terminalType, AppSettings.TerminalTypePowerShell, StringComparison.OrdinalIgnoreCase))
        {
            return PowerShellFileName;
        }

        if (IsInstalled(PwshFileName)) return PwshFileName;

        if (string.Equals(terminalType, AppSettings.TerminalTypeAuto, StringComparison.OrdinalIgnoreCase))
        {
            return PowerShellFileName;
        }

        usedFallback = true;
        return PowerShellFileName;
    }

    /// <summary>
    /// 判断 shell 是否可用：先检查 PATH，再检查 PowerShell 7 的常见安装目录。
    /// 不额外拉起进程（等价于 <c>where pwsh</c>，但更快）。
    /// </summary>
    public static bool IsInstalled(string executable)
    {
        foreach (var directory in GetSearchDirectories(executable))
        {
            try
            {
                if (File.Exists(Path.Combine(directory, executable))) return true;
            }
            catch
            {
                // PATH 中可能存在无法解析的条目，跳过即可。
            }
        }

        return false;
    }

    /// <summary>返回 PowerShell 7 的完整可执行文件路径（找到时），供必要时直接启动。</summary>
    public static string? FindExecutable(string executable)
    {
        foreach (var directory in GetSearchDirectories(executable))
        {
            try
            {
                var candidate = Path.Combine(directory, executable);
                if (File.Exists(candidate)) return candidate;
            }
            catch
            {
                // 忽略无效 PATH 条目。
            }
        }

        return null;
    }

    /// <summary>终端类型对应的中文显示名（由调用方交给本地化服务翻译）。</summary>
    public static string GetDisplayName(string terminalType) => terminalType switch
    {
        AppSettings.TerminalTypePwsh => "PowerShell 7 (pwsh.exe)",
        AppSettings.TerminalTypePowerShell => "Windows PowerShell 5.1 (powershell.exe)",
        _ => "自动探测（优先 PowerShell 7）",
    };

    private static IEnumerable<string> GetSearchDirectories(string executable)
    {
        var directories = new List<string>();

        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(path))
        {
            foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var directory = entry.Trim().Trim('"');
                if (directory.Length > 0) directories.Add(directory);
            }
        }

        if (!string.Equals(executable, PwshFileName, StringComparison.OrdinalIgnoreCase))
        {
            return directories;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (programFiles.Length > 0)
        {
            directories.Add(Path.Combine(programFiles, "PowerShell", "7"));
            directories.Add(Path.Combine(programFiles, "PowerShell", "7-preview"));
        }

        var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrEmpty(localAppData))
        {
            // Microsoft Store 版 PowerShell 在这里留下 pwsh.exe 执行别名。
            directories.Add(Path.Combine(localAppData, "Microsoft", "WindowsApps"));
        }

        return directories;
    }
}
