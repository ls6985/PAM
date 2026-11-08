using System.Diagnostics;
using System.Text;

namespace PAM.Core;

public sealed partial class StarshipService
{
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
