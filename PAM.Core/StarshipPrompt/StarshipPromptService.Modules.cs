using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PAM.Core;

public sealed partial class StarshipPromptService
{
    /// <summary>
    /// 精选模块的托管段落模板：format 里引用了某个模块变量时，块内重写该模块的段落，
    /// 把选定颜色直接内嵌进模块自身 format——顶层 format 的外层 (fg:…) 只能染色裸文字，
    /// 模块输出自带样式；而段落的 style 键又只在段落 format 引用 ($style) 时才生效，
    /// 所以内嵌是唯一可靠的做法。show_always / ssh_only / disabled 让模块在任何环境都按预览显示。
    /// success_symbol 里的 \$ 是 Starship 层转义（写盘前还会再做一层 TOML 转义成 \\$）。
    /// </summary>
    private static readonly Dictionary<string, string[]> ManagedModuleTemplates = new()
    {
        ["username"] = ["show_always = true", "format = \"[$user](fg:{0})\""],
        ["hostname"] = ["ssh_only = false", "format = \"[@$hostname](fg:{0})\""],
        ["directory"] = ["format = \"[$path](fg:{0})\""],
        ["character"] =
        [
            "format = \"$symbol\"",
            "success_symbol = \"[\\\\$](fg:{0})\"",
            "error_symbol = \"[✗](fg:{0})\"",
        ],
        ["shell"] = ["disabled = false", "format = \"[$indicator](fg:{0})\""],
        ["git_branch"] = ["format = \"[$branch](fg:{0})\""],
        ["git_status"] = ["format = \"[$all_status$ahead_behind](fg:{0})\""],
        ["git_commit"] = ["only_detached = false", "format = \"[$hash](fg:{0})\""],
        ["git_state"] = ["format = \"[$state](fg:{0})\""],
        ["git_metrics"] = ["disabled = false", "format = \"[$added$deleted](fg:{0})\""],
        ["cmd_duration"] = ["format = \"[$duration](fg:{0})\""],
        ["status"] = ["disabled = false", "format = \"[$symbol](fg:{0})\""],
        ["time"] = ["disabled = false", "format = \"[$time](fg:{0})\""],
        ["jobs"] = ["format = \"[$number](fg:{0})\""],
        ["battery"] = ["format = \"[$symbol$percentage](fg:{0})\""],
        ["python"] = ["format = \"[$version](fg:{0})\""],
        ["nodejs"] = ["format = \"[$version](fg:{0})\""],
        ["rust"] = ["format = \"[$version](fg:{0})\""],
        ["golang"] = ["format = \"[$version](fg:{0})\""],
        ["package"] = ["format = \"[$version](fg:{0})\""],
    };

    /// <summary>
    /// 从 format 里按出现顺序提取要接管的模块与颜色（同一模块取首次出现的颜色，
    /// 没写 #RRGGBB 颜色的变量不接管）。清单外的手写变量不在模板里，保持原段落不动。
    /// </summary>
    private static List<(string Module, string Color)> ExtractManagedModules(string format)
    {
        var modules = new List<(string Module, string Color)>();
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (Match match in BlockRegex.Matches(format))
        {
            var inner = UnescapeStarship(match.Groups[1].Value);
            if (!StarshipVariables.IsVariable(inner)) continue;

            var module = inner[1..];
            if (!seen.Add(module)) continue;

            var hex = HexColorRegex.Match(match.Groups[2].Value).Value;
            if (hex.Length == 0 || !ManagedModuleTemplates.ContainsKey(module)) continue;

            modules.Add((module, hex.ToUpperInvariant()));
        }

        return modules;
    }

    /// <summary>
    /// 组装托管块：标记行 + 顶层 format + add_newline + 各模块段落（含颜色），段落间留空行。
    /// 接管 [directory] 段时显式写入 use_os_path_sep（false = 路径分隔符显示为 /），
    /// 两种取值都落盘，保证从文件读回时能还原选项。
    /// </summary>
    private static List<string> BuildManagedBlock(
        string format, bool addNewline, bool useForwardSlash, List<(string Module, string Color)> modules)
    {
        var block = new List<string>
        {
            StartMarker,
            NoticeLine,
            BuildFormatLine(format),
            "add_newline = " + (addNewline ? "true" : "false"),
        };
        foreach (var (module, color) in modules)
        {
            block.Add("[" + module + "]");
            foreach (var line in ManagedModuleTemplates[module])
            {
                block.Add(string.Format(CultureInfo.InvariantCulture, line, color));
            }

            if (module == "directory")
            {
                block.Add("use_os_path_sep = " + (useForwardSlash ? "false" : "true"));
            }

            block.Add(string.Empty);
        }

        block.Add(EndMarker);
        return block;
    }
}
