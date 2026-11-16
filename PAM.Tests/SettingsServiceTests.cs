using System.IO;
using System.Text;
using System.Text.Json;
using PAM.Core;
using Xunit;

namespace PAM.Tests;

/// <summary>
/// settings.json 读写与容错回退的纯逻辑测试。
/// 所有写入都落在临时目录，测试不会改动真实的 <c>%AppData%\PAM\settings.json</c>。
/// </summary>
public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _settingsPath;
    private readonly SettingsService _service;

    public SettingsServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "PAM.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _settingsPath = Path.Combine(_root, "settings.json");
        _service = new SettingsService(_settingsPath);
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

    // ---------------- 读写逻辑 ----------------

    [Fact]
    public void DefaultConstructor_PointsToAppDataPamSettingsJson()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PAM",
            "settings.json");

        // 只校验路径，不写入真实配置文件。
        Assert.Equal(expected, new SettingsService().SettingsFilePath);
        Assert.True(Path.IsPathRooted(expected));
    }

    [Fact]
    public void Save_WritesCamelCaseIndentedJson()
    {
        _service.Save(new AppSettings
        {
            Hotkey = "Ctrl+Alt+K",
            TerminalType = AppSettings.TerminalTypePowerShell,
            AutoLaunchTerminal = false,
            Theme = AppSettings.ThemeDark,
            Language = AppSettings.LanguageEnUS,
            KeepTrayOnClose = false,
            StartTrayWithWindows = false,
            SwitchImeToEnglish = false,
            AliasLaunchEnhanced = false,
            ShowAliasRemarkOnLaunch = false,
            FriendlyCommandNotFound = false,
        });

        Assert.True(File.Exists(_settingsPath));

        var json = File.ReadAllText(_settingsPath);
        using var document = JsonDocument.Parse(json);

        var names = document.RootElement.EnumerateObject().Select(static property => property.Name).ToArray();
        Assert.Equal(
            new[] { "hotkey", "terminalType", "autoLaunchTerminal", "theme", "language", "keepTrayOnClose", "startTrayWithWindows", "switchImeToEnglish", "aliasLaunchEnhanced", "showAliasRemarkOnLaunch", "friendlyCommandNotFound", "warmPoolEnabled" }
                .OrderBy(static name => name, StringComparer.Ordinal).ToArray(),
            names.OrderBy(static name => name, StringComparer.Ordinal).ToArray());

        // 缩进写盘，且 [JsonIgnore] 的计算属性不落盘。
        Assert.Contains(Environment.NewLine + "  \"hotkey\"", json);
        Assert.DoesNotContain("UsesPowerShell7", json);
        Assert.Equal("Ctrl+Alt+K", document.RootElement.GetProperty("hotkey").GetString());
        Assert.Equal("powershell", document.RootElement.GetProperty("terminalType").GetString());
        Assert.False(document.RootElement.GetProperty("autoLaunchTerminal").GetBoolean());
        Assert.Equal("Dark", document.RootElement.GetProperty("theme").GetString());
        Assert.Equal("en-US", document.RootElement.GetProperty("language").GetString());
        Assert.False(document.RootElement.GetProperty("keepTrayOnClose").GetBoolean());
        Assert.False(document.RootElement.GetProperty("startTrayWithWindows").GetBoolean());
        Assert.False(document.RootElement.GetProperty("switchImeToEnglish").GetBoolean());
        Assert.False(document.RootElement.GetProperty("aliasLaunchEnhanced").GetBoolean());
        Assert.False(document.RootElement.GetProperty("showAliasRemarkOnLaunch").GetBoolean());
        Assert.False(document.RootElement.GetProperty("friendlyCommandNotFound").GetBoolean());
    }

    [Fact]
    public void Save_KeepsHotkeyReadableInsteadOfUnicodeEscaping()
    {
        _service.Save(new AppSettings());

        var json = File.ReadAllText(_settingsPath);
        Assert.Contains("\"Ctrl+Alt+T\"", json);
        Assert.DoesNotContain("\\u002B", json);
    }

    [Fact]
    public void Load_AfterSave_MapsAllProperties()
    {
        var saved = new AppSettings
        {
            Hotkey = "Ctrl+Shift+F12",
            TerminalType = AppSettings.TerminalTypeAuto,
            AutoLaunchTerminal = false,
            Theme = AppSettings.ThemeLight,
            Language = AppSettings.LanguageEnUS,
            KeepTrayOnClose = false,
            StartTrayWithWindows = false,
            SwitchImeToEnglish = false,
            AliasLaunchEnhanced = false,
            ShowAliasRemarkOnLaunch = false,
            FriendlyCommandNotFound = false,
        };

        _service.Save(saved);
        var loaded = _service.Load();

        Assert.Equal(saved.Hotkey, loaded.Hotkey);
        Assert.Equal(saved.TerminalType, loaded.TerminalType);
        Assert.Equal(saved.AutoLaunchTerminal, loaded.AutoLaunchTerminal);
        Assert.Equal(saved.Theme, loaded.Theme);
        Assert.Equal(saved.Language, loaded.Language);
        Assert.Equal(saved.KeepTrayOnClose, loaded.KeepTrayOnClose);
        Assert.Equal(saved.StartTrayWithWindows, loaded.StartTrayWithWindows);
        Assert.Equal(saved.SwitchImeToEnglish, loaded.SwitchImeToEnglish);
        Assert.Equal(saved.AliasLaunchEnhanced, loaded.AliasLaunchEnhanced);
        Assert.Equal(saved.ShowAliasRemarkOnLaunch, loaded.ShowAliasRemarkOnLaunch);
        Assert.Equal(saved.FriendlyCommandNotFound, loaded.FriendlyCommandNotFound);

        _service.Save(new AppSettings { TerminalType = AppSettings.TerminalTypePwsh });
        Assert.True(_service.Load().UsesPowerShell7);
    }

    [Fact]
    public void Load_AcceptsPascalCaseAndPartialJson()
    {
        File.WriteAllText(_settingsPath, """{"Hotkey":"Ctrl+Alt+J","THEME":"Dark"}""");

        var loaded = _service.Load();

        Assert.Equal("Ctrl+Alt+J", loaded.Hotkey);
        Assert.Equal(AppSettings.ThemeDark, loaded.Theme);
        Assert.Equal(AppSettings.LanguageZhCN, loaded.Language);
        Assert.True(loaded.AutoLaunchTerminal);
        Assert.True(loaded.KeepTrayOnClose);
        Assert.True(loaded.StartTrayWithWindows);
        Assert.True(loaded.SwitchImeToEnglish);
        Assert.True(loaded.AliasLaunchEnhanced);
        Assert.True(loaded.ShowAliasRemarkOnLaunch);
        Assert.True(loaded.FriendlyCommandNotFound);
    }

    [Fact]
    public void Save_OverwritesExistingFileWithoutLeavingTempFile()
    {
        _service.Save(new AppSettings { Theme = AppSettings.ThemeLight });
        _service.Save(new AppSettings { Theme = AppSettings.ThemeDark });

        Assert.Equal(
            new[] { "settings.json" },
            Directory.GetFiles(_root).Select(static file => Path.GetFileName(file)).ToArray());

        Assert.Equal(AppSettings.ThemeDark, _service.Load().Theme);
    }

    [Fact]
    public void Save_CreatesMissingParentDirectory()
    {
        var path = Path.Combine(_root, "nested", "deeper", "settings.json");
        var service = new SettingsService(path);

        service.Save(new AppSettings { Hotkey = "Ctrl+Alt+P" });

        Assert.True(File.Exists(path));
        Assert.Equal("Ctrl+Alt+P", service.Load().Hotkey);
    }

    [Fact]
    public void GetLastWriteTimeUtc_TracksSavedFile()
    {
        Assert.Equal(DateTime.MinValue, _service.GetLastWriteTimeUtc());

        _service.Save(new AppSettings());

        var written = _service.GetLastWriteTimeUtc();
        Assert.True(written > DateTime.MinValue);
        Assert.True(written >= DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public void Constructor_RejectsEmptySettingsPath()
    {
        Assert.Throws<ArgumentException>(() => new SettingsService("  "));
        Assert.Throws<ArgumentException>(() => new SettingsService(null!));
        Assert.Throws<ArgumentNullException>(() => _service.Save(null!));
    }

    // ---------------- 容错回退 ----------------

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var loaded = new SettingsService(Path.Combine(_root, "absent", "settings.json")).Load();

        AssertDefaults(loaded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \r\n  ")]
    [InlineData("null")]
    [InlineData("{ not valid json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{\"hotkey\": 123, \"theme\": {\"nested\": true}}")]
    [InlineData("{\"language\": \"fr-FR\", \"hotkey\": \"\", \"terminalType\": 7}")]
    public void Load_CorruptedContent_ReturnsDefaultsWithoutThrowing(string content)
    {
        File.WriteAllText(_settingsPath, content, Encoding.UTF8);

        var loaded = _service.Load();

        AssertDefaults(loaded);
    }

    [Fact]
    public void Load_BinaryGarbageAndTruncatedJson_ReturnsDefaults()
    {
        // 写到一半被截断的 JSON：Save 用临时文件原子替换正是为了避免这种情况。
        var json = JsonSerializer.Serialize(new AppSettings { Hotkey = "Ctrl+Alt+M" });
        File.WriteAllText(_settingsPath, json[..(json.Length / 2)]);
        AssertDefaults(_service.Load());

        var random = new byte[512];
        Random.Shared.NextBytes(random);
        File.WriteAllBytes(_settingsPath, random);
        AssertDefaults(_service.Load());
    }

    [Fact]
    public void Load_NormalizesManuallyBrokenValues()
    {
        File.WriteAllText(
            _settingsPath,
            """{"hotkey":"NotAHotkey","theme":"Neon","language":"fr-FR","terminalType":"cmd"}""");

        AssertDefaults(_service.Load());
    }

    [Fact]
    public void Load_NormalizesHotkeyDisplayButKeepsKnownEnumCasing()
    {
        File.WriteAllText(
            _settingsPath,
            """{"hotkey":"control+ alt +k","terminalType":"PWSh","theme":"dark"}""");

        var loaded = _service.Load();

        Assert.Equal("Ctrl+Alt+K", loaded.Hotkey);
        Assert.Equal("PWSh", loaded.TerminalType);
        Assert.True(loaded.UsesPowerShell7);
        Assert.Equal("dark", loaded.Theme);
    }

    [Fact]
    public void Save_NormalizesBeforeWriting()
    {
        _service.Save(new AppSettings { Hotkey = "Ctrl+Alt+Q", Theme = "Neon", Language = "fr-FR" });

        var loaded = _service.Load();

        Assert.Equal("Ctrl+Alt+Q", loaded.Hotkey);
        Assert.Equal(AppSettings.ThemeSystem, loaded.Theme);
        Assert.Equal(AppSettings.LanguageZhCN, loaded.Language);
    }

    private static void AssertDefaults(AppSettings settings)
    {
        Assert.Equal(AppSettings.DefaultHotkey, settings.Hotkey);
        Assert.Equal(AppSettings.TerminalTypePwsh, settings.TerminalType);
        Assert.Equal(AppSettings.ThemeSystem, settings.Theme);
        Assert.Equal(AppSettings.LanguageZhCN, settings.Language);
        Assert.True(settings.AutoLaunchTerminal);
        Assert.True(settings.KeepTrayOnClose);
        Assert.True(settings.StartTrayWithWindows);
        Assert.True(settings.SwitchImeToEnglish);
        Assert.True(settings.AliasLaunchEnhanced);
        Assert.True(settings.ShowAliasRemarkOnLaunch);
    }
}
