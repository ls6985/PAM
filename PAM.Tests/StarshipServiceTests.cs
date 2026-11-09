using System.IO;
using System.Text;
using PAM.Core;
using Xunit;

namespace PAM.Tests;

/// <summary>
/// Starship 服务的纯逻辑测试：$PROFILE init 区块的幂等写入、starship.toml 的备份与恢复、
/// 内置预设资源。配置路径注入到临时目录，不触碰真实的用户配置。
/// </summary>
public sealed class StarshipServiceTests : IDisposable
{
    private static readonly UTF8Encoding Utf8Bom = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _root;
    private readonly string _profilePath;
    private readonly string _configPath;
    private readonly StarshipService _starship;

    public StarshipServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "PAM.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _profilePath = Path.Combine(_root, "profile.ps1");
        _configPath = Path.Combine(_root, ".config", "starship.toml");
        _starship = new StarshipService(_configPath);
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

    private void WriteProfile(string content, Encoding? encoding = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_profilePath)!);
        File.WriteAllText(_profilePath, content, encoding ?? Utf8Bom);
    }

    // ---------------- $PROFILE init 区块 ----------------

    [Fact]
    public void EnsureProfileInit_AppendsBlockToMissingFile_WithUtf8BomAndCrLf()
    {
        var result = _starship.EnsureProfileInit(_profilePath);

        Assert.False(result.FileExisted);
        var text = File.ReadAllText(_profilePath);
        Assert.Contains(StarshipService.BlockStartMarker, text);
        Assert.Contains(StarshipService.InitLine, text);
        Assert.Contains(StarshipService.BlockEndMarker, text);
        Assert.True(File.ReadAllBytes(_profilePath).AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.Contains("\r\n", text);
    }

    [Fact]
    public void EnsureProfileInit_IsIdempotent_AndKeepsOtherContent()
    {
        WriteProfile("# 我的配置\r\nSet-Alias -Name rr -Value \"pwsh.exe\"\r\n");

        _starship.EnsureProfileInit(_profilePath);
        _starship.EnsureProfileInit(_profilePath);
        var text = File.ReadAllText(_profilePath);

        Assert.Single(Occurrences(text, StarshipService.BlockStartMarker));
        Assert.Single(Occurrences(text, StarshipService.InitLine));
        Assert.Contains("# 我的配置", text);
        Assert.Contains("Set-Alias -Name rr -Value \"pwsh.exe\"", text);
        Assert.True(text.IndexOf(StarshipService.BlockStartMarker, StringComparison.Ordinal)
            > text.IndexOf("Set-Alias -Name rr", StringComparison.Ordinal));
    }

    [Fact]
    public void EnsureProfileInit_ReplacesOldBlock_AndAbsorbsStrayInitLines()
    {
        WriteProfile(
            """
            # 手写的旧配置
            # >>> PAM Managed >>>
            Invoke-Expression (&starship init powershell)
            # >>> PAM Starship >>>
            Invoke-Expression (&starship init powershell)
            # 旧区块里的脏行
            # <<< PAM Starship <<<
            """);

        _starship.EnsureProfileInit(_profilePath);
        var text = File.ReadAllText(_profilePath);

        Assert.Single(Occurrences(text, "starship init"));
        Assert.DoesNotContain("# 旧区块里的脏行", text);
        Assert.Contains("# 手写的旧配置", text);
        Assert.True(_starship.HasInitLine(_profilePath));
    }

    [Fact]
    public void EnsureProfileInit_KeepsStrayInitLineInUserArea()
    {
        WriteProfile(
            """
            # 我的配置
            Invoke-Expression (&starship init powershell)
            # >>> PAM Managed >>>
            # >>> PAM Starship >>>
            Invoke-Expression (&starship init powershell)
            # <<< PAM Starship <<<
            """);

        _starship.EnsureProfileInit(_profilePath);
        var text = File.ReadAllText(_profilePath);

        Assert.Equal(2, Occurrences(text, "starship init").Count);
        Assert.Contains("# 我的配置", text);
        Assert.True(text.IndexOf(ProfileRenderer.StartMarker, StringComparison.Ordinal)
                    > text.IndexOf("# 我的配置", StringComparison.Ordinal));
    }

    [Fact]
    public void EnsureProfileInit_DoesNotTouchStrayInitLineInsidePamCustomRegion()
    {
        // PAM-Custom 区块里的内容归 CustomCommandService 管，Starship 侧不能动它。
        WriteProfile(
            """
            #region PAM-Custom
            # @pam-custom hello 打招呼
            function hello { Write-Host hi }
            # 手写在这里的 init 行也保持原样
            Invoke-Expression (&starship init powershell)
            #endregion
            """);

        _starship.EnsureProfileInit(_profilePath);
        var text = File.ReadAllText(_profilePath);

        Assert.Equal(2, Occurrences(text, "starship init").Count);
        // 源码文件在 Windows 上可能是 CRLF，raw string 字面量会带上 \r\n；
        // 断言只关心「区块后空一行接新块」，先归一化行尾再比较。
        Assert.Contains("#endregion\n\n# >>> PAM Starship >>>", text.Replace("\r\n", "\n"));
    }

    [Fact]
    public void EnsureProfileInit_PreservesNoBomUpgradeAndNewlineStyle()
    {
        // 无 BOM UTF-8 + LF：写回应升级为 BOM（兼容 5.1）并保留 LF 换行风格。
        WriteProfile("# 别删我\n$env:EDITOR = 'nvim'\n", Utf8NoBom);

        _starship.EnsureProfileInit(_profilePath);
        var bytes = File.ReadAllBytes(_profilePath);

        Assert.True(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        var text = File.ReadAllText(_profilePath);
        Assert.Contains("# 别删我", text);
        Assert.Contains("\n$env:EDITOR = 'nvim'\n", text);
        Assert.DoesNotContain("\r", text);
    }

    // ---------------- starship.toml 备份与恢复 ----------------

    [Fact]
    public void ApplyPreset_WritesConfigBacksUpOriginal_AndWritesInitLine()
    {
        WriteProfile("# profile\r\n");
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        File.WriteAllText(_configPath, "# 原始配置\nformat = \"$user@$hostname\"\n", Utf8NoBom);

        var presetId = Assert.Single(_starship.GetPresets(), p => p.Id == "no-nerd-font").Id;
        var result = _starship.ApplyPreset(presetId, _profilePath);

        Assert.True(result.ConfigWritten);
        Assert.True(result.ProfileUpdated);
        Assert.DoesNotContain("# 原始配置", File.ReadAllText(_configPath));
        Assert.Equal("# 原始配置\nformat = \"$user@$hostname\"\n", File.ReadAllText(_starship.GetBackupPath()));
        Assert.True(_starship.HasInitLine(_profilePath));

        var state = _starship.GetState(_profilePath);
        Assert.True(state.ConfigExists);
        Assert.True(state.BackupExists);
        Assert.True(state.ProfileHasInitLine);
    }

    [Fact]
    public void RestoreConfig_RestoresPreviousContent()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        File.WriteAllText(_configPath, "v1", Utf8NoBom);

        Assert.True(_starship.BackupConfig());
        File.WriteAllText(_configPath, "v2(被改坏)", Utf8NoBom);

        Assert.True(_starship.RestoreConfig());
        Assert.Equal("v1", File.ReadAllText(_configPath));
    }

    [Fact]
    public void BackupConfig_WithoutExistingFile_DoesNothing()
    {
        Assert.False(_starship.BackupConfig());
        Assert.False(File.Exists(_starship.GetBackupPath()));
        Assert.False(_starship.RestoreConfig());
    }

    [Fact]
    public void ApplyPreset_WithUnknownId_Throws()
    {
        WriteProfile("# profile\r\n");
        Assert.Throws<InvalidOperationException>(() => _starship.ApplyPreset("不存在的预设", _profilePath));
    }

    // ---------------- 主题预设识别 ----------------

    [Fact]
    public void DetectAppliedPreset_MatchesPristinePresetContent_AndRejectsModified()
    {
        Assert.Null(_starship.DetectAppliedPreset()); // 配置文件不存在

        var content = _starship.ExtractPreset("plain-text-symbols")!;
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        File.WriteAllText(_configPath, content, Utf8NoBom);
        Assert.Equal("plain-text-symbols", _starship.DetectAppliedPreset());

        // 之后追加内容（手动调整或应用自定义提示符）就不再识别为预设
        File.AppendAllText(_configPath, "\n[extra]\nkey = 1\n", Utf8NoBom);
        Assert.Null(_starship.DetectAppliedPreset());
    }

    // ---------------- 预设资源 ----------------

    [Fact]
    public void GetPresets_ContainsBundledOfficialPresets()
    {
        var ids = _starship.GetPresets().Select(p => p.Id).ToList();

        Assert.Contains("nerd-font-symbols", ids);
        Assert.Contains("gruvbox-rainbow", ids);
        Assert.Contains("tokyo-night", ids);
        Assert.Contains("plain-text-symbols", ids);
        Assert.Contains("no-nerd-font", ids);
    }

    [Fact]
    public void ExtractPreset_ReturnsNonEmptyToml_ForEveryPreset()
    {
        foreach (var preset in _starship.GetPresets())
        {
            var content = _starship.ExtractPreset(preset.Id);
            Assert.NotNull(content);
            Assert.NotEmpty(content!.Trim());
            // 预设要么声明需要 Nerd Font，要么显式标注无需特殊字体。
            Assert.Equal(preset.RequiresNerdFont, !preset.DisplayNameSource.Contains("无需特殊字体", StringComparison.Ordinal));
        }
    }

    private static List<int> Occurrences(string text, string needle)
    {
        var positions = new List<int>();
        var index = text.IndexOf(needle, StringComparison.Ordinal);
        while (index >= 0)
        {
            positions.Add(index);
            index = text.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }

        return positions;
    }
}
