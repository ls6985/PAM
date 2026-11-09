using System.Text;
using PAM.Core;
using Xunit;

namespace PAM.Tests;

/// <summary>
/// 托管区渲染：上下分区、幂等、旧生成块归拢、同名避让与编码保持。
/// 所有文件都在临时目录，不会碰真实的 $PROFILE。
/// </summary>
public sealed class ProfileRendererTests : IDisposable
{
    private readonly string _root;
    private readonly string _profilePath;

    public ProfileRendererTests()
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
            // 临时目录被占用时交给系统清理，不影响测试结论。
        }
    }

    private void WriteProfile(string text) => File.WriteAllText(_profilePath, text, new UTF8Encoding(true));

    private string ReadProfile() => File.ReadAllText(_profilePath);

    private static int Count(string text, string marker)
        => text.Split('\n').Count(line => line.TrimEnd('\r') == marker);

    private static PamData SampleData() => new()
    {
        Aliases =
        [
            new AliasData { Name = "blbl", Path = @"C:\App\bilibili.exe", Remark = "打开哔哩哔哩" },
        ],
        CustomCommands =
        [
            new CustomCommandData
            {
                Name = "hello",
                Remark = "打招呼",
                CodeLines = ["function hello {", "    Write-Host 'hi'", "}"],
            },
        ],
    };

    [Fact]
    public void Render_CreatesManagedSection_AndKeepsUserArea()
    {
        const string userContent = "# 我的配置\r\nSet-Alias ll 'Get-ChildItem'\r\n$PSStyle.OutputRendering = 'PlainText'\r\n";
        WriteProfile(userContent);

        var result = ProfileRenderer.Render(_profilePath, SampleData());
        var text = ReadProfile();

        Assert.False(result.FileCreated);
        Assert.StartsWith("# 我的配置\r\nSet-Alias ll 'Get-ChildItem'\r\n", text);
        Assert.Equal(1, Count(text, ProfileRenderer.StartMarker));
        Assert.True(text.IndexOf(ProfileRenderer.StartMarker, StringComparison.Ordinal)
                    > text.IndexOf("Set-Alias ll", StringComparison.Ordinal));
        Assert.Contains("function blbl { Invoke-PamAlias", text);
        Assert.Contains("#region PAM-Custom", text);
        Assert.Contains("function pls {", text);
        Assert.Contains("function ongit {", text);
        Assert.Contains("function sudo {", text);
        Assert.Empty(result.ConflictingNames);
    }

    [Fact]
    public void Render_IsIdempotent()
    {
        WriteProfile("# 我的配置\n");
        var data = SampleData();

        ProfileRenderer.Render(_profilePath, data);
        var first = ReadProfile();
        var firstHash = ProfileRenderer.Inspect(_profilePath).Hash;
        ProfileRenderer.Render(_profilePath, data);
        var second = ReadProfile();

        Assert.Equal(first, second);
        Assert.Equal(firstHash, ProfileRenderer.Inspect(_profilePath).Hash);
        Assert.Equal(1, Count(second, ProfileRenderer.StartMarker));
    }

    [Fact]
    public void Render_StripsLegacyGeneratedBlocksFromUserArea()
    {
        WriteProfile(string.Join("\n",
            "# 我的配置",
            "Set-Alias legacy 'C:\\old.exe'",
            "# >>> PAM Alias Launcher >>>",
            "function legacy { }",
            "# <<< PAM Alias Launcher <<<",
            "#region PAM-Custom",
            "# @pam-custom old 旧命令",
            "function old { }",
            "# @pam-custom-end",
            "#endregion",
            "# 用户写在末尾的内容"));

        ProfileRenderer.Render(_profilePath, SampleData());
        var text = ReadProfile();

        Assert.Equal(1, Count(text, AliasLaunchService.StartMarker));
        Assert.Equal(1, Count(text, "#region PAM-Custom"));
        Assert.Contains("Set-Alias legacy 'C:\\old.exe'", text);
        Assert.Contains("# 用户写在末尾的内容", text);
        Assert.DoesNotContain("function old { }", text);
        Assert.DoesNotContain("# @pam-custom old", text);
        Assert.True(text.IndexOf("# 我的配置", StringComparison.Ordinal)
                    < text.IndexOf(ProfileRenderer.StartMarker, StringComparison.Ordinal));
    }

    [Fact]
    public void Render_PreservesStarshipBlockInsideManagedArea()
    {
        WriteProfile(string.Join("\n",
            "# 我的配置",
            "# >>> PAM Starship >>>",
            "Invoke-Expression (&starship init powershell)",
            "# <<< PAM Starship <<<",
            ""));

        ProfileRenderer.Render(_profilePath, SampleData());
        var text = ReadProfile();

        Assert.Equal(1, Count(text, StarshipService.BlockStartMarker));
        Assert.Contains("Invoke-Expression (&starship init powershell)", text);
        Assert.True(text.IndexOf(StarshipService.BlockStartMarker, StringComparison.Ordinal)
                    > text.IndexOf(ProfileRenderer.StartMarker, StringComparison.Ordinal));
    }

    [Fact]
    public void Render_OverridesExternalEditsInManagedArea()
    {
        WriteProfile("# 我的配置\n");
        var rendered = ProfileRenderer.Render(_profilePath, SampleData());
        Assert.Equal(rendered.ManagedHash, ProfileRenderer.Inspect(_profilePath).Hash);

        var tampered = ReadProfile().Replace("打开哔哩哔哩", "手改的备注");
        WriteProfile(tampered);
        Assert.NotEqual(rendered.ManagedHash, ProfileRenderer.Inspect(_profilePath).Hash);

        ProfileRenderer.Render(_profilePath, SampleData());

        Assert.Contains("打开哔哩哔哩", ReadProfile());
        Assert.DoesNotContain("手改的备注", ReadProfile());
    }

    [Fact]
    public void Render_GeneratesGuardedFunctionsWithoutRemovingUserAliases()
    {
        WriteProfile("# 我的配置\n");

        ProfileRenderer.Render(_profilePath, SampleData());
        var text = ReadProfile();

        Assert.Contains("Get-Command -Name \"blbl\" -CommandType Alias,Function", text);
        Assert.DoesNotContain("Remove-Item -Path \"Alias:", text);
        Assert.Contains("-Path \"C:\\App\\bilibili.exe\" -Remark \"打开哔哩哔哩\" -Rest $args", text);
    }

    [Fact]
    public void Render_ReportsUserAreaNameConflicts()
    {
        WriteProfile(string.Join("\n",
            "Set-Alias blbl 'C:\\mine.exe'",
            "function hello { }",
            ""));

        var result = ProfileRenderer.Render(_profilePath, new PamData
        {
            Aliases = [new AliasData { Name = "blbl", Path = @"C:\other.exe" }],
        });

        Assert.Equal(["blbl"], result.ConflictingNames);
    }

    [Fact]
    public void Render_WithEmptyAliases_OmitsLauncherBlock()
    {
        WriteProfile("# 我的配置\n");

        ProfileRenderer.Render(_profilePath, new PamData());
        var text = ReadProfile();

        Assert.DoesNotContain(AliasLaunchService.StartMarker, text);
        Assert.Contains("#region PAM-Custom", text);
        Assert.Contains("function pls {", text);
    }

    [Fact]
    public void Render_PreservesBomAndCrlf()
    {
        WriteProfile("# 我的配置\r\nSet-Alias ll 'Get-ChildItem'\r\n");

        ProfileRenderer.Render(_profilePath, SampleData());

        var bytes = File.ReadAllBytes(_profilePath);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var text = Encoding.UTF8.GetString(bytes[3..]);
        Assert.Contains("\r\n", text);
        Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty));
    }

    [Fact]
    public void Render_KeepsLfNewLineStyle()
    {
        WriteProfile("# 我的配置\nSet-Alias ll 'Get-ChildItem'\n");

        ProfileRenderer.Render(_profilePath, SampleData());

        Assert.DoesNotContain("\r\n", ReadProfile());
    }

    [Fact]
    public void Render_CreatesMissingFile()
    {
        var result = ProfileRenderer.Render(_profilePath, SampleData());

        Assert.True(result.FileCreated);
        Assert.True(File.Exists(_profilePath));
        Assert.Contains(ProfileRenderer.StartMarker, ReadProfile());
    }

    [Fact]
    public void Inspect_WithoutMarker_ReturnsEmptyHash()
    {
        WriteProfile("# 只有用户配置\n");

        var state = ProfileRenderer.Inspect(_profilePath);

        Assert.False(state.HasMarker);
        Assert.Equal(string.Empty, state.Hash);
    }

    [Fact]
    public void Render_RejectsBlankPath()
        => Assert.Throws<ArgumentException>(() => ProfileRenderer.Render("  ", new PamData()));
}
