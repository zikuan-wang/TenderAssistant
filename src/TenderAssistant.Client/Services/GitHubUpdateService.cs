using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TenderAssistant.Client.Services;

public sealed class GitHubUpdateService
{
    public const string LatestReleasePageUrl = "https://github.com/zikuan-wang/TenderAssistant/releases/latest";

    private static readonly Uri LatestReleaseApiUri = new("https://api.github.com/repos/zikuan-wang/TenderAssistant/releases/latest");
    private static readonly Regex VersionRegex = new(@"\d+(?:\.\d+){0,3}", RegexOptions.Compiled);
    private static readonly HttpClient Client = CreateHttpClient();

    public Version CurrentVersion { get; } = AppVersionService.CurrentVersion;

    public async Task<UpdateCheckResult> CheckLatestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Client.GetAsync(LatestReleaseApiUri, cancellationToken);
            if (response.StatusCode is HttpStatusCode.NotFound)
            {
                return UpdateCheckResult.Failed(
                    "无法访问 GitHub Release。当前仓库是私有仓库或尚未创建 Release，客户端没有 GitHub 授权时无法在线检查更新。",
                    CurrentVersion);
            }

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var release = await System.Text.Json.JsonSerializer.DeserializeAsync<GitHubRelease>(stream, cancellationToken: cancellationToken);
            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return UpdateCheckResult.Failed("GitHub Release 返回内容为空或格式无效。", CurrentVersion);
            }

            if (!TryParseVersion(release.TagName, out var latestVersion))
            {
                return UpdateCheckResult.Failed($"无法解析最新版本号：{release.TagName}", CurrentVersion);
            }

            var installer = release.Assets.FirstOrDefault(static asset =>
                asset.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)
                && asset.Name.Contains("Client", StringComparison.OrdinalIgnoreCase));
            installer ??= release.Assets.FirstOrDefault(static asset => asset.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));

            return latestVersion > Normalize(CurrentVersion)
                ? UpdateCheckResult.Available(CurrentVersion, latestVersion, release.HtmlUrl, installer?.BrowserDownloadUrl, installer?.Name)
                : UpdateCheckResult.NoUpdate(CurrentVersion, latestVersion, release.HtmlUrl);
        }
        catch (HttpRequestException ex)
        {
            return UpdateCheckResult.Failed($"检查更新失败：{ex.Message}", CurrentVersion);
        }
        catch (TaskCanceledException)
        {
            return UpdateCheckResult.Failed("检查更新超时或已取消。", CurrentVersion);
        }
        catch (Exception ex)
        {
            return UpdateCheckResult.Failed($"检查更新失败：{ex.Message}", CurrentVersion);
        }
    }

    public async Task<string> DownloadInstallerAsync(UpdateCheckResult update, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(update.AssetDownloadUrl))
        {
            throw new InvalidOperationException("当前 Release 没有可下载的 MSI 安装包。");
        }

        var updateDirectory = Path.Combine(Path.GetTempPath(), "TenderAssistant", "Updates");
        Directory.CreateDirectory(updateDirectory);
        var fileName = string.IsNullOrWhiteSpace(update.AssetName)
            ? $"TenderAssistant.Client.Setup-{AppVersionService.Format(update.LatestVersion)}.msi"
            : update.AssetName;
        var targetPath = Path.Combine(updateDirectory, fileName);

        using var response = await Client.GetAsync(update.AssetDownloadUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = File.Create(targetPath);
        await source.CopyToAsync(target, cancellationToken);
        return targetPath;
    }

    public static void OpenReleasePage(string releaseUrl)
    {
        Process.Start(new ProcessStartInfo(string.IsNullOrWhiteSpace(releaseUrl) ? LatestReleasePageUrl : releaseUrl)
        {
            UseShellExecute = true
        });
    }

    public static void LaunchInstaller(string installerPath)
    {
        Process.Start(new ProcessStartInfo("msiexec.exe", $"/i \"{installerPath}\"")
        {
            UseShellExecute = true
        });
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TenderAssistant.Client");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private static bool TryParseVersion(string value, out Version version)
    {
        var match = VersionRegex.Match(value);
        if (match.Success && Version.TryParse(match.Value, out var parsed))
        {
            version = Normalize(parsed);
            return true;
        }

        version = new Version(0, 0, 0);
        return false;
    }

    private static Version Normalize(Version version)
    {
        return new Version(
            Math.Max(0, version.Major),
            Math.Max(0, version.Minor),
            Math.Max(0, version.Build),
            Math.Max(0, version.Revision));
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("assets")] IReadOnlyList<GitHubReleaseAsset> Assets);

    private sealed record GitHubReleaseAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}

public sealed record UpdateCheckResult(
    bool IsSuccess,
    bool HasUpdate,
    Version CurrentVersion,
    Version? LatestVersion,
    string Message,
    string? ReleaseUrl,
    string? AssetDownloadUrl,
    string? AssetName)
{
    public static UpdateCheckResult Available(Version currentVersion, Version latestVersion, string releaseUrl, string? assetDownloadUrl, string? assetName)
    {
        var assetMessage = string.IsNullOrWhiteSpace(assetDownloadUrl)
            ? "，但该 Release 未上传 MSI 安装包"
            : string.Empty;
        return new UpdateCheckResult(true, true, currentVersion, latestVersion, $"发现新版本 {AppVersionService.Format(latestVersion)}{assetMessage}。", releaseUrl, assetDownloadUrl, assetName);
    }

    public static UpdateCheckResult NoUpdate(Version currentVersion, Version latestVersion, string releaseUrl)
    {
        return new UpdateCheckResult(true, false, currentVersion, latestVersion, $"当前已是最新版本 {AppVersionService.Format(currentVersion)}。", releaseUrl, null, null);
    }

    public static UpdateCheckResult Failed(string message, Version currentVersion)
    {
        return new UpdateCheckResult(false, false, currentVersion, null, message, GitHubUpdateService.LatestReleasePageUrl, null, null);
    }
}
