using System.IO;
using System.Text;
using PAM.Core;
using Xunit;

namespace PAM.Tests;

/// <summary>
/// Windows Terminal settings.json 文本拼接的纯逻辑测试：
/// 注释必须逐字保留、结构逐层补齐、校验失败不落盘、备份可恢复。
/// 全部在临时目录里进行，不触碰真实的 WT 配置。
/// </summary>
public sealed class WindowsTerminalServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _settingsPath;
    private readonly WindowsTerminalService _service = new();

    public WindowsTerminalServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "PAM.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _settingsPath = Path.Combine(_root, "settings.json");
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

    private void WriteSettings(string content) => File.WriteAllText(_settingsPath, content, new UTF8Encoding(false));

    // ---------------- 拼接（直接测内部算法） ----------------

    [Fact]
    public void SpliceFontFace_InsertsFontIntoEmptyDefaults()
    {
        const string original = """{ "profiles": { "defaults": {} } }""";

        var updated = WindowsTerminalService.SpliceFontFace(original, "Cascadia Code NF");

        Assert.Contains("\"face\": \"Cascadia Code NF\"", updated);
        AssertNoTrailingCommaAdded(updated);
    }

    [Fact]
    public void SpliceFontFace_InsertsFontIntoDefaultsWithOtherMembers()
    {
        const string original =
            """
            {
                "profiles": {
                    "defaults": {
                        "colorScheme": "One Half Dark",
                        "useAcrylicInTabRow": true
                    }
                }
            }
            """;

        var updated = WindowsTerminalService.SpliceFontFace(original, "CaskaydiaCove Nerd Font");

        Assert.Contains("\"font\": { \"face\": \"CaskaydiaCove Nerd Font\" }", updated);
        Assert.Contains("\"colorScheme\": \"One Half Dark\"", updated);
        AssertNoTrailingCommaAdded(updated);
    }

    [Fact]
    public void SpliceFontFace_ReplacesExistingFace_AndKeepsCommentsVerbatim()
    {
        const string original =
            """
            // 用户注释：改字体别删这行
            {
                "profiles": {
                    // defaults 里的注释也要原样保留
                    "defaults": {
                        "font": {
                            "face": "Cascadia Mono"
                        }
                    }
                }
            }
            """;

        var updated = WindowsTerminalService.SpliceFontFace(original, "Cascadia Code NF");

        Assert.Contains("\"face\": \"Cascadia Code NF\"", updated);
        Assert.DoesNotContain("Cascadia Mono", updated);
        Assert.Contains("// 用户注释：改字体别删这行", updated);
        Assert.Contains("// defaults 里的注释也要原样保留", updated);
    }

    [Fact]
    public void SpliceFontFace_CreatesFontObjectWhenMissing()
    {
        const string original =
            """
            {
                "profiles": {
                    "defaults": {
                        "font": {
                            "size": 12.0
                        }
                    }
                }
            }
            """;

        var updated = WindowsTerminalService.SpliceFontFace(original, "Hack Nerd Font");

        Assert.Contains("\"face\": \"Hack Nerd Font\"", updated);
        Assert.Contains("\"size\": 12.0", updated);
        AssertNoTrailingCommaAdded(updated);
    }

    [Fact]
    public void SpliceFontFace_CreatesDefaultsWhenMissing()
    {
        const string original =
            """
            {
                "profiles": {
                    "list": [
                        { "name": "PowerShell", "hidden": false }
                    ]
                }
            }
            """;

        var updated = WindowsTerminalService.SpliceFontFace(original, "Cascadia Code NF");

        Assert.Contains("\"defaults\": { \"font\": { \"face\": \"Cascadia Code NF\" } }", updated);
        Assert.Contains("\"name\": \"PowerShell\"", updated);
        AssertNoTrailingCommaAdded(updated);
    }

    [Fact]
    public void SpliceFontFace_CreatesProfilesWhenMissing()
    {
        const string original = """{ "theme": "dark" }""";

        var updated = WindowsTerminalService.SpliceFontFace(original, "Cascadia Code NF");

        Assert.Contains("\"profiles\": { \"defaults\": { \"font\": { \"face\": \"Cascadia Code NF\" } } }", updated);
        Assert.Contains("\"theme\": \"dark\"", updated);
        AssertNoTrailingCommaAdded(updated);
    }

    [Fact]
    public void SpliceFontFace_RejectsLegacyArrayProfiles_WithoutTouchingText()
    {
        const string original = """{ "profiles": [ { "name": "old" } ] }""";

        Assert.Throws<NotSupportedException>(() => WindowsTerminalService.SpliceFontFace(original, "X"));
    }

    [Fact]
    public void SpliceFontFace_ResultAlwaysParsesAsJson()
    {
        const string original =
            """
            {
                // 注释 + 尾逗号都要活下来
                "profiles": {
                    "defaults": {
                        "colorScheme": "Campbell",
                    }
                },
            }
            """;

        var updated = WindowsTerminalService.SpliceFontFace(original, "Cascadia Code NF");

        // 原文自带尾逗号（JSONC 合法），用宽松模式解析并确认 face 已写入。
        System.Text.Json.JsonDocument.Parse(
            updated,
            new System.Text.Json.JsonDocumentOptions
            {
                CommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
        Assert.Contains("\"face\": \"Cascadia Code NF\"", updated);
    }

    // ---------------- 完整落盘流程 ----------------

    [Fact]
    public void ApplyFont_BackupsThenWrites_AndReadFontFaceRoundTrips()
    {
        WriteSettings("""{ "profiles": { "defaults": {} } }""");

        var result = _service.ApplyFont("Cascadia Code NF", _settingsPath);

        Assert.True(result.Success, result.Message);
        Assert.Equal("Cascadia Code NF", _service.ReadFontFace(_settingsPath));
        Assert.Equal("""{ "profiles": { "defaults": {} } }""", File.ReadAllText(_settingsPath + ".bak"));
    }

    [Fact]
    public void ApplyFont_PreservesBom()
    {
        File.WriteAllText(_settingsPath, """{ "profiles": { "defaults": {} } }""", new UTF8Encoding(true));

        var result = _service.ApplyFont("Cascadia Code NF", _settingsPath);

        Assert.True(result.Success, result.Message);
        Assert.True(File.ReadAllBytes(_settingsPath).AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
    }

    [Fact]
    public void ApplyFont_WithComments_KeepsThemOnDisk()
    {
        WriteSettings("// 我的注释\n{ \"profiles\": { \"defaults\": { \"font\": { \"face\": \"Cascadia Mono\" } } } }");

        var result = _service.ApplyFont("Cascadia Code NF", _settingsPath);

        Assert.True(result.Success, result.Message);
        var text = File.ReadAllText(_settingsPath);
        Assert.Contains("// 我的注释", text);
        Assert.Contains("Cascadia Code NF", text);
    }

    [Fact]
    public void ApplyFont_WithEmptyFontName_FailsWithoutWriting()
    {
        WriteSettings("""{ "profiles": { "defaults": {} } }""");
        var before = File.ReadAllText(_settingsPath);

        var result = _service.ApplyFont("", _settingsPath);

        Assert.False(result.Success);
        Assert.Equal(before, File.ReadAllText(_settingsPath));
        Assert.False(File.Exists(_settingsPath + ".bak"));
    }

    [Fact]
    public void ApplyFont_WithMissingFile_FailsGracefully()
    {
        var result = _service.ApplyFont("Cascadia Code NF", Path.Combine(_root, "不存在.json"));

        Assert.False(result.Success);
        Assert.False(File.Exists(_settingsPath));
    }

    [Fact]
    public void RestoreBackup_RecoversPreviousSettings()
    {
        WriteSettings("""{ "profiles": { "defaults": {} } }""");
        _service.ApplyFont("Cascadia Code NF", _settingsPath);
        File.WriteAllText(_settingsPath, "{ 坏掉的内容", new UTF8Encoding(false));

        Assert.True(_service.RestoreBackup(_settingsPath));
        Assert.Equal("""{ "profiles": { "defaults": {} } }""", File.ReadAllText(_settingsPath));
    }

    [Fact]
    public void RestoreBackup_WithoutBackup_ReturnsFalse()
    {
        WriteSettings("""{ "profiles": { "defaults": {} } }""");

        Assert.False(_service.RestoreBackup(_settingsPath));
    }

    [Fact]
    public void GetState_ReportsFontAndBackup()
    {
        WriteSettings("""{ "profiles": { "defaults": { "font": { "face": "Consolas" } } } }""");
        _service.ApplyFont("Cascadia Code NF", _settingsPath);

        var state = _service.GetState(_settingsPath);

        Assert.True(state.SettingsExists);
        Assert.Equal("Cascadia Code NF", state.CurrentFont);
        Assert.True(state.BackupExists);
    }

    // ---------------- 外观属性：通用 JSONC 拼接 ----------------

    [Fact]
    public void SpliceProperty_CreatesMissingProfilesAndDefaults()
    {
        const string original = """{ "theme": "dark" }""";

        var updated = WindowsTerminalService.SpliceProperty(
            original,
            TerminalAppearanceSchema.GetPath(TerminalAppearanceKey.CursorShape),
            TerminalAppearanceChange.Quote("filledBox"));

        Assert.Contains("\"profiles\": { \"defaults\": { \"cursorShape\": \"filledBox\" } }", updated);
        Assert.Contains("\"theme\": \"dark\"", updated);
        AssertNoTrailingCommaAdded(updated);
    }

    [Fact]
    public void SpliceProperty_ReplacesScalar_AndKeepsCommentsVerbatim()
    {
        const string original =
            """
            // 用户注释
            {
                "profiles": {
                    "defaults": {
                        // 透明度后面的说明
                        "opacity": 50,
                        "colorScheme": "Campbell"
                    }
                }
            }
            """;

        var updated = WindowsTerminalService.SpliceProperty(
            original,
            TerminalAppearanceSchema.GetPath(TerminalAppearanceKey.Opacity),
            "85");

        Assert.Contains("\"opacity\": 85", updated);
        Assert.Contains("// 透明度后面的说明", updated);
        Assert.Contains("\"colorScheme\": \"Campbell\"", updated);
        AssertNoTrailingCommaAdded(updated);
    }

    [Fact]
    public void SpliceProperty_DoesNotTouchSameNamedPropertyInNestedObject()
    {
        const string original =
            """
            {
                "profiles": {
                    "defaults": {
                        "unfocusedAppearance": { "cursorShape": "emptyBox" },
                        "cursorShape": "bar"
                    }
                }
            }
            """;

        var updated = WindowsTerminalService.SpliceProperty(
            original,
            TerminalAppearanceSchema.GetPath(TerminalAppearanceKey.CursorShape),
            TerminalAppearanceChange.Quote("vintage"));

        Assert.Contains("\"unfocusedAppearance\": { \"cursorShape\": \"emptyBox\" }", updated);
        Assert.Contains("\"cursorShape\": \"vintage\"", updated);
        AssertNoTrailingCommaAdded(updated);
    }

    [Fact]
    public void SpliceProperty_RemoveLastMember_DoesNotLeaveDanglingComma()
    {
        const string original =
            """
            {
                "profiles": {
                    "defaults": {
                        "colorScheme": "Campbell",
                        "useAcrylic": true
                    }
                }
            }
            """;

        var updated = WindowsTerminalService.SpliceProperty(
            original,
            TerminalAppearanceSchema.GetPath(TerminalAppearanceKey.UseAcrylic),
            null);

        Assert.DoesNotContain("useAcrylic", updated);
        Assert.Contains("\"colorScheme\": \"Campbell\"", updated);
        AssertNoTrailingCommaAdded(updated);
    }

    [Fact]
    public void SpliceProperty_RemoveFirstMember_KeepsOtherMembers()
    {
        const string original =
            """
            {
                "profiles": {
                    "defaults": {
                        "cursorColor": "#FFFFFF",
                        "opacity": 70
                    }
                }
            }
            """;

        var updated = WindowsTerminalService.SpliceProperty(
            original,
            TerminalAppearanceSchema.GetPath(TerminalAppearanceKey.CursorColor),
            null);

        Assert.DoesNotContain("cursorColor", updated);
        Assert.Contains("\"opacity\": 70", updated);
        AssertNoTrailingCommaAdded(updated);
    }

    [Fact]
    public void ApplyAppearance_WritesEveryProperty_AndKeepsCommentsAndOtherKeys()
    {
        WriteSettings(
            """
            // 用户注释：别删
            {
                "profiles": {
                    "defaults": {
                        "colorScheme": "Campbell"
                    }
                }
            }
            """);

        var changes = new[]
        {
            TerminalAppearanceChange.SetString(TerminalAppearanceKey.FontFace, "Cascadia Code NF"),
            TerminalAppearanceChange.SetBool(TerminalAppearanceKey.RetroTerminalEffect, true),
            TerminalAppearanceChange.SetString(TerminalAppearanceKey.CursorShape, "filledBox"),
            TerminalAppearanceChange.SetString(TerminalAppearanceKey.CursorColor, "#FF8800"),
            TerminalAppearanceChange.SetString(TerminalAppearanceKey.BackgroundImage, @"C:\img\a.png"),
            TerminalAppearanceChange.SetNumber(TerminalAppearanceKey.BackgroundImageOpacity, 0.5),
            TerminalAppearanceChange.SetNumber(TerminalAppearanceKey.Opacity, 85),
            TerminalAppearanceChange.SetBool(TerminalAppearanceKey.UseAcrylic, true),
        };

        var result = _service.ApplyAppearance(changes, _settingsPath);

        Assert.True(result.Success, result.Message);
        var appearance = _service.ReadAppearance(_settingsPath);
        Assert.Equal("Cascadia Code NF", appearance.FontFace);
        Assert.True(appearance.RetroTerminalEffect);
        Assert.Equal("filledBox", appearance.CursorShape);
        Assert.Equal("#FF8800", appearance.CursorColor);
        Assert.Equal(@"C:\img\a.png", appearance.BackgroundImage);
        Assert.Equal(0.5, appearance.BackgroundImageOpacity);
        Assert.Equal(85, appearance.Opacity);
        Assert.True(appearance.UseAcrylic);

        var text = File.ReadAllText(_settingsPath);
        Assert.Contains("// 用户注释：别删", text);
        Assert.Contains("\"colorScheme\": \"Campbell\"", text);
    }

    [Fact]
    public void ApplyAppearance_RemoveProperties_AndReadBackAsDefaults()
    {
        WriteSettings(
            """
            {
                "profiles": {
                    "defaults": {
                        "cursorShape": "bar",
                        "backgroundImage": "C:/img.png",
                        "opacity": 70,
                        "useAcrylic": true,
                        "font": { "face": "Consolas" }
                    }
                }
            }
            """);

        var result = _service.ApplyAppearance(
            [
                TerminalAppearanceChange.Remove(TerminalAppearanceKey.CursorShape),
                TerminalAppearanceChange.Remove(TerminalAppearanceKey.BackgroundImage),
                TerminalAppearanceChange.Remove(TerminalAppearanceKey.FontFace),
            ],
            _settingsPath);

        Assert.True(result.Success, result.Message);
        var appearance = _service.ReadAppearance(_settingsPath);
        Assert.Null(appearance.CursorShape);
        Assert.Null(appearance.BackgroundImage);
        Assert.Null(appearance.FontFace);
        Assert.Equal(70, appearance.Opacity);
        Assert.True(appearance.UseAcrylic);
        AssertNoTrailingCommaAdded(File.ReadAllText(_settingsPath));
    }

    [Fact]
    public void ApplyAppearance_WithInvalidValue_FailsWithoutWriting()
    {
        WriteSettings("""{ "profiles": { "defaults": {} } }""");
        var before = File.ReadAllText(_settingsPath);

        var result = _service.ApplyAppearance(
            [TerminalAppearanceChange.SetString(TerminalAppearanceKey.CursorColor, "not-a-color")],
            _settingsPath);

        Assert.False(result.Success);
        Assert.Equal(before, File.ReadAllText(_settingsPath));
    }

    // ---------------- 根级窗口属性 ----------------

    [Fact]
    public void ApplyAppearance_WritesRootWindowFlags_AndReadsThemBack()
    {
        WriteSettings(
            """
            // 用户注释：别删
            {
                "theme": "system",
                "alwaysShowTabs": true,
                "showTabsInTitlebar": true
            }
            """);

        var result = _service.ApplyAppearance(
            [
                TerminalAppearanceChange.SetBool(TerminalAppearanceKey.AutoHideWindow, true),
                TerminalAppearanceChange.SetBool(TerminalAppearanceKey.MinimizeToNotificationArea, true),
                TerminalAppearanceChange.SetBool(TerminalAppearanceKey.AlwaysShowTabs, false),
                TerminalAppearanceChange.SetBool(TerminalAppearanceKey.ShowTabsInTitlebar, false),
            ],
            _settingsPath);

        Assert.True(result.Success, result.Message);
        var flags = _service.ReadGlobalFlags(_settingsPath);
        Assert.True(flags.AutoHideWindow);
        Assert.True(flags.MinimizeToNotificationArea);
        Assert.False(flags.AlwaysShowTabs);
        Assert.False(flags.ShowTabsInTitlebar);

        var text = File.ReadAllText(_settingsPath);
        Assert.Contains("// 用户注释：别删", text);
        Assert.Contains("\"theme\": \"system\"", text);
        AssertNoTrailingCommaAdded(text);
    }

    [Fact]
    public void ApplyAppearance_RootFlags_TurningOffWritesExplicitFalse()
    {
        WriteSettings("""{ "autoHideWindow": true, "minimizeToNotificationArea": true }""");

        var result = _service.ApplyAppearance(
            [
                TerminalAppearanceChange.SetBool(TerminalAppearanceKey.AutoHideWindow, false),
                TerminalAppearanceChange.SetBool(TerminalAppearanceKey.MinimizeToNotificationArea, false),
            ],
            _settingsPath);

        Assert.True(result.Success, result.Message);
        var flags = _service.ReadGlobalFlags(_settingsPath);
        Assert.False(flags.AutoHideWindow);
        Assert.False(flags.MinimizeToNotificationArea);
    }

    [Fact]
    public void ReadGlobalFlags_WithMissingFile_ReturnsAllNull()
    {
        var flags = _service.ReadGlobalFlags(Path.Combine(_root, "absent.json"));

        Assert.Equal(WindowsTerminalGlobalFlags.Empty, flags);
    }

    // ---------------- 备份：安装本软件前的原始配置 ----------------

    [Fact]
    public void MultipleWrites_NeverOverwriteOriginalBackup_RestoreReturnsPreInstallConfig()
    {
        const string original = """{ "profiles": { "defaults": {} } }""";
        WriteSettings(original);

        Assert.True(_service.ApplyFont("Font A", _settingsPath).Success);
        Assert.True(_service.ApplyFont("Font B", _settingsPath).Success);
        Assert.True(_service.ApplyAppearance(
            [TerminalAppearanceChange.SetNumber(TerminalAppearanceKey.Opacity, 70)],
            _settingsPath).Success);

        Assert.True(_service.RestoreBackup(_settingsPath));
        Assert.Equal(original, File.ReadAllText(_settingsPath));
    }

    [Fact]
    public void RestoreBackup_KeepsBackup_SoItCanBeRestoredAgainAfterMoreEdits()
    {
        const string original = """{ "profiles": { "defaults": {} } }""";
        WriteSettings(original);

        _service.ApplyFont("Font A", _settingsPath);
        Assert.True(_service.RestoreBackup(_settingsPath));
        Assert.True(File.Exists(_settingsPath + ".bak"), "恢复不应消费掉原始备份");

        _service.ApplyFont("Font C", _settingsPath);
        Assert.True(_service.RestoreBackup(_settingsPath));
        Assert.Equal(original, File.ReadAllText(_settingsPath));
    }

    [Fact]
    public void GetState_ReportsOriginalBackupTime()
    {
        WriteSettings("""{ "profiles": { "defaults": {} } }""");
        _service.ApplyFont("Cascadia Code NF", _settingsPath);

        var state = _service.GetState(_settingsPath);

        Assert.True(state.BackupExists);
        Assert.Equal(_settingsPath + ".bak", state.BackupPath);
        Assert.NotNull(state.BackupCreatedAt);
    }

    private static void AssertNoTrailingCommaAdded(string json)
    {
        // 用禁止尾逗号的解析验证插入逻辑没有引入非法 JSON。
        // （原始内容若自带尾逗号则不适用此断言，那属于 JSONC 的合法范畴。）
        System.Text.Json.JsonDocument.Parse(
            json,
            new System.Text.Json.JsonDocumentOptions
            {
                CommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                AllowTrailingCommas = false,
            });
    }
}
