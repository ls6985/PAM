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
    public void BuildLines_WrapsBodyInSameNameFunction(bool isEnabled)
    {
        var code = new[]
        {
            "git pull --rebase",
            "",
            "if ($?) {",
            "\tWrite-Host \"完成\"",
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

        // 命令体必须包在同名函数里，否则裸命令会在每次启动终端时立即执行。
        Assert.Contains(isEnabled ? "function gpr {" : "# function gpr {", lines);
        Assert.Contains(isEnabled ? "}" : "# }", lines);

        foreach (var line in code)
        {
            Assert.Contains(isEnabled ? line : (line.Length == 0 ? "#" : "# " + line), lines);
        }

        Assert.Contains("# @pam-custom-end", lines);
    }

    [Fact]
    public void BuildLines_EmitsBodyWrappedInSameNameFunction()
    {
        var lines = CustomCommandService.BuildLines([
            new CustomCommandData { Name = "p", CodeLines = ["ping -t 8.8.8.8"] },
        ]);

        var index = lines.IndexOf("function p {");
        Assert.True(index > 0, "命令体必须包在同名函数里，否则会在启动终端时立即执行。");
        Assert.Equal("ping -t 8.8.8.8", lines[index + 1]);
        Assert.Equal("}", lines[index + 2]);
        Assert.Equal("# @pam-custom-end", lines[index + 3]);
    }

    [Fact]
    public void BuildLines_AppendsFixedArgumentsAndArgsToLastLine()
    {
        var lines = CustomCommandService.BuildLines([
            new CustomCommandData
            {
                Name = "p",
                CodeLines = ["ping -t 8.8.8.8"],
                AllowsArguments = true,
                Arguments = "-n 4",
            },
        ]);

        Assert.Contains("function p {", lines);
        Assert.Contains("ping -t 8.8.8.8 -n 4 @args", lines);
    }

    [Fact]
    public void BuildLines_DoesNotAppendArgumentsWhenDisabled()
    {
        var lines = CustomCommandService.BuildLines([
            new CustomCommandData
            {
                Name = "p",
                CodeLines = ["ping -t 8.8.8.8", "Write-Host done"],
                Arguments = "-n 4",
            },
        ]);

        Assert.Contains("ping -t 8.8.8.8", lines);
        Assert.Contains("Write-Host done", lines);
        Assert.DoesNotContain(lines, static line => line.Contains("@args", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("function gpr {\r\n    git pull --rebase\r\n}", "git pull --rebase")]
    [InlineData("function global:gpr { git pull }", "git pull")]
    [InlineData("  function gpr { git pull }  ", "git pull")]
    [InlineData("git status", "git status")]
    [InlineData("function other { git pull }", "function other { git pull }")]
    [InlineData("function gpr { git pull", "function gpr { git pull")]
    public void StripOuterFunction_RemovesOnlyMatchingWrapper(string code, string expected)
        => Assert.Equal(expected, CustomCommandCode.StripOuterFunction("gpr", code));

    [Fact]
    public void StripOuterFunction_KeepsHereStringTerminatorAtLineStart()
    {
        var code = "function note {\n$text = @\"\nhello\n\"@\nWrite-Host $text\n}";
        var body = CustomCommandCode.StripOuterFunction("note", code);

        Assert.Contains("\n\"@\n", body);
        Assert.DoesNotContain("function note", body);
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
