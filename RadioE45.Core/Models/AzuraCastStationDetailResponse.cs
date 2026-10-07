using System.Text.Json.Serialization;

namespace RadioE45.Models;

public class AzuraCastStationDetailResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("shortcode")]
    public string Shortcode { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("listen_url")]
    public string ListenUrl { get; set; } = "";

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("hls_enabled")]
    public bool HlsEnabled { get; set; }

    [JsonPropertyName("hls_is_default")]
    public bool HlsIsDefault { get; set; }

    [JsonPropertyName("hls_url")]
    public string? HlsUrl { get; set; }
}
