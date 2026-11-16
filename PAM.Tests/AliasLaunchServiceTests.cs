using PAM.Core;
using Xunit;

namespace PAM.Tests;

/// <summary>
/// 别名启动器片段生成的纯逻辑测试，不读写文件。
/// </summary>
public sealed class AliasLaunchServiceTests
{
    private static AliasData Entry(string name, string path, string remark = "", string arguments = "")
        => new() { Name = name, Path = path, Remark = remark, Arguments = arguments };

    [Fact]
    public void BuildLines_GeneratesHelperAndGuardedFunction()
    {
        var lines = AliasLaunchService.BuildLines([Entry("bili", @"C:\App\bili.exe", "哔哩哔哩")]);
        var text = string.Join("\n", lines);

        Assert.Equal(AliasLaunchService.StartMarker, lines[0]);
        Assert.Equal(AliasLaunchService.EndMarker, lines[^1]);
        Assert.Contains("function Invoke-PamAlias {", text);
        Assert.Contains(@"Test-Path ""function:bili""", text);
        Assert.Contains(@"Test-Path ""alias:bili""", text);
        Assert.Contains(@"function bili { Invoke-PamAlias -Path ""C:\App\bili.exe"" -Remark ""哔哩哔哩"" -Rest $args }", text);
        Assert.Contains("aliasLaunchEnhanced", text);
        Assert.Contains("showAliasRemarkOnLaunch", text);
        Assert.Contains("hideTerminalAfterAliasLaunch", text);
        Assert.Contains(AliasLaunchService.HideBeforeCloseMarker, text);
        Assert.DoesNotContain("EventWaitHandle", text);
        Assert.Contains("Start-Process -FilePath 'explorer.exe'", text);
        Assert.Contains("New-Object -ComObject WScript.Shell", text);
        Assert.Contains("'.lnk'", text);
        Assert.Contains("catch {", text);
        Assert.DoesNotContain("-RedirectStandardOutput", text);
        Assert.Contains("& $Path @Rest", text);
    }

    [Fact]
    public void BuildLines_HidesThenClosesOnEverySuccessfulLaunchPath()
    {
        var lines = AliasLaunchService.BuildLines([Entry("bili", @"C:\App\bili.exe")]);
        var text = string.Join("\n", lines);

        Assert.Contains("$closeAfterLaunch = $false", text);
        Assert.Contains("if ($null -ne $cfg.hideTerminalAfterAliasLaunch)", text);
        Assert.Contains("Add-Type -Namespace PAM -Name Win32", text);
        Assert.Contains("CASCADIA_HOSTING_WINDOW_CLASS", text);
        Assert.Contains("ShowWindow($pamWindow, 0)", text);
        Assert.Contains("exit", text);
        Assert.Equal(
            4,
            lines.Count(static line => line.Contains("& $pamHideAndExit", StringComparison.Ordinal)));
        Assert.DoesNotContain("EventWaitHandle", text);
    }

    [Fact]
    public void BuildLines_DoesNotRemoveUserAliases()
    {
        var lines = AliasLaunchService.BuildLines([Entry("a", @"C:\a.exe")]);

        Assert.DoesNotContain(lines, static line => line.Contains("Remove-Item", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildLines_QuotesSpecialCharactersAndFlattensRemark()
    {
        var lines = AliasLaunchService.BuildLines([Entry("cash", @"C:\$money\a`b.exe", "第一行\n第二行")]);

        Assert.Contains(lines, static line => line.Contains(@"-Path ""C:\`$money\a``b.exe""", StringComparison.Ordinal));
        Assert.Contains(lines, static line => line.Contains(@"-Remark ""第一行 第二行""", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, static line => line.Contains("第一行\n第二行", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildLines_PrependsFixedArgumentsToRest()
    {
        var lines = AliasLaunchService.BuildLines([
            Entry("chrome", @"C:\App\chrome.exe", "浏览器", @"--incognito --user-data-dir=""D:\My Data"" ""C:\My File.txt"""),
        ]);

        Assert.Contains(lines, static line =>
            line.Contains(
                @"-Rest (@(""--incognito"", ""--user-data-dir=D:\My Data"", ""C:\My File.txt"") + $args)",
                StringComparison.Ordinal));
    }

    [Fact]
    public void BuildLines_EscapesFixedArguments()
    {
        var lines = AliasLaunchService.BuildLines([
            Entry("cash", @"C:\App\cash.exe", "", @"--path=""C:\$money\a`b"""),
        ]);

        Assert.Contains(lines, static line =>
            line.Contains(@"-Rest (@(""--path=C:\`$money\a``b"") + $args)", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildLines_WithBlankFixedArguments_KeepsBareArgs()
    {
        var lines = AliasLaunchService.BuildLines([Entry("bili", @"C:\App\bili.exe", "", "   ")]);

        Assert.Contains(lines, static line => line.Contains("-Rest $args }", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildLines_WithEmptyAliases_ReturnsEmpty()
        => Assert.Empty(AliasLaunchService.BuildLines([]));

    [Fact]
    public void BuildLines_KeepsMultipleAliasesInOrder()
    {
        var lines = AliasLaunchService.BuildLines([
            Entry("a", @"C:\a.exe"),
            Entry("b", @"C:\b.exe"),
        ]);

        var names = lines
            .Where(static line => line.StartsWith("    function ", StringComparison.Ordinal))
            .Select(static line => line["    function ".Length..].Split(' ')[0])
            .ToArray();

        Assert.Equal(["a", "b"], names);
    }
}
