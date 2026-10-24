using System.Globalization;
using System.Text.Json;
using RadioE45.Models;
using RadioE45.Services.Platform;

namespace RadioE45.Services.Updates;

/// <summary>
/// iOS and Mac App Store: latest published version from the iTunes Lookup API (no official update API
/// exists). It reflects the published version, not the one in review. The app is sold in every
/// country, so the device region is enough (docs/APP-UPDATE-ROADMAP.md D8).
/// </summary>
public sealed class AppleLookupUpdateSource : IAppUpdateSource
{
    public const string HttpClientName = "Updates";
    public const string BundleId = "com.radioe45.app";
    internal const string FallbackCountry = "it";

    /// <summary>Lookup entity for iOS apps (iPhone and iPad).</summary>
    public const string IosEntity = "software";

    /// <summary>Lookup entity for Mac apps (Mac App Store, Mac Catalyst build).</summary>
    public const string MacEntity = "macSoftware";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _entity;

    public AppleLookupUpdateSource(IHttpClientFactory httpClientFactory, string entity)
    {
        _httpClientFactory = httpClientFactory;
        _entity = entity;
    }

    public bool IsSupported => true;

    public async Task<AppUpdateInfo?> GetLatestAsync(CancellationToken cancellationToken)
    {
        string country = NormalizeCountry(RegionInfo.CurrentRegion.TwoLetterISORegionName);
        // The cb parameter changes on every request: Apple's CDN caches each URL for about 15 h,
        // so without it a new release stays invisible for up to a day.
        long cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Uri uri = new($"https://itunes.apple.com/lookup?bundleId={BundleId}&entity={_entity}&cb={cacheBuster}&country={country}");

        using HttpClient client = _httpClientFactory.CreateClient(HttpClientName);
        using HttpResponseMessage response = await client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        return Parse(await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken));
    }

    internal static AppUpdateInfo? Parse(JsonDocument document)
    {
        using (document)
        {
            if (!document.RootElement.TryGetProperty("results", out JsonElement results)
                || results.ValueKind != JsonValueKind.Array
                || results.GetArrayLength() == 0)
            {
                return null;
            }

            JsonElement app = results[0];
            string? version = app.TryGetProperty("version", out JsonElement v) ? v.GetString() : null;
            if (!AppVersion.TryParse(version, out AppVersion parsed))
                throw new FormatException($"Unreadable App Store version '{version}'.");

            Uri? url = app.TryGetProperty("trackViewUrl", out JsonElement u)
                       && Uri.TryCreate(u.GetString(), UriKind.Absolute, out Uri? created)
                       && created.Scheme == Uri.UriSchemeHttps
                ? created
                : null;

            return new AppUpdateInfo(parsed, url is null ? AppUpdateAction.None : AppUpdateAction.OpenUrl, url);
        }
    }

    /// <summary>Two-letter lowercase code; anything else (invariant "IV", "001", empty) falls back to Italy.</summary>
    internal static string NormalizeCountry(string? region) =>
        region is { Length: 2 } && region.All(char.IsAsciiLetter) && !region.Equals("IV", StringComparison.OrdinalIgnoreCase)
            ? region.ToLowerInvariant()
            : FallbackCountry;
}
