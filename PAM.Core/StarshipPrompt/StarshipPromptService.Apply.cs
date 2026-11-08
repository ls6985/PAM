using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PAM.Core;

public sealed partial class StarshipPromptService
{
    /// <summary>
    /// 把一行 format 写入 starship.toml：托管块内写顶层 format，并按段颜色接管 format 里
    /// 引用到的模块段落；未被引用的段落、注释、编码、换行原样保留。
    /// </summary>
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
        var modules = ExtractManagedModules(format);
        var (clean, insertIndex) = StripManagedParts(lines, modules.Select(static m => m.Module).ToList());
        clean.InsertRange(insertIndex, BuildManagedBlock(format, modules));

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

    /// <summary>
    /// 恢复首次应用前的原始 starship.toml；没有备份时返回 false。
    /// 恢复即消费备份（删除文件）：按钮的可用性就是"有未撤销的应用前备份"，
    /// 恢复后回到禁用，再次应用会重新备份。
    /// </summary>
    public bool Restore()
    {
        var backupPath = GetBackupPath();
        if (!File.Exists(backupPath)) return false;

        var path = GetConfigPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Copy(backupPath, path, overwrite: true);
        File.Delete(backupPath);
        return true;
    }

    /// <summary>
    /// 移除旧 PAM 块、旧顶层 format（含 """\ 多行字符串）以及被引用模块的既有段落
    /// （[module] 与 [module.x] 子表：段落里的 format / 样式会盖过顶层 format 的外层颜色，
    /// 且 TOML 不允许重复定义表，必须整段移走后由托管块重写）。
    /// 返回清理后的行与块插入位置：第一个段落表头之前（无段落时为行尾），
    /// 这样块内的 [module] 表头不会把后面的根级键捕获进自己的表。
    /// </summary>
    private static (List<string> Lines, int InsertIndex) StripManagedParts(
        List<string> lines, IReadOnlyCollection<string> managedModules)
    {
        var result = new List<string>(lines.Count);
        var inBlock = false;
        var topLevel = true;
        string? skippingModule = null;

        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].TrimStart();

            if (trimmed.Equals(StartMarker, StringComparison.OrdinalIgnoreCase))
            {
                inBlock = true;
                continue;
            }

            if (inBlock)
            {
                if (trimmed.Equals(EndMarker, StringComparison.OrdinalIgnoreCase)) inBlock = false;
                continue;
            }

            if (skippingModule is not null)
            {
                if (trimmed.Length == 0) continue;
                if (TryGetHeaderName(trimmed, out var pending))
                {
                    if (pending == skippingModule
                        || pending.StartsWith(skippingModule + ".", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    skippingModule = null; // 到了别的段落：落回正常流程处理这一行
                }
                else
                {
                    continue;
                }
            }

            // 只认任何 [section] 之前（= 顶层）的 format，段落内的同名 format 不动。
            if (topLevel && IsFormatAssignment(trimmed))
            {
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

            if (TryGetHeaderName(trimmed, out var header))
            {
                topLevel = false;
                if (managedModules.Contains(header))
                {
                    skippingModule = header;
                    continue;
                }
            }

            result.Add(lines[i]);
        }

        var insertIndex = result.FindIndex(static line => TryGetHeaderName(line.TrimStart(), out _));
        if (insertIndex < 0) insertIndex = result.Count;
        return (result, insertIndex);
    }

    /// <summary>识别段落表头行（[module] / [module.sub]）并取出表名；其余行返回 false。</summary>
    private static bool TryGetHeaderName(string trimmed, out string name)
    {
        name = string.Empty;
        if (!trimmed.StartsWith('[')) return false;
        var end = trimmed.IndexOf(']');
        if (end <= 1) return false;

        name = trimmed[1..end].Trim();
        return name.Length > 0;
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
