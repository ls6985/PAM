using System.Globalization;
using System.IO;
using System.Text;
using PAM.Core;
using PAM.Core.Models;
using Xunit;

namespace PAM.Tests;

/// <summary>
/// $PROFILE 读写（别名 + 自定义命令受控区块）的纯逻辑测试。
/// 每个测试用例使用独立的临时目录，不触碰真实的 $PROFILE。
/// </summary>
public sealed class ProfileServiceTests : IDisposable
{
    private static readonly UTF8Encoding Utf8Bom = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly Encoding Ansi = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage);

    private const string RegionHeader = "#region PAM-Custom";
    private const string RegionFooter = "#endregion";

    private readonly string _root;
    private readonly string _profilePath;
    private readonly ProfileService _profile = new();
    private readonly CustomCommandService _custom = new();

    public ProfileServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "PAM.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _profilePath = Path.Combine(_root, "profile.ps1");
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

    // ---------------- 区块管理 ----------------

    [Fact]
    public void CustomSave_AppendsRegionWhenFileHasNoRegion_AndKeepsOtherContent()
    {
        var outside = new[]
        {
            "# 我的 PowerShell 配置",
            @"$env:PATH = ""$env:PATH;C:\tools""",
            @"Set-Alias -Name blbl -Value ""C:\Program Files\app.exe""",
        };
        WriteProfile(Block(outside));

        var result = _custom.Save(_profilePath,
            [new CustomCommandEntry("hello", "function hello {\n    Write-Host \"hi\"\n}", remark: "打招呼")]);

        Assert.False(result.FileCreated);
        Assert.Equal(1, result.EntryCount);

        var lines = ProfileLines();
        Assert.Equal(outside, lines[..3]);
        Assert.Equal(string.Empty, lines[3]);
        Assert.Equal(RegionHeader, lines[4]);
        Assert.Equal(RegionFooter, lines[^1]);
        Assert.Single(lines, IsRegionHeader);
        Assert.Single(lines, IsRegionFooter);
        Assert.Contains("# @pam-custom hello 打招呼", lines);
        Assert.Contains("function hello {", lines);

        var entry = Assert.Single(_custom.Load(_profilePath).Entries);
        Assert.Equal("hello", entry.Name);
        Assert.Equal("打招呼", entry.Remark);
        Assert.Equal("function hello {\n    Write-Host \"hi\"\n}", entry.Code);
    }

    [Fact]
    public void CustomSave_ReplacesExistingRegion_AndKeepsContentOutside()
    {
        WriteProfile(Block(
            "Write-Host \"启动\"",
            RegionHeader,
            "# @pam-custom old1 旧命令",
            @"function old1 { Write-Host ""old1"" }",
            "# @pam-custom-end",
            "# @pam-custom old2",
            "# @pam-custom-state disabled",
            "# function old2 {",
            @"#     Write-Host ""old2""",
            "# }",
            "# @pam-custom-end",
            RegionFooter,
            @"Set-Alias -Name tail -Value 'C:\tail.exe'"));

        var result = _custom.Save(_profilePath,
        [
            new CustomCommandEntry("gpr", "function gpr {\n    git pull --rebase\n\n    Write-Host \"done\"\n}", remark: "拉取并变基"),
            new CustomCommandEntry("hello", @"function hello { Write-Host ""hi"" }"),
        ]);

        Assert.Equal(2, result.EntryCount);

        var text = File.ReadAllText(_profilePath);
        Assert.DoesNotContain("old1", text);
        Assert.DoesNotContain("old2", text);
        Assert.DoesNotContain("# @pam-custom-state", text);

        var lines = ProfileLines();
        Assert.Equal("Write-Host \"启动\"", lines[0]);
        Assert.Contains(@"Set-Alias -Name tail -Value 'C:\tail.exe'", lines);
        Assert.Equal(RegionFooter, lines[^1]);
        Assert.Single(lines, IsRegionHeader);
        Assert.Single(lines, IsRegionFooter);

        var entries = _custom.Load(_profilePath).Entries;
        Assert.Equal(new[] { "gpr", "hello" }, entries.Select(static e => e.Name).ToArray());
    }

    [Fact]
    public void CustomSave_WithEmptyList_RemovesWholeRegion()
    {
        WriteProfile(Block(
            "Write-Host \"x\"",
            RegionHeader,
            "# @pam-custom a",
            "function a { 1 }",
            "# @pam-custom-end",
            RegionFooter,
            "# 结尾"));

        var result = _custom.Save(_profilePath, Array.Empty<CustomCommandEntry>());

        Assert.Equal(0, result.EntryCount);
        Assert.Equal(new[] { "Write-Host \"x\"", "# 结尾" }, ProfileLines());
        Assert.Empty(_custom.Load(_profilePath).Entries);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CustomSave_RoundTripsMultiLineCodeWithIndentationAndBlankLines(bool isEnabled)
    {
        const string code = "function gpr {\n" +
                            "    git pull --rebase\n" +
                            "\n" +
                            "    if ($?) {\n" +
                            "\tWrite-Host \"完成\"\n" +
                            "    }\n" +
                            "}";

        _custom.Save(_profilePath, [new CustomCommandEntry("gpr", code, isEnabled, "拉取并变基")]);

        var lines = ProfileLines();
        Assert.Contains("# @pam-custom gpr 拉取并变基", lines);
        Assert.Equal(isEnabled, !lines.Contains("# @pam-custom-state disabled"));

        foreach (var line in code.Split('\n'))
        {
            Assert.Contains(isEnabled ? line : (line.Length == 0 ? "#" : "# " + line), lines);
        }

        var loaded = Assert.Single(_custom.Load(_profilePath).Entries);
        Assert.Equal("gpr", loaded.Name);
        Assert.Equal(isEnabled, loaded.IsEnabled);
        Assert.Equal(code, loaded.Code);

        // 重新启用后代码原样还原，缩进与空行不丢失。
        loaded.IsEnabled = true;
        _custom.Save(_profilePath, [loaded]);
        Assert.Equal(code, Assert.Single(_custom.Load(_profilePath).Entries).Code);
    }

    [Fact]
    public void AliasSave_And_CustomSave_DoNotBreakEachOther()
    {
        WriteProfile(Block(
            RegionHeader,
            "# @pam-custom inside",
            @"Set-Alias -Name inside -Value ""C:\inside.exe""",
            "# @pam-custom-end",
            RegionFooter));

        _profile.Save(_profilePath, [new AliasEntry("blbl", @"C:\Program Files\app.exe", "打开哔哩哔哩")]);

        var lines = ProfileLines();
        Assert.Contains(@"Set-Alias -Name inside -Value ""C:\inside.exe""", lines);
        Assert.Equal(RegionHeader, lines[0]);
        Assert.Single(lines, IsRegionHeader);
        Assert.Contains(@"Set-Alias -Name blbl -Value ""C:\Program Files\app.exe""", lines);

        // 区块内的 Set-Alias 由自定义命令负责，不参与别名读取。
        Assert.Equal(new[] { "blbl" }, _profile.Load(_profilePath).Entries.Select(static e => e.Name).ToArray());

        _custom.Save(_profilePath, [new CustomCommandEntry("hello", "function hello { 1 }")]);

        var alias = Assert.Single(_profile.Load(_profilePath).Entries);
        Assert.Equal("blbl", alias.Name);
        Assert.Equal(@"C:\Program Files\app.exe", alias.Path);
        Assert.Equal("打开哔哩哔哩", alias.Remark);
        Assert.Single(_custom.Load(_profilePath).Entries);
        Assert.Contains("# @alias-meta blbl 打开哔哩哔哩", ProfileLines());
    }

    [Fact]
    public void CustomSave_PutsRegionAfterAliasBlock()
    {
        WriteProfile(Block("Write-Host \"开头\""));

        _custom.Save(_profilePath, [new CustomCommandEntry("gpr", "function gpr { 1 }")]);
        _profile.Save(_profilePath, [new AliasEntry("blbl", @"C:\app.exe")]);

        var lines = ProfileLines();
        var footer = Array.IndexOf(lines, RegionFooter);
        var meta = Array.IndexOf(lines, "# @alias-meta blbl");

        Assert.True(footer >= 0 && meta > footer, "别名追加后应位于区块结束标记之后");
        Assert.Single(lines, IsRegionHeader);
    }

    // ---------------- 别名路径解析与转义 ----------------

    [Fact]
    public void Load_ParsesDoubleQuotedSingleQuotedAndUnquotedAliasValues()
    {
        var cases = new[]
        {
            new[] { @"Set-Alias -Name dbl -Value ""C:\Program Files\app.exe""", @"C:\Program Files\app.exe" },
            new[] { @"Set-Alias sq 'C:\Tools\My App\app.exe'", @"C:\Tools\My App\app.exe" },
            new[] { @"Set-Alias bare C:\Windows\system32\notepad.exe", @"C:\Windows\system32\notepad.exe" },
            new[] { @"set-alias -name lower -value ""C:\case.exe""", @"C:\case.exe" },
            new[] { @"Set-Alias dollar ""C:\weird`$dir\app.exe""", @"C:\weird$dir\app.exe" },
            new[] { @"Set-Alias tick ""C:``tick\app.exe""", @"C:`tick\app.exe" },
            new[] { @"Set-Alias quote ""C:\file`""name.exe""", @"C:\file""name.exe" },
            new[] { @"Set-Alias doubled 'C:\it''s\app.exe'", @"C:\it's\app.exe" },
        };

        foreach (var testCase in cases)
        {
            WriteProfile(Block("# 配置", testCase[0], "Write-Host \"结尾\""));

            var entry = Assert.Single(_profile.Load(_profilePath).Entries);
            Assert.Equal(testCase[1], entry.Path);
        }
    }

    [Fact]
    public void Save_WritesUniformlyDoubleQuotedAndEscapedPaths()
    {
        var entries = new[]
        {
            new AliasEntry("space", @"C:\Program Files\B站\app.exe", "带空格和中文"),
            new AliasEntry("dollar", @"C:\weird$dir\app.exe"),
            new AliasEntry("tick", @"C:`tick\app.exe"),
            new AliasEntry("quote", @"C:\file""name.exe"),
            new AliasEntry("single", @"C:\it's\app.exe"),
        };

        var result = _profile.Save(_profilePath, entries);
        Assert.Equal(5, result.EntryCount);

        var lines = ProfileLines();
        var aliasLines = lines.Where(static line => line.StartsWith("Set-Alias", StringComparison.Ordinal)).ToArray();
        Assert.Equal(entries.Length, aliasLines.Length);

        foreach (var line in aliasLines)
        {
            Assert.Matches(@"^Set-Alias -Name \S+ -Value ""(.+)""$", line);
        }

        Assert.Contains("# @alias-meta space 带空格和中文", lines);
        Assert.Contains(@"Set-Alias -Name dollar -Value ""C:\weird`$dir\app.exe""", lines);
        Assert.Contains(@"Set-Alias -Name tick -Value ""C:``tick\app.exe""", lines);
        Assert.Contains(@"Set-Alias -Name quote -Value ""C:\file`""name.exe""", lines);
        Assert.Contains(@"Set-Alias -Name single -Value ""C:\it's\app.exe""", lines);

        var loaded = _profile.Load(_profilePath).Entries;
        Assert.Equal(entries.Select(static e => e.Name).ToArray(), loaded.Select(static e => e.Name).ToArray());
        Assert.Equal(entries.Select(static e => e.Path).ToArray(), loaded.Select(static e => e.Path).ToArray());
        Assert.Equal("带空格和中文", loaded[0].Remark);
    }

    [Fact]
    public void Save_NormalizesMultiLineRemarkIntoSingleMetaLine()
    {
        _profile.Save(_profilePath, [new AliasEntry("x", @"C:\x.exe", "第一行\r\n第二行")]);

        Assert.Equal(
            new[] { "# @alias-meta x 第一行 第二行", @"Set-Alias -Name x -Value ""C:\x.exe""" },
            ProfileLines());
        Assert.Equal("第一行 第二行", Assert.Single(_profile.Load(_profilePath).Entries).Remark);

        _custom.Save(_profilePath, [new CustomCommandEntry("y", "function y { 1 }", true, "第一行\n第二行")]);
        Assert.Equal("第一行 第二行", Assert.Single(_custom.Load(_profilePath).Entries).Remark);
    }

    [Fact]
    public void Save_RemovesRecognizedAliasesAndAppendsSnapshot()
    {
        WriteProfile(Block(
            "# 配置",
            "# @alias-meta dup 第一次",
            @"Set-Alias -Name dup -Value ""C:\first.exe""",
            "Write-Host \"保留\"",
            @"Set-Alias old 'C:\old.exe'"));

        var result = _profile.Save(_profilePath, [new AliasEntry("new", @"C:\new.exe")]);

        Assert.Equal(1, result.EntryCount);
        Assert.Equal(
            new[]
            {
                "# 配置",
                "Write-Host \"保留\"",
                string.Empty,
                "# @alias-meta new",
                @"Set-Alias -Name new -Value ""C:\new.exe""",
            },
            ProfileLines());
    }

    [Fact]
    public void Load_KeepsFirstPositionForDuplicateAliasName_AndUsesLastDefinition()
    {
        WriteProfile(Block(
            @"Set-Alias -Name dup -Value ""C:\first.exe""",
            "Write-Host \"中间\"",
            "# @alias-meta dup 第二次定义",
            @"Set-Alias -Name dup -Value ""C:\second.exe""",
            @"Set-Alias -Name other -Value ""C:\other.exe"""));

        var entries = _profile.Load(_profilePath).Entries;

        Assert.Equal(new[] { "dup", "other" }, entries.Select(static e => e.Name).ToArray());
        Assert.Equal(@"C:\second.exe", entries[0].Path);
        Assert.Equal("第二次定义", entries[0].Remark);
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

    // ---------------- 编码与换行 ----------------

    [Fact]
    public void Save_KeepsUtf8BomAndChineseContent()
    {
        WriteProfile("# 中文注释\r\nWrite-Host \"打开哔哩哔哩\"\r\n", Utf8Bom);
        Assert.True(HasUtf8Bom(ProfileBytes()));

        _profile.Save(_profilePath, [new AliasEntry("blbl", @"C:\app.exe", "打开哔哩哔哩")]);

        Assert.True(HasUtf8Bom(ProfileBytes()));
        Assert.Equal("# 中文注释", ProfileLines()[0]);
        Assert.Contains("Write-Host \"打开哔哩哔哩\"", ProfileLines());
        Assert.Equal("打开哔哩哔哩", Assert.Single(_profile.Load(_profilePath).Entries).Remark);

        _custom.Save(_profilePath, [new CustomCommandEntry("你好", "function 你好 { Write-Host \"hi\" }", true, "中文命令")]);
        Assert.True(HasUtf8Bom(ProfileBytes()));
        Assert.Equal("中文命令", Assert.Single(_custom.Load(_profilePath).Entries).Remark);
    }

    [Fact]
    public void Save_UpgradesBomlessUtf8ToUtf8WithBom()
    {
        // Windows PowerShell 5.1 只有在带 BOM 时才按 UTF-8 读取非 ASCII 内容，AGENT-测试.md 记录了这一升级行为。
        WriteProfile("# 中文注释\r\n", Utf8NoBom);
        Assert.False(HasUtf8Bom(ProfileBytes()));

        _profile.Save(_profilePath, [new AliasEntry("blbl", @"C:\app.exe", "打开哔哩哔哩")]);

        Assert.True(HasUtf8Bom(ProfileBytes()));
        Assert.Equal("# 中文注释", ProfileLines()[0]);
        Assert.Equal("打开哔哩哔哩", Assert.Single(_profile.Load(_profilePath).Entries).Remark);
    }

    [Fact]
    public void Save_KeepsAnsiEncodedFileAsAnsi()
    {
        // 系统开启“使用 Unicode UTF-8 提供全球语言支持”时 ACP 即 65001，ANSI 回退分支不可达。
        if (CultureInfo.CurrentCulture.TextInfo.ANSICodePage == 65001)
        {
            return;
        }

        var sample = AnsiOnlySample();
        var originalLine = "Write-Host '" + sample + "'";
        File.WriteAllText(_profilePath, originalLine + "\r\n", Ansi);

        var before = ProfileBytes();
        Assert.False(HasUtf8Bom(before));
        Assert.False(IsValidUtf8(before));

        _profile.Save(_profilePath, [new AliasEntry("greet", @"C:\app.exe", sample)]);

        var after = ProfileBytes();
        Assert.False(HasUtf8Bom(after));
        Assert.Contains(originalLine, Ansi.GetString(after));
        Assert.Equal(sample, Assert.Single(_profile.Load(_profilePath).Entries).Remark);
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    public void Save_KeepsOriginalNewLineStyle(string newLine)
    {
        var lines = new[]
        {
            "# config",
            "Write-Host 'banner'",
            "Set-Alias old 'C:\\old.exe'",
        };
        File.WriteAllText(_profilePath, string.Join(newLine, lines) + newLine, Utf8NoBom);

        _profile.Save(_profilePath, [new AliasEntry("blbl", @"C:\Program Files\app.exe", "打开")]);
        AssertOnlyExpectedNewLines(ProfileBytes(), newLine);

        _custom.Save(_profilePath, [new CustomCommandEntry("gpr", "function gpr {\n    git pull --rebase\n}")]);
        AssertOnlyExpectedNewLines(ProfileBytes(), newLine);

        var text = File.ReadAllText(_profilePath);
        Assert.Contains(RegionHeader + newLine, text);
        Assert.Contains("function gpr {" + newLine, text);
        Assert.Equal("打开", Assert.Single(_profile.Load(_profilePath).Entries).Remark);
        Assert.Equal("function gpr {\n    git pull --rebase\n}", Assert.Single(_custom.Load(_profilePath).Entries).Code);
    }

    // ---------------- 异常与边界 ----------------

    [Fact]
    public void Save_CreatesMissingProfileFileWithParentDirectories()
    {
        var aliasPath = Path.Combine(_root, "a", "b", "c", "Microsoft.PowerShell_profile.ps1");
        Assert.False(Directory.Exists(Path.GetDirectoryName(aliasPath)!));

        var aliasSave = _profile.Save(aliasPath, [new AliasEntry("blbl", @"C:\app.exe")]);

        Assert.True(aliasSave.FileCreated);
        Assert.True(File.Exists(aliasPath));
        Assert.True(HasUtf8Bom(File.ReadAllBytes(aliasPath)));
        Assert.Equal(
            new[] { "# @alias-meta blbl", @"Set-Alias -Name blbl -Value ""C:\app.exe""" },
            File.ReadAllLines(aliasPath));

        var customPath = Path.Combine(_root, "x", "y", "profile.ps1");
        var customSave = _custom.Save(customPath, [new CustomCommandEntry("hello", "function hello { 1 }")]);

        Assert.True(customSave.FileCreated);
        Assert.Equal(RegionHeader, File.ReadAllLines(customPath)[0]);
        Assert.True(HasUtf8Bom(File.ReadAllBytes(customPath)));
    }

    [Fact]
    public void Load_MissingFileReportsFalseWithoutThrowing()
    {
        var path = Path.Combine(_root, "missing", "profile.ps1");

        var aliases = _profile.Load(path);
        Assert.Equal(path, aliases.ProfilePath);
        Assert.False(aliases.FileExists);
        Assert.Empty(aliases.Entries);

        var commands = _custom.Load(path);
        Assert.Equal(path, commands.ProfilePath);
        Assert.False(commands.FileExists);
        Assert.Empty(commands.Entries);
    }

    [Fact]
    public void Load_EmptyFileReportsExistsWithNoEntries_AndSaveFillsItIn()
    {
        File.WriteAllBytes(_profilePath, Array.Empty<byte>());

        Assert.True(_profile.Load(_profilePath).FileExists);
        Assert.Empty(_profile.Load(_profilePath).Entries);
        Assert.Empty(_custom.Load(_profilePath).Entries);

        _profile.Save(_profilePath, Array.Empty<AliasEntry>());
        Assert.Empty(ProfileLines());

        _custom.Save(_profilePath, [new CustomCommandEntry("hello", "function hello { 1 }")]);
        Assert.Equal(RegionHeader, ProfileLines()[0]);
        Assert.Equal(RegionFooter, ProfileLines()[^1]);
        Assert.True(HasUtf8Bom(ProfileBytes()));
        Assert.Equal("function hello { 1 }", Assert.Single(_custom.Load(_profilePath).Entries).Code);

        _profile.Save(_profilePath, [new AliasEntry("blbl", @"C:\app.exe")]);
        var text = File.ReadAllText(_profilePath);
        Assert.Contains(RegionHeader, text);
        Assert.Contains(@"Set-Alias -Name blbl -Value ""C:\app.exe""", text);
    }

    [Fact]
    public void Load_LeavesUnrecognizedProfileContentUntouched()
    {
        WriteProfile(Block(
            "# 手工维护的别名（没有 @alias-meta，也不会被清理）",
            @"Set-Alias manual 'C:\manual.exe'",
            "function custom { param($x) $x * 2 }",
            "$PSStyle.OutputRendering = 'PlainText'"));

        var manual = Assert.Single(_profile.Load(_profilePath).Entries);
        Assert.Equal("manual", manual.Name);
        Assert.Equal(string.Empty, manual.Remark);

        var linesBefore = ProfileLines();
        _custom.Save(_profilePath, [new CustomCommandEntry("hello", "function hello { 1 }")]);
        var linesAfter = ProfileLines();

        Assert.Equal(linesBefore, linesAfter[..linesBefore.Length]);
        Assert.Equal(RegionFooter, linesAfter[^1]);
    }

    [Fact]
    public void Services_RejectEmptyProfilePath()
    {
        Assert.Throws<ArgumentException>(() => _profile.Load("   "));
        Assert.Throws<ArgumentException>(() => _profile.Save(string.Empty, Array.Empty<AliasEntry>()));
        Assert.Throws<ArgumentException>(() => _custom.Load(null!));
        Assert.Throws<ArgumentException>(() => _custom.Save("\t", Array.Empty<CustomCommandEntry>()));
    }

    [Fact]
    public void Save_And_Load_HandleVeryLongAndSpecialCharacterPaths()
    {
        var longPath = "C:\\" + string.Join("\\", Enumerable.Range(0, 40).Select(static i => $"dir{i:D2} {new string('a', 20)}")) + "\\app.exe";
        var specialPath = @"D:\`$env""quote'中文名.exe";
        var longRemark = string.Concat(Enumerable.Repeat("备注", 200));

        Assert.True(longPath.Length > 1000);

        var result = _profile.Save(_profilePath,
        [
            new AliasEntry("long", longPath, longRemark),
            new AliasEntry("special", specialPath),
        ]);

        Assert.Equal(2, result.EntryCount);

        var text = File.ReadAllText(_profilePath);
        Assert.Contains(longPath, text);
        Assert.Contains(@"Set-Alias -Name special -Value ""D:\```$env`""quote'中文名.exe""", text);

        var entries = _profile.Load(_profilePath).Entries;
        Assert.Equal(longPath, entries[0].Path);
        Assert.Equal(longRemark, entries[0].Remark);
        Assert.Equal(specialPath, entries[1].Path);

        // 自定义命令代码原样写入区块，特殊字符不做转义也不丢失。
        var code = "function special {\n    Write-Host \"" + specialPath + "\"\n    $env:TEST = 1\n}";
        _custom.Save(_profilePath, [new CustomCommandEntry("special", code, false, "含特殊字符")]);

        var loaded = Assert.Single(_custom.Load(_profilePath).Entries);
        Assert.False(loaded.IsEnabled);
        Assert.Equal("含特殊字符", loaded.Remark);
        Assert.Equal(code, loaded.Code);
    }

    [Fact]
    public void CustomSave_UnbalancedRegion_IsRebuiltFromHeaderToEndOfFile()
    {
        // 未闭合的旧区块会一直延伸到文件末尾，保存时应整体重建。
        WriteProfile(Block(
            "Write-Host \"保留\"",
            RegionHeader,
            "# @pam-custom broken",
            "function broken { 1 }",
            "# @pam-custom-end"));

        _custom.Save(_profilePath, [new CustomCommandEntry("hello", "function hello { 1 }")]);

        var lines = ProfileLines();
        Assert.Contains("Write-Host \"保留\"", lines);
        Assert.DoesNotContain("broken", string.Join('\n', lines));
        Assert.Single(lines, IsRegionHeader);
        Assert.Equal(RegionFooter, lines[^1]);
        Assert.Equal(new[] { "hello" }, _custom.Load(_profilePath).Entries.Select(static e => e.Name).ToArray());
    }

    // ---------------- 辅助方法 ----------------

    private void WriteProfile(string content, Encoding? encoding = null)
        => File.WriteAllText(_profilePath, content, encoding ?? Utf8Bom);

    private string[] ProfileLines() => File.ReadAllLines(_profilePath);

    private byte[] ProfileBytes() => File.ReadAllBytes(_profilePath);

    private static string Block(params string[] lines) => string.Join("\r\n", lines) + "\r\n";

    private static bool IsRegionHeader(string line) => line.StartsWith(RegionHeader, StringComparison.OrdinalIgnoreCase);

    private static bool IsRegionFooter(string line) => line.StartsWith(RegionFooter, StringComparison.OrdinalIgnoreCase);

    private static bool HasUtf8Bom(byte[] bytes)
        => bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

    private static bool IsValidUtf8(byte[] bytes)
    {
        try
        {
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>取一个当前 ANSI 代码页能编码、但严格 UTF-8 解码会失败的样本，使断言与测试机区域设置无关。</summary>
    private static string AnsiOnlySample()
    {
        foreach (var candidate in new[] { "中文", "é", "ü", "±", "½", "÷", "Ω", "ß" })
        {
            var bytes = Ansi.GetBytes(candidate);
            if (Ansi.GetString(bytes) != candidate || IsValidUtf8(bytes))
            {
                continue;
            }

            return candidate;
        }

        throw new InvalidOperationException(
            "当前 ANSI 代码页 " + CultureInfo.CurrentCulture.TextInfo.ANSICodePage + " 找不到可验证编码回退的样本字符。");
    }

    /// <summary>校验文件行尾只有原文件那一种风格，既不能混入另一种也不能被改成系统默认换行。</summary>
    private static void AssertOnlyExpectedNewLines(byte[] bytes, string expectedNewLine)
    {
        var text = Encoding.UTF8.GetString(bytes);
        var lf = text.Count(static c => c == '\n');
        var cr = text.Count(static c => c == '\r');
        var crlf = CountOccurrences(text, "\r\n");

        if (expectedNewLine == "\n")
        {
            Assert.Equal(0, cr);
            Assert.True(lf > 0);
        }
        else
        {
            Assert.Equal(lf, crlf);
            Assert.Equal(cr, crlf);
            Assert.True(crlf > 0);
        }
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = text.IndexOf(value, StringComparison.Ordinal);

        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
