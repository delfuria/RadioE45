using System.Text.Json.Serialization;

namespace RadioE45.Logic.AzuraResponses;

public class Station
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("shortcode")]
    public string Shortcode { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("frontend")]
    public string Frontend { get; set; } = string.Empty;

    [JsonPropertyName("backend")]
    public string Backend { get; set; } = string.Empty;

    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = string.Empty;

    [JsonPropertyName("listen_url")]
    public string ListenUrl { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("public_player_url")]
    public string PublicPlayerUrl { get; set; } = string.Empty;

    [JsonPropertyName("playlist_pls_url")]
    public string PlaylistPlsUrl { get; set; } = string.Empty;

    [JsonPropertyName("playlist_m3u_url")]
    public string PlaylistM3uUrl { get; set; } = string.Empty;

    [JsonPropertyName("is_public")]
    public bool IsPublic { get; set; }

    [JsonPropertyName("mounts")]
    public List<Mount> Mounts { get; set; } = [];

    [JsonPropertyName("remotes")]
    public List<object> Remotes { get; set; } = [];

    [JsonPropertyName("hls_enabled")]
    public bool HlsEnabled { get; set; }

    [JsonPropertyName("hls_is_default")]
    public bool HlsIsDefault { get; set; }

    [JsonPropertyName("hls_url")]
    public string? HlsUrl { get; set; }

    [JsonPropertyName("hls_listeners")]
    public int HlsListeners { get; set; }
}
