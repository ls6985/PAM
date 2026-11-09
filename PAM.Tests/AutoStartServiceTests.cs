using PAM.Core;
using Xunit;

namespace PAM.Tests;

/// <summary>
/// 登录启动项注册表值的纯逻辑测试：只校验字符串构造与解析，不读写真实注册表。
/// </summary>
public sealed class AutoStartServiceTests
{
    private const string ExePath = @"C:\Apps\PAM\PAM.Tray.exe";

    [Fact]
    public void BuildRunValue_QuotesAbsolutePath()
        => Assert.Equal("\"C:\\Apps\\PAM\\PAM.Tray.exe\"", AutoStartService.BuildRunValue(ExePath));

    [Fact]
    public void BuildRunValue_HandlesSpacesAndSurroundingQuotes()
        => Assert.Equal(
            "\"C:\\Program Files\\PAM\\PAM.Tray.exe\"",
            AutoStartService.BuildRunValue(" \"C:\\Program Files\\PAM\\PAM.Tray.exe\" "));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("\"C:\\Apps\\PAM\\PAM.Tray.exe\"", @"C:\Apps\PAM\PAM.Tray.exe")]
    [InlineData("C:\\Apps\\PAM\\PAM.Tray.exe", @"C:\Apps\PAM\PAM.Tray.exe")]
    [InlineData("\"C:\\Program Files\\PAM\\PAM.Tray.exe\" --minimized", @"C:\Program Files\PAM\PAM.Tray.exe")]
    [InlineData("C:\\Apps\\PAM\\PAM.Tray.exe --minimized", @"C:\Apps\PAM\PAM.Tray.exe")]
    [InlineData("\"unterminated", null)]
    public void ExtractExecutablePath_ParsesQuotedAndParameterizedValues(string? value, string? expected)
        => Assert.Equal(expected, AutoStartService.ExtractExecutablePath(value));

    [Theory]
    [InlineData("\"C:\\Apps\\PAM\\PAM.Tray.exe\"", true)]
    [InlineData("\"c:\\apps\\pam\\pam.tray.exe\"", true)]
    [InlineData("\"C:\\Apps\\PAM\\PAM.Tray.exe\" --minimized", true)]
    [InlineData("C:\\Apps\\PAM\\PAM.Tray.exe", true)]
    [InlineData("\"C:\\Apps\\Other\\PAM.Tray.exe\"", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("\"unterminated", false)]
    public void PointsTo_ComparesNormalizedPathsIgnoringCase(string? value, bool expected)
        => Assert.Equal(expected, AutoStartService.PointsTo(value, ExePath));
}
