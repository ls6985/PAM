using System.Text.RegularExpressions;

namespace PAM.Core;

/// <summary>
/// 自定义命令代码的形态转换：把「要执行的命令」包成与命令名同名的 PowerShell 函数，
/// 并兼容早期版本里以完整 <c>function</c> 定义保存的代码。
///
/// <para>包装后的函数只有在终端里输入命令名时才会执行；未包装的裸命令写在 <c>$PROFILE</c> 里
/// 会在每次启动终端时立即执行。</para>
///
/// <para>函数体保持左对齐、不额外缩进：PowerShell 5.1 要求 here-string 的结束标记
/// （<c>"@</c> / <c>'@</c>）位于行首，缩进会改变 here-string 的内容甚至使其无法解析。</para>
/// </summary>
public static class CustomCommandCode
{
    /// <summary>
    /// 去掉与命令名同名的 <c>function</c> 定义外壳，只留函数体。
    /// 不是同名函数定义、或结构不完整时原样返回，避免误伤普通脚本。
    /// </summary>
    public static string StripOuterFunction(string? name, string? code)
    {
        var body = NormalizeBlock(code);
        var commandName = (name ?? string.Empty).Trim();

        if (body.Length == 0 || commandName.Length == 0)
        {
            return body;
        }

        // function / filter + 可选的 scope 限定符 + 同名函数 + 可选空参数表 + 起始花括号。
        var header = new Regex(
            @"^\s*(?:function|filter)\s+(?:(?:global|script|local|private):)?" +
            Regex.Escape(commandName) +
            @"\s*(?:\(\s*\))?\s*\{",
            RegexOptions.IgnoreCase);

        var match = header.Match(body);
        if (!match.Success)
        {
            return body;
        }

        var inner = body[(match.Index + match.Length)..].TrimEnd();
        if (inner.Length == 0 || inner[^1] != '}')
        {
            return body;
        }

        return Dedent(NormalizeBlock(inner[..^1]).TrimEnd());
    }

    /// <summary>
    /// 生成以命令名为函数名的完整函数定义行。内置命令自带完整定义，不走本方法。
    /// </summary>
    /// <param name="allowsArguments">是否把固定参数与终端临时参数追加到命令体最后一行的末尾。</param>
    public static List<string> BuildFunctionLines(
        string name,
        string? code,
        bool allowsArguments,
        string? arguments)
    {
        var bodyLines = StripOuterFunction(name, code).Split('\n').ToList();
        TrimBlankEdges(bodyLines);

        if (allowsArguments && bodyLines.Count > 0)
        {
            AppendArguments(bodyLines, arguments);
        }

        var lines = new List<string> { $"function {name.Trim()} {{" };
        lines.AddRange(bodyLines);
        lines.Add("}");
        return lines;
    }

    /// <summary>
    /// 把固定参数与 <c>@args</c> 接到最后一个非空行末尾。函数没有 param 块时
    /// <c>$args</c> 自动收集调用时传入的参数，<c>@args</c> 负责原样展开。
    /// </summary>
    private static void AppendArguments(List<string> bodyLines, string? arguments)
    {
        var suffix = string.Join(
            " ",
            new[] { (arguments ?? string.Empty).Trim(), "@args" }
                .Where(static part => part.Length > 0));

        for (var i = bodyLines.Count - 1; i >= 0; i--)
        {
            if (string.IsNullOrWhiteSpace(bodyLines[i]))
            {
                continue;
            }

            bodyLines[i] = bodyLines[i].TrimEnd() + " " + suffix;
            return;
        }
    }

    /// <summary>
    /// 按最小公共缩进整体左移。公共缩进为 0 时不改动，
    /// 保证含 here-string 的代码（结束标记必须在行首）不被破坏。
    /// </summary>
    private static string Dedent(string code)
    {
        var lines = code.Split('\n');
        var indent = int.MaxValue;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var width = 0;
            while (width < line.Length && (line[width] == ' ' || line[width] == '\t'))
            {
                width++;
            }

            indent = Math.Min(indent, width);
        }

        if (indent is <= 0 or int.MaxValue)
        {
            return code;
        }

        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length >= indent)
            {
                lines[i] = lines[i][indent..];
            }
        }

        return string.Join("\n", lines);
    }

    private static string NormalizeBlock(string? code)
        => Models.CustomCommandEntry.NormalizeCodeBlock(code);

    private static void TrimBlankEdges(List<string> lines)
    {
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }
    }
}
