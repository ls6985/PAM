using System.Text;
using PAM.Core;
using PAM.Core.Models;
using Xunit;

namespace PAM.Tests;

/// <summary>
/// 别名启动器区块的读写：幂等、空列表整体移除、不破坏 PAM-Custom / Set-Alias / 编码 / 换行 / 转义。
/// 所有文件都在临时目录，不会碰真实的 $PROFILE。
/// </summary>
public sealed class AliasLaunchServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _profilePath;

    public AliasLaunchServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "PAM.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _profilePath = Path.Combine(_root, "Microsoft.PowerShell_profile.ps1");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // 临时目录被占用时交给系统清理。
        }
    }

    private static AliasEntry Entry(string name, string path, string remark = "")
        => new(name, path, remark);

    private void WriteProfile(string text) => File.WriteAllText(_profilePath, text, new UTF8Encoding(true));

    private string ReadProfile() => File.ReadAllText(_profilePath);

    private static int Count(string text, string marker)
        => text.Split('\n').Count(line => line.TrimEnd('\r') == marker);

    [Fact]
    public void Sync_AppendsLauncherRegion_AndKeepsSetAliasLines()
    {
        WriteProfile("# @alias-meta bili 哔哩哔哩\r\nSet-Alias -Name bili -Value \"C:\\App\\bili.exe\"\r\n");

        AliasLaunchService.Sync(_profilePath, [Entry("bili", @"C:\App\bili.exe", "哔哩哔哩")]);

        var text = ReadProfile();
        Assert.Equal(1, Count(text, AliasLaunchService.StartMarker));
        Assert.Equal(1, Count(text, AliasLaunchService.EndMarker));
        Assert.Contains("function Invoke-PamAlias {", text);
        Assert.Contains("Remove-Item -Path \"Alias:bili\" -ErrorAction SilentlyContinue", text);
        Assert.Contains("function bili { Invoke-PamAlias -Path \"C:\\App\\bili.exe\" -Remark \"哔哩哔哩\" -Rest $args }", text);
        Assert.Contains("Set-Alias -Name bili -Value \"C:\\App\\bili.exe\"", text);
        Assert.Contains("aliasLaunchEnhanced", text);
        Assert.Contains("showAliasRemarkOnLaunch", text);
        // 增强模式需使子进程脱离终端进程树：无参数由 explorer.exe 启动，带参数经临时 .lnk 转交 explorer。
        // 将输出重定向到 NUL 仅能抑制日志，无法改变控制台归属，当前实现不再采用该方式。
        Assert.Contains("Start-Process -FilePath 'explorer.exe'", text);
        Assert.Contains("New-Object -ComObject WScript.Shell", text);
        Assert.Contains("'.lnk'", text);
        Assert.Contains("catch {", text); // COM / explorer 失败时退回普通启动
        Assert.DoesNotContain("-RedirectStandardOutput", text);
        Assert.DoesNotContain("NUL", text);
        // 开关关闭时恢复原来的直接启动。
        Assert.Contains("& $Path @Rest", text);
    }

    [Fact]
    public void Sync_IsIdempotent()
    {
        WriteProfile("# @alias-meta a 备注\nSet-Alias -Name a -Value \"C:\\a.exe\"\n");
        var entries = new[] { Entry("a", @"C:\a.exe", "备注") };

        AliasLaunchService.Sync(_profilePath, entries);
        var first = ReadProfile();
        AliasLaunchService.Sync(_profilePath, entries);
        var second = ReadProfile();

        Assert.Equal(first, second);
        Assert.Equal(1, Count(second, AliasLaunchService.StartMarker));
        Assert.Equal(1, Count(second, "function Invoke-PamAlias {"));
    }

    [Fact]
    public void Sync_WithEmptyEntries_RemovesWholeRegion()
    {
        WriteProfile("# @alias-meta a 备注\nSet-Alias -Name a -Value \"C:\\a.exe\"\n");
        AliasLaunchService.Sync(_profilePath, [Entry("a", @"C:\a.exe", "备注")]);
        Assert.Contains(AliasLaunchService.StartMarker, ReadProfile());

        AliasLaunchService.Sync(_profilePath, []);

        var text = ReadProfile();
        Assert.DoesNotContain(AliasLaunchService.StartMarker, text);
        Assert.DoesNotContain("Invoke-PamAlias", text);
        Assert.Contains("Set-Alias -Name a -Value \"C:\\a.exe\"", text);
    }

    [Fact]
    public void Sync_KeepsPamCustomRegionAndOtherContent()
    {
        const string custom = "#region PAM-Custom\nfunction hello { Write-Host \"hi\" }\n#endregion";
        WriteProfile(custom + "\n# @alias-meta a 备注\nSet-Alias -Name a -Value \"C:\\a.exe\"\n");

        AliasLaunchService.Sync(_profilePath, [Entry("a", @"C:\a.exe", "备注")]);

        var text = ReadProfile();
        Assert.Contains(custom, text);
        Assert.Contains("# @alias-meta a 备注", text);
    }

    [Fact]
    public void Sync_PreservesBomAndCrlf()
    {
        WriteProfile("# comment\r\n# @alias-meta a 备注\r\nSet-Alias -Name a -Value \"C:\\a.exe\"\r\n");

        AliasLaunchService.Sync(_profilePath, [Entry("a", @"C:\a.exe", "备注")]);

        var bytes = File.ReadAllBytes(_profilePath);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var text = Encoding.UTF8.GetString(bytes[3..]);
        Assert.Contains("\r\n", text);
        Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty));
    }

    [Fact]
    public void Sync_KeepsLfNewLineStyle()
    {
        WriteProfile("# comment\n# @alias-meta a 备注\nSet-Alias -Name a -Value \"C:\\a.exe\"\n");

        AliasLaunchService.Sync(_profilePath, [Entry("a", @"C:\a.exe", "备注")]);

        var bytes = File.ReadAllBytes(_profilePath);
        var text = Encoding.UTF8.GetString(bytes[3..]);
        Assert.DoesNotContain("\r\n", text);
    }

    [Fact]
    public void Sync_QuotesSpecialCharactersAndFlattensRemark()
    {
        WriteProfile("# initial\n");

        AliasLaunchService.Sync(_profilePath,
            [Entry("cash", @"C:\$money\a`b.exe", "第一行\n第二行")]);

        var text = ReadProfile();
        Assert.Contains("-Path \"C:\\`$money\\a``b.exe\"", text);
        Assert.Contains("-Remark \"第一行 第二行\"", text);
        Assert.DoesNotContain("第一行\n第二行", text);
    }
}
