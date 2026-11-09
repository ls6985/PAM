using System.Diagnostics;
using System.Text;

namespace PAM.Core;

public sealed partial class StarshipService
{
    /// <summary>starship.toml 路径：注入路径 → STARSHIP_CONFIG → ~/.config/starship.toml。</summary>
    public string GetConfigPath()
    {
        if (_configPathOverride is not null)
        {
            return _configPathOverride;
        }

        var overridePath = Environment.GetEnvironmentVariable("STARSHIP_CONFIG");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            ConfigFileName);
    }

    public string GetBackupPath() => GetConfigPath() + BackupSuffix;

    /// <summary>当前配置状态：toml 是否存在、有无备份、$PROFILE 里有没有 init 行。</summary>
    public StarshipConfigState GetState(string profilePath)
    {
        var configPath = GetConfigPath();
        return new StarshipConfigState(
            configPath,
            File.Exists(configPath),
            File.Exists(GetBackupPath()),
            profilePath,
            HasInitLine(profilePath));
    }

    /// <summary>写配置前备份现有 starship.toml（单槽覆盖）。文件不存在时不做任何事。</summary>
    public bool BackupConfig()
    {
        var configPath = GetConfigPath();
        if (!File.Exists(configPath))
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.Copy(configPath, GetBackupPath(), overwrite: true);
        return true;
    }

    /// <summary>用备份恢复 starship.toml。没有备份时返回 false。</summary>
    public bool RestoreConfig()
    {
        var backupPath = GetBackupPath();
        if (!File.Exists(backupPath))
        {
            return false;
        }

        var configPath = GetConfigPath();
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.Copy(backupPath, configPath, overwrite: true);
        return true;
    }

    /// <summary>应用内置主题预设：备份 → 写 starship.toml → 确保 $PROFILE 有 init 行。</summary>
    public StarshipApplyResult ApplyPreset(string presetId, string profilePath)
    {
        var configPath = GetConfigPath();
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

        BackupConfig();
        var content = ExtractPreset(presetId)
            ?? throw new InvalidOperationException($"未找到内置主题预设：{presetId}");

        // TOML 规范不建议带 BOM，Starship 直接按 UTF-8 读取。
        ProfileFileStore.Write(configPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var profileUpdated = EnsureProfileInit(profilePath);
        return new StarshipApplyResult(true, profileUpdated.Updated);
    }

    /// <summary>
    /// 识别当前 starship.toml 是否与某个内置预设完全一致（忽略换行与结尾空白差异）。
    /// 应用预设后未手动修改时可识别；存在 PAM 提示符块或已手动修改则返回 null。
    /// </summary>
    public string? DetectAppliedPreset()
    {
        var configPath = GetConfigPath();
        if (!File.Exists(configPath))
        {
            return null;
        }

        var current = Normalize(File.ReadAllText(configPath, Encoding.UTF8));
        foreach (var preset in GetPresets())
        {
            var content = ExtractPreset(preset.Id);
            if (content is not null && Normalize(content) == current)
            {
                return preset.Id;
            }
        }

        return null;

        static string Normalize(string text)
            => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
    }

    /// <summary>打开 starship.toml（不存在则先创建空文件），交给系统默认编辑器。</summary>
    public StarshipOpenResult OpenConfig()
    {
        var configPath = GetConfigPath();
        var created = false;

        if (!File.Exists(configPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            File.WriteAllText(configPath, string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            created = true;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = configPath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(configPath),
            });
        }
        catch
        {
            // 没有 .toml 文件关联时回退到记事本，保证按钮始终能打开配置文件。
            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                ArgumentList = { configPath },
                UseShellExecute = true,
            });
        }

        return new StarshipOpenResult(configPath, created);
    }
}
