namespace PAM.Core;

/// <summary>
/// 把设置里的 <c>pwsh</c> / <c>powershell</c> / <c>auto</c> 解析为实际 shell 文件名。
/// PAM.App（设置页、保存后启动窗口）与 PAM.Tray（热键唤醒终端）共用同一份判定。
/// </summary>
public static class ShellResolver
{
    public const string WindowsTerminalFileName = "wt.exe";
    public const string PwshFileName = "pwsh.exe";
    public const string PowerShellFileName = "powershell.exe";

    /// <summary>
    /// 返回实际使用的 shell。<c>usedFallback</c> 仅在"明确指定 pwsh 但系统未安装"时为 true，
    /// 用于设置页给出回退提示；<c>auto</c> 的回退是预期行为，不算回退。
    /// </summary>
    public static string Resolve(string? terminalType, out bool usedFallback)
    {
        usedFallback = false;

        if (string.Equals(terminalType, AppSettings.TerminalTypePowerShell, StringComparison.OrdinalIgnoreCase))
        {
            return PowerShellFileName;
        }

        if (IsInstalled(PwshFileName)) return PwshFileName;

        usedFallback = !string.Equals(terminalType, AppSettings.TerminalTypeAuto, StringComparison.OrdinalIgnoreCase);
        return PowerShellFileName;
    }

    /// <summary>判断 shell 是否可用：检查 PATH 与 PowerShell 7 的常见安装目录，不额外拉起进程。</summary>
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

    private static IEnumerable<string> GetSearchDirectories(string executable)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(path))
        {
            foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var directory = entry.Trim().Trim('"');
                if (directory.Length > 0) yield return directory;
            }
        }

        if (!string.Equals(executable, PwshFileName, StringComparison.OrdinalIgnoreCase)) yield break;

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (programFiles.Length > 0)
        {
            yield return Path.Combine(programFiles, "PowerShell", "7");
            yield return Path.Combine(programFiles, "PowerShell", "7-preview");
        }

        var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrEmpty(localAppData))
        {
            // Microsoft Store 版 PowerShell 在这里留下 pwsh.exe 执行别名。
            yield return Path.Combine(localAppData, "Microsoft", "WindowsApps");
        }
    }
}
