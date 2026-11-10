namespace PAM.Core;

/// <summary>
/// 各 $PROFILE 托管块共用的 PowerShell 片段与标记常量，避免重复维护。
/// 这里只放“在所有托管块里逐字相同的文本”，差异化的变量名由调用方传入。
/// </summary>
internal static class ProfileScriptSnippets
{
    /// <summary>写入方在托管块顶部标注的提示：区块由工具自动维护，用户勿手改标记行。</summary>
    public const string ManagedSectionNotice = "# 该区块由 PowerShell 管理器自动维护，请勿手动修改标记行。";

    /// <summary>
    /// 读取 settings.json 的 PowerShell 片段：把配置对象赋给 <paramref name="configVar"/>，
    /// 路径变量名为 <paramref name="pathVar"/>。别名启动与未知命令提示两个托管块共用这段样板，
    /// 缩进（8 / 8 / 12 空格）与现有生成结果保持一致。
    /// </summary>
    public static string[] ReadSettingsJsonLines(string pathVar, string configVar)
    {
        return
        [
            $"        ${pathVar} = Join-Path $env:APPDATA 'PAM\\settings.json'",
            $"        if (Test-Path -LiteralPath ${pathVar}) {{",
            $"            ${configVar} = Get-Content -LiteralPath ${pathVar} -Raw | ConvertFrom-Json",
        ];
    }
}
