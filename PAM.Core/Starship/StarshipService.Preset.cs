using System.Reflection;
using System.Text;

namespace PAM.Core;

public sealed partial class StarshipService
{
    /// <summary>内置预设清单。RequiresNerdFont 的预设需要先安装 Nerd Font，界面会提示。</summary>
    private static readonly StarshipPreset[] KnownPresets =
    [
        new("nerd-font-symbols", "Nerd Font 符号（需 Nerd Font）", true),
        new("pastel-powerline", "粉彩电力线（需 Nerd Font）", true),
        new("gruvbox-rainbow", "Gruvbox 彩虹（需 Nerd Font）", true),
        new("tokyo-night", "Tokyo Night（需 Nerd Font）", true),
        new("plain-text-symbols", "纯文本符号（无需特殊字体）", false),
        new("no-nerd-font", "无 Nerd Font 模式（无需特殊字体）", false),
    ];

    private static readonly string ResourcePrefix = typeof(StarshipService).Namespace + ".Presets.";

    /// <summary>内置预设列表（资源确实存在的才返回）。</summary>
    public IReadOnlyList<StarshipPreset> GetPresets()
    {
        var assembly = typeof(StarshipService).Assembly;
        var names = assembly.GetManifestResourceNames();

        return KnownPresets
            .Where(preset => names.Contains(ResourcePrefix + preset.Id + ".toml", StringComparer.Ordinal))
            .ToList();
    }

    /// <summary>解出内置预设 toml 内容。</summary>
    public string? ExtractPreset(string presetId)
    {
        var assembly = typeof(StarshipService).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + presetId + ".toml");
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
