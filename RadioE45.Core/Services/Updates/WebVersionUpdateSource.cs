using System.Text.Json;
using RadioE45.Models;
using RadioE45.Services.Platform;

namespace RadioE45.Services.Updates;

/// <summary>
/// Web app: the build published with the site writes `version.json` next to `index.html`
/// (`{"version":"0.46"}`, from the repository's version.json). A visitor whose page was loaded
/// earlier compares its own version with the one served now. Opening the app again (new tab,
/// cache-busted URL) loads the new build.
/// </summary>
public sealed class WebVersionUpdateSource : IAppUpdateSource
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Uri _appBase;

    /// <param name="appBase">Folder of the published app, ending with a slash (https://host/RadioE45/).</param>
    public WebVersionUpdateSource(IHttpClientFactory httpClientFactory, Uri appBase)
    {
        _httpClientFactory = httpClientFactory;
        _appBase = appBase;
    }

    public bool IsSupported => true;

    public async Task<AppUpdateInfo?> GetLatestAsync(CancellationToken cancellationToken)
    {
        // The query string defeats HTTP caches: the file must reflect the site as published now.
        Uri versionUrl = new(_appBase, $"version.json?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");

        using HttpClient client = _httpClientFactory.CreateClient(AppleLookupUpdateSource.HttpClientName);
        using HttpResponseMessage response = await client.GetAsync(versionUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        return Parse(await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken), _appBase);
    }

    internal static AppUpdateInfo Parse(JsonDocument document, Uri appBase)
    {
        using (document)
        {
            string? text = document.RootElement.ValueKind == JsonValueKind.Object
                           && document.RootElement.TryGetProperty("version", out JsonElement v)
                           && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;
            if (!AppVersion.TryParse(text, out AppVersion version))
                throw new FormatException($"Unreadable web version '{text}'.");

            // A new URL for the same app: the browser fetches index.html again instead of reusing the cached one.
            Uri reload = new(appBase, $"?v={version}");
            return new AppUpdateInfo(version, AppUpdateAction.OpenUrl, reload);
        }
    }
}
