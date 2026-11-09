using PAM.Core;
using Xunit;

namespace PAM.Tests;

/// <summary>
/// pam-data.json 的读写、容错回退与数据规范化测试。
/// 所有写入都落在临时目录，不改动真实的用户数据文件。
/// </summary>
public sealed class PamDataServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _dataPath;
    private readonly PamDataService _service;

    public PamDataServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "PAM.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _dataPath = Path.Combine(_root, "pam-data.json");
        _service = new PamDataService(_dataPath);
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

    [Fact]
    public void DefaultConstructor_PointsToAppDataPamDataJson()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PAM",
            "pam-data.json");

        Assert.Equal(expected, new PamDataService().DataFilePath);
    }

    [Fact]
    public void Load_WithMissingFile_ReturnsEmptyData()
    {
        var data = _service.Load();

        Assert.Equal(PamData.CurrentVersion, data.Version);
        Assert.Empty(data.Aliases);
        Assert.Empty(data.CustomCommands);
        Assert.Null(data.ManagedHash);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsAliasesAndCommands()
    {
        var data = new PamData
        {
            ManagedHash = "abc123",
            Aliases =
            [
                new AliasData
                {
                    Name = "blbl",
                    Path = @"C:\Program Files\bilibili\哔哩哔哩.exe",
                    Remark = "打开哔哩哔哩",
                },
                new AliasData { Name = "bb", Path = @"D:\Program Files\Quark\quark.exe" },
            ],
            CustomCommands =
            [
                new CustomCommandData
                {
                    Name = "hello",
                    Remark = "打招呼",
                    IsEnabled = false,
                    CodeLines = ["function hello {", "    Write-Host \"hi\"", "}"],
                },
            ],
        };

        _service.Save(data);
        var loaded = _service.Load();

        Assert.Equal(2, loaded.Aliases.Count);
        Assert.Equal("blbl", loaded.Aliases[0].Name);
        Assert.Equal(@"C:\Program Files\bilibili\哔哩哔哩.exe", loaded.Aliases[0].Path);
        Assert.Equal("打开哔哩哔哩", loaded.Aliases[0].Remark);
        Assert.Equal("bb", loaded.Aliases[1].Name);
        Assert.Equal(string.Empty, loaded.Aliases[1].Remark);

        var command = Assert.Single(loaded.CustomCommands);
        Assert.Equal("hello", command.Name);
        Assert.False(command.IsEnabled);
        Assert.Equal(["function hello {", "    Write-Host \"hi\"", "}"], command.CodeLines);
        Assert.Equal("abc123", loaded.ManagedHash);
    }

    [Fact]
    public void Save_WritesCamelCaseJsonWithoutEscapingCjk()
    {
        _service.Save(new PamData
        {
            Aliases = [new AliasData { Name = "blbl", Path = @"C:\a.exe", Remark = "中文备注" }],
        });

        var json = File.ReadAllText(_dataPath);

        Assert.Contains("\"version\": 1", json);
        Assert.Contains("\"aliases\":", json);
        Assert.Contains("\"customCommands\":", json);
        Assert.Contains("中文备注", json);
        Assert.DoesNotContain("\\u4E2D", json);
        Assert.False(File.Exists(_dataPath + ".tmp"));
    }

    [Fact]
    public void Load_WithCorruptedJson_ReturnsEmptyData()
    {
        File.WriteAllText(_dataPath, "{ not json");

        var data = _service.Load();

        Assert.Empty(data.Aliases);
        Assert.Empty(data.CustomCommands);
    }

    [Fact]
    public void Load_NormalizesBlankNamesAndMissingFields()
    {
        File.WriteAllText(_dataPath, """
            {
              "aliases": [
                { "name": "  ok  ", "path": "C:\\ok.exe" },
                { "name": "   ", "path": "C:\\blank.exe" }
              ],
              "customCommands": [ { "name": "keep" } ]
            }
            """);

        var data = _service.Load();

        var alias = Assert.Single(data.Aliases);
        Assert.Equal("ok", alias.Name);
        Assert.Equal("C:\\ok.exe", alias.Path);
        Assert.Equal(string.Empty, alias.Remark);

        var command = Assert.Single(data.CustomCommands);
        Assert.Equal("keep", command.Name);
        Assert.Empty(command.CodeLines);
        Assert.True(command.IsEnabled);
    }

    [Fact]
    public void Save_OverwritesExistingFile()
    {
        _service.Save(new PamData { Aliases = [new AliasData { Name = "old" }] });
        _service.Save(new PamData { Aliases = [new AliasData { Name = "new" }] });

        Assert.Equal("new", Assert.Single(_service.Load().Aliases).Name);
    }

    [Fact]
    public void Clone_ProducesIndependentCopies()
    {
        var data = new PamData
        {
            Aliases = [new AliasData { Name = "a", Path = @"C:\a.exe" }],
            CustomCommands = [new CustomCommandData { Name = "c", CodeLines = ["line"] }],
        };

        var clone = data.Clone();
        clone.Aliases[0].Name = "changed";
        clone.CustomCommands[0].CodeLines[0] = "changed";
        clone.Aliases.Add(new AliasData { Name = "extra" });

        Assert.Equal("a", data.Aliases[0].Name);
        Assert.Equal("line", data.CustomCommands[0].CodeLines[0]);
        Assert.Single(data.Aliases);
    }

    [Fact]
    public void Constructor_RejectsBlankPath()
        => Assert.Throws<ArgumentException>(() => new PamDataService("  "));

    [Fact]
    public void Save_RejectsNullData()
        => Assert.Throws<ArgumentNullException>(() => _service.Save(null!));
}
