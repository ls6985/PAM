using PAM.Core;
using Xunit;

namespace PAM.Tests;

/// <summary>
/// 自定义命令片段生成、内置命令定义与名称校验的纯逻辑测试，不读写文件。
/// </summary>
public sealed class CustomCommandServiceTests
{
    private static readonly string[] BuiltinNames = ["pls", "ongit", "sudo"];

    [Fact]
    public void TryValidateName_RejectsAllBuiltinCommandNames()
    {
        foreach (var name in BuiltinNames)
        {
            Assert.False(CustomCommandService.TryValidateName(name, out _));
            Assert.False(CustomCommandService.TryValidateName(name.ToUpperInvariant(), out var error));
            Assert.Contains(name.ToUpperInvariant(), error);
        }

        Assert.True(CustomCommandService.TryValidateName("pls2", out _));
        Assert.True(CustomCommandService.TryValidateName("sudo2", out _));
    }

    [Fact]
    public void TryValidateName_RejectsInvalidFunctionNames()
    {
        Assert.False(CustomCommandService.TryValidateName("has space", out _));
        Assert.False(CustomCommandService.TryValidateName("bad$name", out _));
        Assert.False(CustomCommandService.TryValidateName("bad;name", out _));
        Assert.False(CustomCommandService.TryValidateName("  ", out _));
        Assert.True(CustomCommandService.TryValidateName("good-name_2", out _));
    }

    [Fact]
    public void IsBuiltinName_IsCaseInsensitiveAndCoversAllBuiltins()
    {
        Assert.Equal(BuiltinNames.Length, CustomCommandService.BuiltinCommandNames.Count);
        foreach (var name in BuiltinNames)
        {
            Assert.Contains(name, CustomCommandService.BuiltinCommandNames);
            Assert.True(CustomCommandService.IsBuiltinName(name));
            Assert.True(CustomCommandService.IsBuiltinName(name.ToUpperInvariant()));
        }

        Assert.False(CustomCommandService.IsBuiltinName("sudo2"));
        Assert.False(CustomCommandService.IsBuiltinName(null));
    }

    [Fact]
    public void GetBuiltinEntries_AreReadOnlyAndCoverDefinitionTable()
    {
        var entries = CustomCommandService.GetBuiltinEntries();

        Assert.Equal(BuiltinNames, entries.Select(static entry => entry.Name).ToArray());
        Assert.All(entries, static entry => Assert.True(entry.IsBuiltin));
        Assert.All(entries, static entry => Assert.True(entry.IsEnabled));
    }

    [Fact]
    public void BuiltinCode_DoesNotMatchPlsAliasScanner()
    {
        // 内置函数体若含字面量 Invoke-PamAlias -Path "…"，会被 pls 当成别名列出。
        const string scanner = @"Invoke-PamAlias\s+-Path\s+""(?<path>[^""]*)""";

        foreach (var entry in CustomCommandService.GetBuiltinEntries())
        {
            Assert.DoesNotMatch(scanner, entry.Code);
        }
    }

    [Fact]
    public void BuildLines_AlwaysAppendsAllBuiltins()
    {
        var lines = CustomCommandService.BuildLines([]);

        Assert.Equal("#region PAM-Custom", lines[0]);
        Assert.Equal("#endregion", lines[^1]);
        foreach (var name in BuiltinNames)
        {
            Assert.Contains(lines, line => line.Contains($"function {name} {{", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void BuildLines_SkipsBuiltinNamesFromData()
    {
        var lines = CustomCommandService.BuildLines([
            new CustomCommandData { Name = "pls", CodeLines = ["function pls { }"] },
            new CustomCommandData { Name = "hello", CodeLines = ["function hello { }"] },
        ]);

        Assert.Contains("# @pam-custom hello", lines);
        Assert.DoesNotContain(lines, static line => line == "function pls { }");
        Assert.Single(lines, static line => line.Contains("function pls {", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BuildLines_RoundTripsMultiLineCodeWithIndentationAndBlankLines(bool isEnabled)
    {
        var code = new[]
        {
            "function gpr {",
            "    git pull --rebase",
            "",
            "    if ($?) {",
            "\tWrite-Host \"完成\"",
            "    }",
            "}",
        };

        var lines = CustomCommandService.BuildLines([
            new CustomCommandData
            {
                Name = "gpr",
                CodeLines = code,
                IsEnabled = isEnabled,
                Remark = "拉取并变基",
            },
        ]);

        Assert.Contains("# @pam-custom gpr 拉取并变基", lines);
        Assert.Equal(!isEnabled, lines.Contains("# @pam-custom-state disabled"));

        foreach (var line in code)
        {
            Assert.Contains(isEnabled ? line : (line.Length == 0 ? "#" : "# " + line), lines);
        }

        Assert.Contains("# @pam-custom-end", lines);
    }

    [Theory]
    [InlineData("\"C:\\a b.exe\"", "C:\\a b.exe")]
    [InlineData("'C:\\a b.exe'", "C:\\a b.exe")]
    [InlineData("  \"C:\\padded\"  ", "C:\\padded")]
    [InlineData("'C:\\mismatch\"", "'C:\\mismatch\"")]
    [InlineData("\"\"", "")]
    [InlineData("\"", "\"")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void NormalizePath_StripsMatchingSurroundingQuotes(string? input, string expected)
        => Assert.Equal(expected, ProfileService.NormalizePath(input));
}
