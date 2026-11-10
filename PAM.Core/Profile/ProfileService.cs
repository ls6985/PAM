using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PAM.Core;

/// <summary>打开配置文件的结果。</summary>
public sealed record ProfileOpenResult(string Path, bool FileCreated);

/// <summary>
/// 负责定位 $PROFILE、打开配置文件、检查执行策略与启动终端。
/// 托管区的读写由 <see cref="ProfileRenderer"/> 承担。
/// </summary>
public sealed class ProfileService
{
    private const string PowerShellExecutable = "powershell.exe";

    // PowerShell 5.1 在重定向输出时默认使用控制台代码页；先统一为 UTF-8 再输出路径。
    private const string Utf8Prelude = "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; ";

    static ProfileService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    // $PROFILE 路径按机器稳定：缓存解析结果，避免每次保存/启动都新开 powershell 进程去查路径。
    private static readonly Dictionary<string, string> ProfilePathCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 调用 <c>powershell -NoProfile -Command "$PROFILE"</c> 获取当前用户的配置文件路径。
    /// 保留此无参重载以兼容 Windows PowerShell 5.1 的既有调用。
    /// </summary>
    public string GetProfilePath() => GetProfilePathFromShell(ShellResolver.PowerShellFileName);

    /// <summary>
    /// 按设置里的默认终端定位 $PROFILE（PowerShell 7 与 5.1 是两个不同文件），
    /// 同时返回是否因缺少 PowerShell 7 而回退，供设置页给出提示。
    /// </summary>
    public (string Path, bool UsedFallback) ResolveProfilePath(string? terminalType)
    {
        var shell = ShellResolver.Resolve(terminalType, out var usedFallback);
        return (GetProfilePathFromShell(shell), usedFallback);
    }

    public string GetProfilePath(string? terminalType) => ResolveProfilePath(terminalType).Path;

    private string GetProfilePathFromShell(string shellFileName)
    {
        if (ProfilePathCache.TryGetValue(shellFileName, out var cachedPath))
        {
            return cachedPath;
        }

        var result = RunShell(shellFileName, Utf8Prelude + "$PROFILE", TimeSpan.FromSeconds(20));

        var path = result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .LastOrDefault();

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                $"{shellFileName} 没有返回 $PROFILE 路径。" + BuildProcessError(result));
        }

        if (!Path.IsPathRooted(path))
        {
            throw new InvalidOperationException($"{shellFileName} 返回了无效的 $PROFILE 路径：{path}");
        }

        ProfilePathCache[shellFileName] = path;
        return path;
    }

    /// <summary>
    /// 定位所选终端的 $PROFILE，文件不存在时创建（UTF-8 BOM，兼容 Windows PowerShell 5.1），
    /// 然后用系统默认编辑器打开。
    /// </summary>
    public ProfileOpenResult OpenProfileFile(string? terminalType)
    {
        var (path, _) = ResolveProfilePath(terminalType);
        var created = EnsureProfileFile(path);
        var directory = Path.GetDirectoryName(path);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                WorkingDirectory = directory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            });
        }
        catch
        {
            // 没有 .ps1 文件关联时回退到记事本，保证按钮始终能打开配置文件。
            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                ArgumentList = { path },
                UseShellExecute = true,
            });
        }

        return new ProfileOpenResult(path, created);
    }

    private static bool EnsureProfileFile(string path)
    {
        if (File.Exists(path)) return false;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        File.WriteAllText(path, "# PowerShell $PROFILE" + Environment.NewLine, new UTF8Encoding(true));
        return true;
    }

    public string GetExecutionPolicy()
    {
        var result = RunPowerShell(Utf8Prelude + "Get-ExecutionPolicy", TimeSpan.FromSeconds(15));
        return result.StandardOutput.Trim();
    }

    public static bool IsExecutionPolicyRestricted(string? policy)
    {
        if (string.IsNullOrWhiteSpace(policy))
        {
            return false;
        }

        return policy.Trim().ToLowerInvariant() is "restricted" or "allsigned" or "undefined";
    }

    public void RepairExecutionPolicy()
    {
        RunPowerShell(
            "Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned -Force",
            TimeSpan.FromSeconds(20));
    }

    /// <summary>
    /// 保存后启动一个新的终端窗口，并显式加载刚写入的 $PROFILE。
    /// 使用完整路径而不是 <c>$PROFILE</c>，保证切换默认终端后加载的仍是同一份配置。
    /// </summary>
    public void LaunchPowerShell(string profilePath, string? terminalType = null)
    {
        if (!File.Exists(profilePath))
        {
            throw new FileNotFoundException("配置文件不存在，无法加载。", profilePath);
        }

        var shell = ShellResolver.Resolve(terminalType, out _);
        var escapedPath = profilePath.Replace("'", "''", StringComparison.Ordinal);

        var startInfo = new ProcessStartInfo
        {
            FileName = shell,
            UseShellExecute = true,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        // 使用 ArgumentList 避免拼接字符串带来的参数注入问题。
        startInfo.ArgumentList.Add("-NoExit");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add($". '{escapedPath}'");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 PowerShell 进程。");
    }

    public static string NormalizePath(string? input)
    {
        var value = (input ?? string.Empty).Trim();

        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'')))
        {
            value = value[1..^1].Trim();
        }

        return value;
    }

    /// <summary>备注压成单行；供 $PROFILE 的三个写入方共用，保证空白处理一致。</summary>
    internal static string NormalizeRemark(string? remark)
    {
        var value = (remark ?? string.Empty)
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        return value;
    }

    /// <summary>
    /// 始终用双引号包裹，并转义 PowerShell 双引号字符串中的反引号、美元符号和双引号。
    /// 路径里的单引号等其他字符无需处理；供 $PROFILE 的多个写入方共用。
    /// </summary>
    internal static string QuoteForPowerShell(string path)
    {
        var escaped = path
            .Replace("`", "``", StringComparison.Ordinal)
            .Replace("$", "`$", StringComparison.Ordinal)
            .Replace("\"", "`\"", StringComparison.Ordinal);

        return $"\"{escaped}\"";
    }

    private static PowerShellProcessResult RunPowerShell(string command, TimeSpan timeout)
        => RunShell(PowerShellExecutable, command, timeout);

    private static PowerShellProcessResult RunShell(string executable, string command, TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(command);

        Process process;

        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"无法启动 {executable}。");
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"无法启动 {executable}，请确认系统中已安装对应的 PowerShell。",
                exception);
        }

        using (process)
        {
            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();

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

                throw new TimeoutException("PowerShell 命令执行超时。");
            }

            var standardOutput = standardOutputTask.GetAwaiter().GetResult();
            var standardError = standardErrorTask.GetAwaiter().GetResult();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"PowerShell 命令执行失败（退出码 {process.ExitCode}）。" +
                    BuildProcessError(new PowerShellProcessResult(standardOutput, standardError, process.ExitCode)));
            }

            return new PowerShellProcessResult(standardOutput, standardError, process.ExitCode);
        }
    }

    private static string BuildProcessError(PowerShellProcessResult result)
    {
        var error = result.StandardError.Trim();
        if (error.Length == 0)
        {
            return string.Empty;
        }

        if (error.Length > 500)
        {
            error = error[..500] + "…";
        }

        return " " + error;
    }

    private sealed record PowerShellProcessResult(
        string StandardOutput,
        string StandardError,
        int ExitCode);
}
