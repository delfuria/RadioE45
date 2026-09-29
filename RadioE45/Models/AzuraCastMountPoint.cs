using System.Text.Json.Serialization;

namespace RadioE45.Models;

public class AzuraCastMountPoint
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("is_default")]
    public bool IsDefault { get; set; }

    // Full public URL of the mount (e.g. http://host/listen/{shortcode}/radio.mp3 behind the web
    // proxy, or http://host:8000/radio.mp3 with the proxy disabled). Path alone is relative to the
    // Icecast listener port, so it 404s when the stream is only reachable through the proxy.
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    // Suffix appended to the station's UrlBase to build the stream URL: the mount URL's path,
    // prefixed with ":port" when it isn't the scheme default (same shape as ":8060/radio.mp3").
    public string StreamSuffix
    {
        get
        {
            if (!Uri.TryCreate(Url, UriKind.Absolute, out Uri? uri))
                return Path;

            return uri.IsDefaultPort ? uri.PathAndQuery : $":{uri.Port}{uri.PathAndQuery}";
        }
    }
}
