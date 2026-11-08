using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace PAM.Core;

public sealed partial class StarshipPromptService
{
    /// <summary>把一行 format 拆成预览片段：变量换成示例值、样式里的 fg:#RRGGBB 取出来当颜色。</summary>
    public static IReadOnlyList<PromptPreviewPart> ParsePreview(string? format)
    {
        if (string.IsNullOrEmpty(format)) return [];

        var parts = new List<PromptPreviewPart>();
        var index = 0;
        foreach (Match match in BlockRegex.Matches(format))
        {
            if (match.Index > index)
            {
                parts.Add(new PromptPreviewPart(UnescapeStarship(format[index..match.Index]), null));
            }

            var inner = UnescapeStarship(match.Groups[1].Value);
            var text = StarshipVariables.IsVariable(inner) ? StarshipVariables.SampleOf(inner) : inner;
            var hex = HexColorRegex.Match(match.Groups[2].Value).Value;
            parts.Add(new PromptPreviewPart(text, hex.Length == 0 ? null : hex.ToUpperInvariant()));
            index = match.Index + match.Length;
        }

        if (index < format.Length)
        {
            parts.Add(new PromptPreviewPart(UnescapeStarship(format[index..]), null));
        }

        return parts;
    }

    /// <summary>
    /// 把一整行 format 拆成结构化片段，供美化页「点选组合」编辑器还原成可选项：
    /// 每个 <c>[内容](样式)</c> 块拆成一段（变量或固定文字 + 颜色），块之间的零散文字也按固定文字处理。
    /// 颜色取样式里的 fg:#RRGGBB（转大写），缺失时回退色板首色。
    /// </summary>
    public static IReadOnlyList<PromptFormatSegment> ParseFormatSegments(string? format)
    {
        var segments = new List<PromptFormatSegment>();
        if (string.IsNullOrEmpty(format)) return segments;

        var index = 0;
        foreach (Match match in BlockRegex.Matches(format))
        {
            if (match.Index > index)
            {
                segments.Add(new PromptFormatSegment(PromptSegmentKind.Literal, UnescapeStarship(format[index..match.Index]), PromptPalette.Colors[0]));
            }

            var inner = UnescapeStarship(match.Groups[1].Value);
            var hexMatch = HexColorRegex.Match(match.Groups[2].Value);
            var color = hexMatch.Success ? hexMatch.Value.ToUpperInvariant() : PromptPalette.Colors[0];
            var kind = StarshipVariables.IsVariable(inner) ? PromptSegmentKind.Variable : PromptSegmentKind.Literal;
            segments.Add(new PromptFormatSegment(kind, inner, color));
            index = match.Index + match.Length;
        }

        if (index < format.Length)
        {
            segments.Add(new PromptFormatSegment(PromptSegmentKind.Literal, UnescapeStarship(format[index..]), PromptPalette.Colors[0]));
        }

        return segments;
    }

    /// <summary>Starship format 里的 \[ \] \$ \\ 反转义，只用于预览显示。</summary>
    private static string UnescapeStarship(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 >= value.Length)
            {
                builder.Append(value[i]);
                continue;
            }

            var next = value[++i];
            builder.Append(next switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => next,
            });
        }

        return builder.ToString();
    }
}
