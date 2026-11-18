using System.Diagnostics;

namespace PAM.App;

/// <summary>
/// 仓库在 GitHub 上的地址集中维护，关于页入口、提交反馈与更新检测共用。
/// Repository 是克隆地址，浏览器打开时由 GitHub 跳转到仓库首页。
/// </summary>
internal static class GitHubLinks
{
    public const string Repository = "https://github.com/ls6985/PAM.git";
    public const string IssuesNew = "https://github.com/ls6985/PAM/issues/new";
    public const string License = "https://github.com/ls6985/PAM/blob/main/LICENSE";
    public const string LatestReleaseApi = "https://api.github.com/repos/ls6985/PAM/releases/latest";
    public const string LatestReleaseRedirect = "https://github.com/ls6985/PAM/releases/latest";

    /// <summary>用系统默认浏览器打开地址；失败时返回 false，由调用方给出可复制的地址。</summary>
    public static bool TryOpen(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
