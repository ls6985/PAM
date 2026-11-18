using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PAM.App;

internal enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    NoRelease,
    Failed
}

/// <summary>一次检查的结果；LatestVersion 只在能解析出发布标记时非空。</summary>
internal sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    Version? LatestVersion = null,
    string? Error = null);

/// <summary>
/// 读取 GitHub 上的最新发布标记并与当前程序集版本比较。只发只读请求，不下载、不安装、不写配置。
/// 优先用 Releases API；接口不可达或触发限流时改用 releases/latest 的跳转地址兜底。
/// </summary>
internal static class UpdateCheckService
{
    private static readonly HttpClient Client = CreateClient();

    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var primary = await CheckReleaseApiAsync(cancellationToken).ConfigureAwait(false);
        if (primary.Status != UpdateCheckStatus.Failed) return primary;

        var fallback = await CheckLatestRedirectAsync(cancellationToken).ConfigureAwait(false);
        return fallback.Status == UpdateCheckStatus.Failed ? primary : fallback;
    }

    /// <summary>把版本格式化为界面上的 v主.次.修订 形式，第四段为 0 时省略。</summary>
    public static string Format(Version? version)
    {
        if (version is null) return string.Empty;
        return version.Revision > 0
            ? $"v{version.Major}.{version.Minor}.{version.Build}.{version.Revision}"
            : $"v{version.Major}.{version.Minor}.{version.Build}";
    }

    private static async Task<UpdateCheckResult> CheckReleaseApiAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Client.GetAsync(GitHubLinks.LatestReleaseApi, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new UpdateCheckResult(UpdateCheckStatus.NoRelease);
            if (!response.IsSuccessStatusCode)
                return new UpdateCheckResult(UpdateCheckStatus.Failed, Error: $"HTTP {(int)response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var latest = ParseTag(ReadString(root, "tag_name"));
            if (latest is null)
                return new UpdateCheckResult(UpdateCheckStatus.Failed, Error: "发布信息里没有可识别的版本号");

            return new UpdateCheckResult(Compare(latest), latest);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new UpdateCheckResult(UpdateCheckStatus.Failed, Error: "请求已取消");
        }
        catch (Exception exception)
        {
            return new UpdateCheckResult(UpdateCheckStatus.Failed, Error: Describe(exception));
        }
    }

    /// <summary>不依赖 API：GitHub 会把 releases/latest 跳转到 releases/tag/版本号。</summary>
    private static async Task<UpdateCheckResult> CheckLatestRedirectAsync(CancellationToken cancellationToken)
    {
        const string marker = "/releases/tag/";

        try
        {
            using var response = await Client.GetAsync(GitHubLinks.LatestReleaseRedirect, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new UpdateCheckResult(UpdateCheckStatus.NoRelease);
            if (!response.IsSuccessStatusCode)
                return new UpdateCheckResult(UpdateCheckStatus.Failed, Error: $"HTTP {(int)response.StatusCode}");

            var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? string.Empty;
            var finalPath = response.RequestMessage?.RequestUri?.AbsolutePath ?? string.Empty;
            var index = finalUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                // 没有跳到具体标签，而是停在发布列表页：仓库还没有发布版本。
                if (finalPath.TrimEnd('/').EndsWith("/releases", StringComparison.OrdinalIgnoreCase))
                    return new UpdateCheckResult(UpdateCheckStatus.NoRelease);

                return new UpdateCheckResult(UpdateCheckStatus.Failed, Error: "未能解析最新版本");
            }

            var tag = Uri.UnescapeDataString(finalUrl[(index + marker.Length)..].TrimEnd('/'));
            var latest = ParseTag(tag);
            if (latest is null)
                return new UpdateCheckResult(UpdateCheckStatus.Failed, Error: "发布信息里没有可识别的版本号");

            return new UpdateCheckResult(Compare(latest), latest);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new UpdateCheckResult(UpdateCheckStatus.Failed, Error: "请求已取消");
        }
        catch (Exception exception)
        {
            return new UpdateCheckResult(UpdateCheckStatus.Failed, Error: Describe(exception));
        }
    }

    private static UpdateCheckStatus Compare(Version latest) =>
        latest > Normalize(CurrentVersion)
            ? UpdateCheckStatus.UpdateAvailable
            : UpdateCheckStatus.UpToDate;

    /// <summary>程序集版本是四段，补全缺省段后再比较，避免 1.0.0 与 1.0.0.0 误判。</summary>
    private static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));

    private static Version? ParseTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;

        var text = tag.Trim().TrimStart('v', 'V');
        var suffix = text.IndexOfAny(new[] { '-', '+' });
        if (suffix >= 0) text = text[..suffix];

        var parts = text.Split('.');
        var numbers = new int[4];
        for (var i = 0; i < Math.Min(parts.Length, numbers.Length); i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
                return null;
        }

        return new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string Describe(Exception exception) => exception switch
    {
        HttpRequestException => "无法连接 GitHub",
        TaskCanceledException => "请求超时",
        JsonException => "返回数据格式异常",
        _ => "检查过程出错"
    };

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PAM-Updater/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
