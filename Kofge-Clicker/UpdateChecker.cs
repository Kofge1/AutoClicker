using System.Net.Http.Headers;
using System.Text.Json;

namespace KofgeClicker;

internal sealed record UpdateInfo(string TagName, string ReleaseUrl);

internal static class UpdateChecker
{
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/Kofge1/Kofge-Clicker/releases/latest";
    private const string FallbackReleaseUrl = "https://github.com/Kofge1/Kofge-Clicker/releases/latest";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    internal static async Task<UpdateInfo?> CheckForUpdateAsync(string currentVersion, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient { Timeout = Timeout };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Kofge-Clicker", NormalizeVersion(currentVersion)));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await client.GetAsync(LatestReleaseApiUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;
            var latestTag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(latestTag) || !IsNewerVersion(latestTag, currentVersion))
            {
                return null;
            }

            var releaseUrl = root.TryGetProperty("html_url", out var urlElement) ? urlElement.GetString() : null;
            return new UpdateInfo(latestTag.Trim(), string.IsNullOrWhiteSpace(releaseUrl) ? FallbackReleaseUrl : releaseUrl.Trim());
        }
        catch
        {
            return null;
        }
    }

    private static bool IsNewerVersion(string latestVersion, string currentVersion)
    {
        if (!Version.TryParse(NormalizeVersion(latestVersion), out var latest)
            || !Version.TryParse(NormalizeVersion(currentVersion), out var current))
        {
            return false;
        }

        return latest > current;
    }

    private static string NormalizeVersion(string version)
    {
        var normalized = version.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        return normalized;
    }
}
