namespace PAM.Core;

public sealed partial class StarshipService
{
    /// <summary>
    /// 用 Scoop 安装 Starship。要求 Scoop 已安装；被一键安装流程在最末尾调用。
    /// </summary>
    public StarshipInstallResult InstallStarship(Action<string>? progress = null)
    {
        if (!IsScoopInstalled())
        {
            return new StarshipInstallResult(false, "未检测到 Scoop，请先安装 Scoop。");
        }

        var scoop = Path.Combine(ScoopShimDirectory, "scoop.cmd");
        if (!File.Exists(scoop))
        {
            scoop = "scoop.cmd";
        }

        var result = RunProcess(BuildScoopInstall(scoop), TimeSpan.FromMinutes(15), progress);
        if (result.ExitCode == 0)
        {
            return new StarshipInstallResult(true, "Starship 安装完成。");
        }

        return new StarshipInstallResult(false, $"scoop install starship 失败（退出码 {result.ExitCode}）。{BuildErrorTail(result.StandardError)}");
    }

    /// <summary>
    /// 一键安装：Scoop 不存在时先执行官方引导脚本（需要联网），再安装 Starship。
    /// </summary>
    public StarshipInstallResult InstallScoopAndStarship(Action<string>? progress = null)
    {
        if (!IsScoopInstalled())
        {
            progress?.Invoke("正在安装 Scoop…");
            var bootstrap = RunProcess(
                BuildScoopBootstrap(),
                TimeSpan.FromMinutes(15),
                progress);

            if (bootstrap.ExitCode != 0 || !IsScoopInstalled())
            {
                return new StarshipInstallResult(
                    false,
                    $"Scoop 安装失败（退出码 {bootstrap.ExitCode}）。{BuildErrorTail(bootstrap.StandardError)}");
            }

            progress?.Invoke("Scoop 安装完成。");
        }

        return InstallStarship(progress);
    }
}
