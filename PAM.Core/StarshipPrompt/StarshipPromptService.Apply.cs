using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PAM.Core;

public sealed partial class StarshipPromptService
{
    /// <summary>把一行 format 写入 starship.toml（只替换顶层 format，其他内容原样保留）。</summary>
    public StarshipPromptApplyResult Apply(string format)
    {
        Validate(format);

        var path = GetConfigPath();
        var exists = File.Exists(path);
        var content = exists ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;

        // 只在首次写入时备份：连续应用不会把「用户原始配置」覆盖成上一次结果。
        var backupCreated = false;
        if (exists && !File.Exists(GetBackupPath()))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(path, GetBackupPath(), overwrite: false);
            backupCreated = true;
        }

        var newLine = content.Contains("\r\n", StringComparison.Ordinal)
            ? "\r\n"
            : content.Contains('\n') ? "\n" : Environment.NewLine;

        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        var (clean, insertIndex) = StripManagedParts(lines);

        clean.Insert(insertIndex, StartMarker);
        clean.Insert(insertIndex + 1, NoticeLine);
        clean.Insert(insertIndex + 2, BuildFormatLine(format));
        clean.Insert(insertIndex + 3, EndMarker);

        var output = string.Join(newLine, clean);
        if (!output.EndsWith(newLine, StringComparison.Ordinal)) output += newLine;

        // TOML 规范不建议带 BOM；用临时文件 + Move 原子替换，避免写一半被 Starship 读到。
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tempPath, output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch (IOException) { }
            }
        }

        return new StarshipPromptApplyResult(true, backupCreated, path);
    }

    /// <summary>恢复首次应用前的原始 starship.toml；没有备份时返回 false。</summary>
    public bool Restore()
    {
        var backupPath = GetBackupPath();
        if (!File.Exists(backupPath)) return false;

        var path = GetConfigPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Copy(backupPath, path, overwrite: true);
        return true;
    }

    /// <summary>移除旧的 PAM 块和旧的顶层 format（含 """\ 多行字符串），返回插入位置。</summary>
    private static (List<string> Lines, int InsertIndex) StripManagedParts(List<string> lines)
    {
        var result = new List<string>(lines.Count);
        var inBlock = false;
        var topLevel = true;
        var blockIndex = -1;
        var formatIndex = -1;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            if (trimmed.Equals(StartMarker, StringComparison.OrdinalIgnoreCase))
            {
                inBlock = true;
                if (blockIndex < 0) blockIndex = result.Count;
                continue;
            }

            if (inBlock)
            {
                if (trimmed.Equals(EndMarker, StringComparison.OrdinalIgnoreCase)) inBlock = false;
                continue;
            }

            // 只认任何 [section] 之前（= 顶层）的 format，段落内的同名 format 不动。
            if (formatIndex < 0 && topLevel && IsFormatAssignment(trimmed))
            {
                formatIndex = result.Count;
                var equals = trimmed.IndexOf('=');
                var value = trimmed[(equals + 1)..].TrimStart();
                if (value.StartsWith("\"\"\"", StringComparison.Ordinal)
                    && !value[3..].Contains("\"\"\"", StringComparison.Ordinal))
                {
                    // 多行基本字符串：一直吃到含结束 """ 的那一行。
                    while (i + 1 < lines.Count && !lines[i + 1].Contains("\"\"\"", StringComparison.Ordinal)) i++;
                    if (i + 1 < lines.Count) i++;
                }

                continue;
            }

            if (trimmed.StartsWith('[') && trimmed.Contains(']')) topLevel = false;
            result.Add(line);
        }

        var insertIndex = blockIndex >= 0 ? Math.Min(blockIndex, result.Count)
            : formatIndex >= 0 ? formatIndex
            : 0;
        return (result, insertIndex);
    }

    private static bool IsFormatAssignment(string trimmed)
    {
        var equals = trimmed.IndexOf('=');
        if (equals <= 0) return false;
        return trimmed[..equals].Trim().Equals("format", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>只做 TOML 双引号字符串层转义；format 里的 Starship 语法（\[ \] \$ \\）原样保留。</summary>
    private static string BuildFormatLine(string format) => "format = \"" + EncodeToml(format) + "\"";

    private static void Validate(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            throw new ArgumentException("提示符 format 不能为空。", nameof(format));
        }

        if (format.Contains('\r') || format.Contains('\n'))
        {
            throw new ArgumentException("提示符 format 必须是一行。", nameof(format));
        }

        if (format.Length > MaxFormatLength)
        {
            throw new ArgumentException(
                string.Format(CultureInfo.InvariantCulture, "提示符 format 不能超过 {0} 个字符。", MaxFormatLength),
                nameof(format));
        }

        foreach (Match match in FgColorRegex.Matches(format))
        {
            if (!HexValidation.IsMatch(match.Groups[1].Value))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, "颜色 {0} 不是 #RRGGBB 格式。", match.Groups[1].Value),
                    nameof(format));
            }
        }
    }
}
