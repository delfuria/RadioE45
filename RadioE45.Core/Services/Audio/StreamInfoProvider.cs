using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using RadioE45.Models;

namespace RadioE45.Services.Audio;

public class StreamInfoProvider : IStreamInfoProvider
{
    private static readonly TimeSpan HeadersTimeout = TimeSpan.FromSeconds(3);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<StreamInfoProvider> _logger;

    public StreamInfoProvider(IHttpClientFactory httpClientFactory, ILogger<StreamInfoProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<StreamInfo> GetAsync(AzuraStation station, CancellationToken ct = default)
    {
        string address = PlayedUrl(station);
        bool isHls = IsHls(station, address);

        // An HLS playlist has no stream headers worth reading: only the format is known.
        HttpResponseHeaders? headers = null;
        string? contentType = null;
        if (!isHls && address.Length > 0)
            (headers, contentType) = await ReadHeadersAsync(address, ct);

        return Build(station, address, isHls, headers is null ? null : new HeaderValues(headers, contentType));
    }

    // The URL the player opened, or the one it would try first when nothing is playing yet.
    internal static string PlayedUrl(AzuraStation station)
    {
        if (!string.IsNullOrEmpty(station.OnAirStreamUrl))
            return station.OnAirStreamUrl;
        if (!string.IsNullOrEmpty(station.StreamUrl))
            return station.StreamUrl;
        return station.StreamUrlFallback;
    }

    internal static bool IsHls(AzuraStation station, string address) =>
        (!string.IsNullOrEmpty(station.HlsUrl) && address == station.HlsUrl)
        || address.Contains(".m3u8", StringComparison.OrdinalIgnoreCase);

    internal readonly record struct HeaderValues(HttpResponseHeaders Headers, string? ContentType);

    internal static StreamInfo Build(AzuraStation station, string address, bool isHls, HeaderValues? headerValues)
    {
        if (isHls)
            return new StreamInfo(station.Name, address, Format: "HLS");

        AzuraCastMountPoint? mount = station.Mounts.FirstOrDefault(m => m.Url == address)
            ?? station.Mounts.FirstOrDefault(m => m.IsDefault);

        int? bitrate = null, sampleRate = null, channels = null;
        string? genre = null;
        string? format = FormatName(mount?.Format);

        if (headerValues is { } values)
        {
            Dictionary<string, int> audioInfo = ParseAudioInfo(Header(values.Headers, "ice-audio-info"));
            audioInfo.TryGetValue("bitrate", out int infoBitrate);
            bitrate = infoBitrate > 0 ? infoBitrate : ParseLeadingInt(Header(values.Headers, "icy-br"));
            sampleRate = audioInfo.TryGetValue("samplerate", out int rate) && rate > 0 ? rate : null;
            channels = audioInfo.TryGetValue("channels", out int count) && count > 0 ? count : null;
            genre = Header(values.Headers, "icy-genre") is { Length: > 0 } g ? g.Trim() : null;
            format ??= FormatFromContentType(values.ContentType);
        }

        bitrate ??= mount?.Bitrate is > 0 ? mount.Bitrate : null;
        return new StreamInfo(station.Name, address, format, bitrate, sampleRate, channels, genre);
    }

    // "channels=2;samplerate=44100;bitrate=128" (AzuraCast) or "ice-channels=2;ice-samplerate=44100" (Icecast).
    internal static Dictionary<string, int> ParseAudioInfo(string? value)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
            return result;

        foreach (string part in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separator = part.IndexOf('=');
            if (separator <= 0)
                continue;

            string key = part[..separator].Trim();
            if (key.StartsWith("ice-", StringComparison.OrdinalIgnoreCase))
                key = key[4..];

            if (int.TryParse(part[(separator + 1)..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                result[key] = number;
        }

        return result;
    }

    internal static string? FormatName(string? format) =>
        string.IsNullOrWhiteSpace(format) ? null : format.Trim().ToLowerInvariant();

    internal static string? FormatFromContentType(string? contentType) => contentType?.Trim().ToLowerInvariant() switch
    {
        "audio/mpeg" or "audio/mp3" => "mp3",
        "audio/aac" or "audio/aacp" => "aac",
        "audio/ogg" or "application/ogg" => "ogg",
        "audio/flac" => "flac",
        { Length: > 0 } other when other.StartsWith("audio/", StringComparison.Ordinal) => other["audio/".Length..],
        _ => null,
    };

    private static int? ParseLeadingInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string first = value.Split(',', StringSplitOptions.TrimEntries)[0];
        return int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) && number > 0
            ? number
            : null;
    }

    private static string? Header(HttpResponseHeaders headers, string name) =>
        headers.TryGetValues(name, out IEnumerable<string>? values) ? values.FirstOrDefault() : null;

    private async Task<(HttpResponseHeaders? Headers, string? ContentType)> ReadHeadersAsync(string url, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(HeadersTimeout);

            HttpClient client = _httpClientFactory.CreateClient("AzuraCast");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if (!response.IsSuccessStatusCode)
                return (null, null);

            // The response is disposed right after this returns, which also closes the connection:
            // copy the values the caller needs.
            var copy = new HttpResponseMessage().Headers;
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
                copy.TryAddWithoutValidation(header.Key, header.Value);

            return (copy, response.Content.Headers.ContentType?.MediaType);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidOperationException)
        {
            _logger.LogDebug(ex, "Stream info: headers of {Url} not available", url);
            return (null, null);
        }
    }
}
