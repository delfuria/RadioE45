using System.Net.Http.Headers;
using System.Text.Json;
using RadioE45.Models;
using RadioE45.Services.Platform;

namespace RadioE45.Services.Updates;

/// <summary>What a GitHub release must contain for a channel to offer it.</summary>
public enum GitHubReleaseChannel
{
    /// <summary>Linux `.deb` packages (Uno desktop).</summary>
    LinuxDeb,

    /// <summary>Windows Inno Setup installers (`RadioE45_Setup_*.exe`), outside the Microsoft Store.</summary>
    WindowsInstaller,
}

/// <summary>
/// Latest GitHub release of the app, for channels without a store. One release carries every
/// platform's files, so the release only counts when it holds the file of this channel.
/// </summary>
public sealed class GitHubReleaseUpdateSource : IAppUpdateSource
{
    public const string LatestReleaseUrl = "https://api.github.com/repos/delfuria/RadioE45/releases/latest";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GitHubReleaseChannel _channel;

    public GitHubReleaseUpdateSource(IHttpClientFactory httpClientFactory, GitHubReleaseChannel channel)
    {
        _httpClientFactory = httpClientFactory;
        _channel = channel;
    }

    public bool IsSupported => true;

    public async Task<AppUpdateInfo?> GetLatestAsync(CancellationToken cancellationToken)
    {
        using HttpClient client = _httpClientFactory.CreateClient(AppleLookupUpdateSource.HttpClientName);
        using HttpRequestMessage request = new(HttpMethod.Get, LatestReleaseUrl);
        // GitHub rejects requests without a User-Agent.
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("RadioE45", "1"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        return Parse(await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken), _channel);
    }

    internal static AppUpdateInfo? Parse(JsonDocument document, GitHubReleaseChannel channel)
    {
        using (document)
        {
            JsonElement release = document.RootElement;
            if (release.ValueKind != JsonValueKind.Object || IsTrue(release, "draft") || IsTrue(release, "prerelease"))
                return null;

            string? tag = release.TryGetProperty("tag_name", out JsonElement t) ? t.GetString() : null;
            if (!AppVersion.TryParse(tag, out AppVersion version))
                throw new FormatException($"Unreadable release tag '{tag}'.");

            if (!HasAssetFor(release, channel))
                return null;

            Uri? url = release.TryGetProperty("html_url", out JsonElement u)
                       && Uri.TryCreate(u.GetString(), UriKind.Absolute, out Uri? created)
                       && created.Scheme == Uri.UriSchemeHttps
                       && created.Host == "github.com"
                ? created
                : null;

            return new AppUpdateInfo(version, url is null ? AppUpdateAction.None : AppUpdateAction.OpenUrl, url);
        }
    }

    private static bool HasAssetFor(JsonElement release, GitHubReleaseChannel channel)
    {
        if (!release.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
            return false;

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            string? name = asset.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
            if (name is null)
                continue;

            bool match = channel switch
            {
                GitHubReleaseChannel.LinuxDeb => name.EndsWith(".deb", StringComparison.OrdinalIgnoreCase),
                GitHubReleaseChannel.WindowsInstaller =>
                    name.StartsWith("RadioE45_Setup", StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase),
                _ => false,
            };
            if (match)
                return true;
        }

        return false;
    }

    private static bool IsTrue(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.True;
}
