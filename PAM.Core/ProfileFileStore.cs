using System.Globalization;
using System.IO;
using System.Text;

namespace PAM.Core;

/// <summary>
/// $PROFILE 的文本表示：行列表 + 原始编码 + 原始换行风格。
/// 别名与自定义命令共用同一套读写实现，保证两边写入不会破坏对方的内容。
/// </summary>
internal sealed class ProfileDocument
{
    public ProfileDocument(List<string> lines, Encoding encoding, string newLine)
    {
        Lines = lines;
        Encoding = encoding;
        NewLine = newLine;
    }

    /// <summary>已按 \n 归一化后的行（不含行尾）。</summary>
    public List<string> Lines { get; }

    /// <summary>写回时使用的编码，带 BOM 的 UTF-8 / UTF-16 会原样保留。</summary>
    public Encoding Encoding { get; }

    /// <summary>写回时使用的换行符，保持用户原有风格。</summary>
    public string NewLine { get; }

    public static ProfileDocument CreateNew()
    {
        // Windows PowerShell 5.1 只有在有 BOM 时才按 UTF-8 读取非 ASCII 内容。
        return new ProfileDocument([], new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), "\r\n");
    }

    public static ProfileDocument Read(string path) => ProfileFileStore.Read(path);

    /// <summary>按原始换行风格拼接成完整文本。</summary>
    public string ToText()
    {
        return Lines.Count == 0 ? string.Empty : string.Join(NewLine, Lines) + NewLine;
    }

    /// <summary>用临时文件原子替换写回，避免写入中断损坏配置文件。</summary>
    public void WriteTo(string path) => ProfileFileStore.Write(path, ToText(), Encoding);
}

/// <summary>
/// $PROFILE 的底层文件读写：识别编码与 BOM，写回时保持原有编码与换行风格。
/// </summary>
internal static class ProfileFileStore
{
    public static ProfileDocument Read(string path)
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

                // 无 BOM 的 UTF-8 写回时补上 BOM，否则 Windows PowerShell 5.1 会把中文读成乱码。
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

    public static void Write(string path, string text, Encoding encoding)
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
}
