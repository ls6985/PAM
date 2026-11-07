using System.IO;
using System.Text;
using PAM.Core;
using Xunit;

namespace PAM.Tests;

/// <summary>
/// 自定义提示符：只替换 starship.toml 的顶层 format（含 """\ 多行字符串），
/// 保留其余段落 / 注释 / 编码 / 换行；一行 format 的往返、备份、恢复、幂等，
/// 以及精选变量表与预览解析。所有文件都在临时目录，不会碰真实的 starship.toml。
/// </summary>
public sealed class StarshipPromptServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _configPath;
    private readonly StarshipPromptService _service;

    public StarshipPromptServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "PAM.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _configPath = Path.Combine(_root, "starship.toml");
        _service = new StarshipPromptService(_configPath);
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

    /// <summary>用户机器上那份手工配置的精简版：顶层多行 format + 各段局部 format + 行内注释。</summary>
    private static readonly string[] ExistingLines =
    [
        "add_newline = false",
        "# 整体提示符格式：用户名@主机名 :路径 提示符",
        "format = \"\"\"\\",
        "$username\\",
        "$hostname\\",
        "$directory\\",
        "$character\\",
        "\"\"\"",
        "",
        "[username]",
        "show_always = true",
        "style_user = \"fg:#BBC4B9\"",
        "format = \"[$user]($style)\"",
        "",
        "[hostname]",
        "ssh_only = false",
        "style = \"fg:#BBC4B9\"",
        "format = \"[@$hostname]($style)\"",
        "",
        "[directory]",
        "style = \"fg:#B6D6F2\"",
        "format = \"[ :$path ]($style)\"",
        "truncation_length = 0",
        "use_os_path_sep = false   # 使用 / 作为分隔符",
        "",
        "[character]",
        "success_symbol = \"[\\\\$](fg:#BFA8BF)\"",
    ];

    private static string ExistingToml(string newLine = "\r\n")
        => string.Join(newLine, ExistingLines) + newLine;

    private void Write(string text, bool bom = false)
        => File.WriteAllText(_configPath, text, bom ? new UTF8Encoding(true) : new UTF8Encoding(false));

    private string Read() => File.ReadAllText(_configPath, Encoding.UTF8);

    private static string Format() => StarshipPromptService.CreateDefaultFormat();

    private static int Count(string text, string marker)
        => text.Split('\n').Count(line => line.TrimEnd('\r') == marker);

    [Fact]
    public void Apply_ReplacesOnlyTopLevelFormat_KeepsEveryOtherLine()
    {
        Write(ExistingToml());

        _service.Apply(Format());
        var text = Read();

        Assert.Equal(1, Count(text, StarshipPromptService.StartMarker));
        Assert.Equal(1, Count(text, StarshipPromptService.EndMarker));
        // 旧的多行顶层 format 被整段移除（段落内的局部 format 仍保留）。
        Assert.DoesNotContain("format = \"\"\"", text);
        Assert.DoesNotContain("$username\\", text);
        Assert.Contains("add_newline = false", text);
        Assert.Contains("# 整体提示符格式：用户名@主机名 :路径 提示符", text);
        Assert.Contains("[username]", text);
        Assert.Contains("style_user = \"fg:#BBC4B9\"", text);
        Assert.Contains("format = \"[$user]($style)\"", text);
        Assert.Contains("format = \"[@$hostname]($style)\"", text);
        Assert.Contains("truncation_length = 0", text);
        Assert.Contains("use_os_path_sep = false   # 使用 / 作为分隔符", text);
        Assert.Contains("success_symbol = \"[\\\\$](fg:#BFA8BF)\"", text);
        Assert.Contains("format = \"[$username]", text); // PAM 生成的单行 format
    }

    [Fact]
    public void Apply_IsIdempotent()
    {
        Write(ExistingToml());
        var format = Format();

        _service.Apply(format);
        var first = Read();
        _service.Apply(format);
        var second = Read();

        Assert.Equal(first, second);
        Assert.Equal(1, Count(second, StarshipPromptService.StartMarker));
        Assert.Equal(1, Count(second, StarshipPromptService.EndMarker));
    }

    [Fact]
    public void Apply_PreservesCrlfAndWritesUtf8WithoutBom()
    {
        Write(ExistingToml("\r\n"));

        _service.Apply(Format());

        var bytes = File.ReadAllBytes(_configPath);
        Assert.NotEqual(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("\r\n", text);
        Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty));
    }

    [Fact]
    public void Apply_PreservesLfNewLineStyle()
    {
        Write(ExistingToml("\n"));

        _service.Apply(Format());

        var text = Encoding.UTF8.GetString(File.ReadAllBytes(_configPath));
        Assert.DoesNotContain("\r\n", text);
    }

    [Fact]
    public void Read_RoundTripsFormat_IncludingSpecialCharacters()
    {
        Write(ExistingToml());
        const string format =
            @"[$username](fg:#61AFEF)[BOX\[1\]](fg:#98C379)[ :](fg:#E5C07B)[\$](fg:#E06C75)[a""b](fg:#FFFFFF)";

        _service.Apply(format);
        var state = _service.Read();

        Assert.True(state.Customized);
        Assert.True(state.ConfigExists);
        Assert.True(state.HasBackup);
        Assert.Equal(format, state.Format);
    }

    [Fact]
    public void Apply_CreatesBackupOnlyOnce_AndRestoreReturnsOriginal()
    {
        var original = ExistingToml();
        Write(original);

        _service.Apply(Format());
        Assert.True(File.Exists(_service.GetBackupPath()));
        Assert.Equal(original, File.ReadAllText(_service.GetBackupPath(), Encoding.UTF8));

        // 第二次应用（改过内容）不能把「用户原始配置」覆盖成上一次结果。
        _service.Apply("[$username](fg:#FFFFFF)");
        Assert.Equal(original, File.ReadAllText(_service.GetBackupPath(), Encoding.UTF8));

        Assert.True(_service.Restore());
        Assert.Equal(original, Read());
        Assert.DoesNotContain(StarshipPromptService.StartMarker, Read());
    }

    [Fact]
    public void Restore_WithoutBackup_ReturnsFalse()
    {
        Write(ExistingToml());
        Assert.False(_service.Restore());
    }

    [Fact]
    public void CreateDefaultFormat_UsesFourVariablesAndReusesExistingColors()
    {
        var format = StarshipPromptService.CreateDefaultFormat(ExistingToml());

        Assert.Equal(
            "[$username](fg:#BBC4B9)[$hostname](fg:#BBC4B9)[$directory](fg:#B6D6F2)[$character](fg:#BFA8BF)",
            format);
    }

    [Fact]
    public void CreateDefaultFormat_WithoutExistingConfig_UsesFallbackPalette()
    {
        Assert.Equal(
            "[$username](fg:#BBC4B9)[$hostname](fg:#BBC4B9)[$directory](fg:#B6D6F2)[$character](fg:#BFA8BF)",
            StarshipPromptService.CreateDefaultFormat());
    }

    [Fact]
    public void Read_WithoutPamBlock_ReportsNotCustomized()
    {
        Write(ExistingToml());

        var state = _service.Read();

        Assert.True(state.ConfigExists);
        Assert.False(state.Customized);
        Assert.False(state.HasBackup);
        Assert.Equal(StarshipPromptService.CreateDefaultFormat(ExistingToml()), state.Format);
    }

    [Fact]
    public void Apply_WithNoExistingFormat_InsertsBlockBeforeFirstSection()
    {
        Write("[git_branch]\r\nsymbol = \" \"\r\n");

        _service.Apply(Format());
        var text = Read();

        Assert.Contains(StarshipPromptService.StartMarker, text);
        Assert.True(text.IndexOf(StarshipPromptService.StartMarker, StringComparison.Ordinal)
                    < text.IndexOf("[git_branch]", StringComparison.Ordinal));
        Assert.Contains("[git_branch]", text);
        Assert.Contains("symbol = \" \"", text);
    }

    [Fact]
    public void Apply_RejectsInvalidFormats()
    {
        Write(ExistingToml());

        Assert.Throws<ArgumentException>(() => _service.Apply(string.Empty));
        Assert.Throws<ArgumentException>(() => _service.Apply("   "));
        Assert.Throws<ArgumentException>(() => _service.Apply("[$username](fg:#BBC4B9)\n[$hostname](fg:#BBC4B9)"));
        Assert.Throws<ArgumentException>(() => _service.Apply("[$username](fg:not-a-color)"));
        Assert.Throws<ArgumentException>(() => _service.Apply(new string('x', 5000)));
    }

    [Fact]
    public void Apply_WhenFileMissing_CreatesConfigWithoutBackup()
    {
        Assert.False(File.Exists(_configPath));

        var result = _service.Apply(Format());

        Assert.True(result.ConfigWritten);
        Assert.False(result.BackupCreated);
        Assert.True(File.Exists(_configPath));
        Assert.False(File.Exists(_service.GetBackupPath()));
        Assert.True(_service.Read().Customized);
    }

    [Fact]
    public void StarshipVariables_ExactlyTwentyChineseNamedDistinctVariables()
    {
        var all = StarshipVariables.All;

        Assert.Equal(20, all.Count);
        Assert.Equal(20, all.Select(item => item.Variable).Distinct(StringComparer.Ordinal).Count());
        Assert.All(all, item =>
        {
            Assert.StartsWith("$", item.Variable);
            Assert.False(string.IsNullOrWhiteSpace(item.NameZh));
            Assert.False(string.IsNullOrWhiteSpace(item.NameEn));
            Assert.False(string.IsNullOrWhiteSpace(item.GroupZh));
            Assert.False(string.IsNullOrWhiteSpace(item.Sample));
        });
        Assert.Equal(4, all.Select(item => item.GroupZh).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(all, item => item.Variable == "$username");
        Assert.Contains(all, item => item.Variable == "$directory");
        Assert.Contains(all, item => item.Variable == "$character");
    }

    [Fact]
    public void ParsePreview_ReplacesVariablesAndExtractsColors()
    {
        var parts = StarshipPromptService.ParsePreview(StarshipPromptService.CreateDefaultFormat());

        Assert.Equal(4, parts.Count);
        Assert.Equal(Environment.UserName, parts[0].Text);
        Assert.Equal("#BBC4B9", parts[0].Color);
        Assert.Equal("@" + Environment.MachineName, parts[1].Text);
        Assert.Equal(" :~ ", parts[2].Text);
        Assert.Equal("#B6D6F2", parts[2].Color);
        Assert.Equal(" $", parts[3].Text);
        Assert.Equal("#BFA8BF", parts[3].Color);
    }

    [Fact]
    public void ParsePreview_KeepsLiteralTextEscapesAndUnknownVariables()
    {
        var parts = StarshipPromptService.ParsePreview(
            "hello [$unknown_var](fg:#FFFFFF)[\\$](fg:#E06C75) tail");

        Assert.Equal(4, parts.Count);
        Assert.Equal("hello ", parts[0].Text);
        Assert.Null(parts[0].Color);
        Assert.Equal("$unknown_var", parts[1].Text); // 清单外的变量保留原样，不假装知道它的值
        Assert.Equal("#FFFFFF", parts[1].Color);
        Assert.Equal("$", parts[2].Text);            // \$ 是字面美元符号
        Assert.Equal("#E06C75", parts[2].Color);
        Assert.Equal(" tail", parts[3].Text);
    }

    [Fact]
    public void ParsePreview_EmptyFormat_ReturnsNoParts()
    {
        Assert.Empty(StarshipPromptService.ParsePreview(null));
        Assert.Empty(StarshipPromptService.ParsePreview(string.Empty));
    }
}
