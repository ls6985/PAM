using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using PAM.Core.Models;

namespace PAM.Core;

public sealed record ProfileLoadResult(
    string ProfilePath,
    bool FileExists,
    IReadOnlyList<AliasEntry> Entries);

public sealed record ProfileSaveResult(
    bool FileCreated,
    int EntryCount);

/// <summary>
/// 负责定位、读取、解析并安全写回 PowerShell 的 $PROFILE。
/// </summary>
public sealed class ProfileService
{
    private const string PowerShellExecutable = "powershell.exe";

    // PowerShell 5.1 在重定向输出时默认使用控制台代码页；先统一为 UTF-8 再输出路径。
    private const string Utf8Prelude = "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; ";

    // 兼容：Set-Alias -Name x -Value "path"、Set-Alias x 'path'、无引号路径。
    private static readonly Regex AliasLineRegex = new(
        @"^\s*Set-Alias\s+(?:-Name\s+)?(?<name>[^\s'""]+)\s+(?:-Value\s+)?(?<value>.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MetaLineRegex = new(
        @"^\s*#\s*@alias-meta\s+(?<name>\S+)(?:\s+(?<remark>.*))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    static ProfileService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// 调用 powershell -NoProfile -Command "$PROFILE" 获取当前用户的配置文件路径。
    /// </summary>
    public string GetProfilePath()
    {
        var result = RunPowerShell(Utf8Prelude + "$PROFILE", TimeSpan.FromSeconds(20));

        var path = result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .LastOrDefault();

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                "PowerShell 没有返回 $PROFILE 路径。" + BuildProcessError(result));
        }

        if (!Path.IsPathRooted(path))
        {
            throw new InvalidOperationException($"PowerShell 返回了无效的 $PROFILE 路径：{path}");
        }

        return path;
    }

    public ProfileLoadResult Load(string profilePath)
    {
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            throw new ArgumentException("配置文件路径不能为空。", nameof(profilePath));
        }

        if (!File.Exists(profilePath))
        {
            return new ProfileLoadResult(profilePath, false, Array.Empty<AliasEntry>());
        }

        var document = ReadDocument(profilePath);
        var entries = new List<AliasEntry>();
        var indexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < document.Lines.Count; i++)
        {
            if (!TryParseAliasLine(document.Lines[i], out var name, out var value))
            {
                continue;
            }

            var remark = string.Empty;
            if (i > 0 &&
                TryParseMetaLine(document.Lines[i - 1], out var metaName, out var metaRemark) &&
                string.Equals(metaName, name, StringComparison.OrdinalIgnoreCase))
            {
                remark = metaRemark;
            }

            var entry = new AliasEntry(name, value, remark);

            // 配置里若出现重复别名，以最后一条有效记录为准，同时保持它首次出现的位置。
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

        return new ProfileLoadResult(profilePath, true, entries);
    }

    /// <summary>
    /// 删除所有可识别的 Set-Alias 行及其紧邻的 @alias-meta 注释，再追加当前列表。
    /// 其他内容、编码、BOM 和换行风格保持不变（UTF-8 无 BOM 会升级为 BOM 以兼容 Windows PowerShell 5.1）。
    /// </summary>
    public ProfileSaveResult Save(string profilePath, IEnumerable<AliasEntry> entries)
    {
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            throw new ArgumentException("配置文件路径不能为空。", nameof(profilePath));
        }

        var snapshot = entries.ToList();
        var existed = File.Exists(profilePath);
        var document = existed ? ReadDocument(profilePath) : ProfileDocument.CreateNew();

        var remove = new bool[document.Lines.Count];

        for (var i = 0; i < document.Lines.Count; i++)
        {
            if (!TryParseAliasLine(document.Lines[i], out var name, out _))
            {
                continue;
            }

            remove[i] = true;

            if (i > 0 &&
                !remove[i - 1] &&
                TryParseMetaLine(document.Lines[i - 1], out var metaName, out _) &&
                string.Equals(metaName, name, StringComparison.OrdinalIgnoreCase))
            {
                remove[i - 1] = true;
            }
        }

        var outputLines = new List<string>(document.Lines.Count + snapshot.Count * 2 + 2);

        for (var i = 0; i < document.Lines.Count; i++)
        {
            if (!remove[i])
            {
                outputLines.Add(document.Lines[i]);
            }
        }

        if (snapshot.Count > 0)
        {
            if (outputLines.Count > 0 && !string.IsNullOrWhiteSpace(outputLines[^1]))
            {
                outputLines.Add(string.Empty);
            }

            foreach (var entry in snapshot)
            {
                var name = entry.Name.Trim();
                if (name.Length == 0)
                {
                    continue;
                }

                var path = NormalizePath(entry.Path);
                var remark = NormalizeRemark(entry.Remark);

                outputLines.Add(remark.Length == 0
                    ? $"# @alias-meta {name}"
                    : $"# @alias-meta {name} {remark}");
                outputLines.Add($"Set-Alias -Name {name} -Value {QuoteForPowerShell(path)}");
            }
        }

        var text = outputLines.Count == 0
            ? string.Empty
            : string.Join(document.NewLine, outputLines) + document.NewLine;

        WriteDocument(profilePath, text, document.Encoding);

        return new ProfileSaveResult(!existed, snapshot.Count);
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
    /// 保存后启动一个新的 PowerShell 窗口，并加载 $PROFILE。
    /// </summary>
    public void LaunchPowerShell(string profilePath)
    {
        if (!File.Exists(profilePath))
        {
            throw new FileNotFoundException("配置文件不存在，无法加载。", profilePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = PowerShellExecutable,
            UseShellExecute = true,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        // 使用 ArgumentList 避免拼接字符串带来的参数注入问题。
        startInfo.ArgumentList.Add("-NoExit");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(". $PROFILE");

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

    private static string NormalizeRemark(string? remark)
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
    /// 路径里的单引号等其他字符无需处理。
    /// </summary>
    private static string QuoteForPowerShell(string path)
    {
        var escaped = path
            .Replace("`", "``", StringComparison.Ordinal)
            .Replace("$", "`$", StringComparison.Ordinal)
            .Replace("\"", "`\"", StringComparison.Ordinal);

        return $"\"{escaped}\"";
    }

    private static bool TryParseAliasLine(string line, out string name, out string value)
    {
        name = string.Empty;
        value = string.Empty;

        var match = AliasLineRegex.Match(line);
        if (!match.Success)
        {
            return false;
        }

        name = match.Groups["name"].Value.Trim();
        if (name.Length == 0)
        {
            return false;
        }

        value = ParseAliasValue(match.Groups["value"].Value);
        return value.Length > 0;
    }

    private static string ParseAliasValue(string rawValue)
    {
        var value = rawValue.Trim();

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return UnescapeDoubleQuoted(value[1..^1]);
        }

        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        return value;
    }

    private static string UnescapeDoubleQuoted(string value)
    {
        var builder = new StringBuilder(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '`' || i == value.Length - 1)
            {
                builder.Append(value[i]);
                continue;
            }

            var next = value[++i];
            builder.Append(next switch
            {
                '0' => '\0',
                'a' => '\a',
                'b' => '\b',
                'f' => '\f',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                'v' => '\v',
                _ => next
            });
        }

        return builder.ToString();
    }

    private static bool TryParseMetaLine(string line, out string name, out string remark)
    {
        name = string.Empty;
        remark = string.Empty;

        var match = MetaLineRegex.Match(line);
        if (!match.Success)
        {
            return false;
        }

        name = match.Groups["name"].Value.Trim();
        remark = match.Groups["remark"].Value.Trim();
        return name.Length > 0;
    }

    private static ProfileDocument ReadDocument(string path)
    {
        var bytes = File.ReadAllBytes(path);
        Encoding encoding;
        string text;

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
            text = encoding.GetString(bytes, 2, bytes.Length - 2);
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            encoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: true);
            text = encoding.GetString(bytes, 2, bytes.Length - 2);
        }
        else
        {
            try
            {
                text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                    .GetString(bytes);

                // Windows PowerShell 5.1 只有在有 BOM 时才按 UTF-8 读取无 ASCII 内容。
                encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            }
            catch (DecoderFallbackException)
            {
                try
                {
                    encoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
                    text = encoding.GetString(bytes);
                }
                catch (ArgumentException)
                {
                    encoding = Encoding.Latin1;
                    text = encoding.GetString(bytes);
                }
            }
        }

        var newLine = text.Contains("\r\n", StringComparison.Ordinal)
            ? "\r\n"
            : text.Contains('\n') ? "\n" : "\r\n";

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n').ToList();

        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count == 1 && lines[0].Length == 0)
        {
            lines.Clear();
        }

        return new ProfileDocument(lines, encoding, newLine);
    }

    private static void WriteDocument(string path, string text, Encoding encoding)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = Path.Combine(
            directory ?? Path.GetTempPath(),
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(temporaryPath, text, encoding);

            if (File.Exists(fullPath))
            {
                try
                {
                    File.Replace(temporaryPath, fullPath, destinationBackupFileName: null);
                }
                catch (IOException)
                {
                    // 某些文件系统不支持 File.Replace，退回覆盖写入但仍使用临时文件。
                    File.Copy(temporaryPath, fullPath, overwrite: true);
                    File.Delete(temporaryPath);
                }
                catch (UnauthorizedAccessException)
                {
                    File.Copy(temporaryPath, fullPath, overwrite: true);
                    File.Delete(temporaryPath);
                }
            }
            else
            {
                File.Move(temporaryPath, fullPath);
            }
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch
            {
                // 临时文件清理失败不影响已经写入的配置文件。
            }
        }
    }

    private static PowerShellProcessResult RunPowerShell(string command, TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = PowerShellExecutable,
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
                ?? throw new InvalidOperationException("无法启动 powershell.exe。");
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "无法启动 Windows PowerShell，请确认系统中存在 powershell.exe。",
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

    private sealed record ProfileDocument(
        List<string> Lines,
        Encoding Encoding,
        string NewLine)
    {
        public static ProfileDocument CreateNew()
        {
            return new ProfileDocument([], new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), "\r\n");
        }
    }
}
