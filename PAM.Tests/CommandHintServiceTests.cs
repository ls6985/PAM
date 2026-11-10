using PAM.Core;
using Xunit;

namespace PAM.Tests;

/// <summary>
/// 未知命令提示片段生成的纯逻辑测试，不读写文件。
/// </summary>
public sealed class CommandHintServiceTests
{
    [Fact]
    public void BuildLines_RegistersHandlerAndReadsSwitch()
    {
        var lines = CommandHintService.BuildLines();
        var text = string.Join("\n", lines);

        Assert.Equal(CommandHintService.StartMarker, lines[0]);
        Assert.Equal(CommandHintService.EndMarker, lines[^1]);
        Assert.Contains("$ExecutionContext.InvokeCommand.CommandNotFoundAction = {", text);
        Assert.Contains("friendlyCommandNotFound", text);
        Assert.Contains("if (-not $pamHintEnabled) { return }", text);
        Assert.Contains("$CommandLookupEventArgs.CommandScriptBlock = {", text);
        Assert.Contains("}.GetNewClosure()", text);
        Assert.Contains("$CommandLookupEventArgs.StopSearch = $true", text);
    }

    [Fact]
    public void BuildLines_ProvidesChineseAndEnglishHints()
    {
        var text = string.Join("\n", CommandHintService.BuildLines());

        Assert.Contains("未找到这个命令。输入 pls 查看已有别名", text);
        Assert.Contains("Command not found. Run 'pls' to list aliases", text);
        Assert.Contains("'en-US'", text);
        Assert.Contains("-ForegroundColor Cyan", text);
    }

    [Fact]
    public void BuildLines_IsStable()
        => Assert.Equal(CommandHintService.BuildLines(), CommandHintService.BuildLines());
}
