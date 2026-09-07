using System.Text.Json.Serialization;
using System.Windows.Input;

namespace RadioE45.Models;

public class AzuraCastPodcastEpisode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("publish_at")]
    public long PublishAtUnix { get; set; }

    [JsonPropertyName("art")]
    public string? ArtworkUrl { get; set; }

    [JsonPropertyName("has_media")]
    public bool HasMedia { get; set; }

    [JsonPropertyName("media")]
    public AzuraCastPodcastMedia? Media { get; set; }

    [JsonPropertyName("links")]
    public AzuraCastPodcastEpisodeLinks? Links { get; set; }

    // Valorizzato da PodcastService dopo la deserializzazione — non presente nel JSON dell'episodio.
    [JsonIgnore]
    public string PodcastId { get; set; } = string.Empty;

    // URL di stream/download reale dell'episodio — va preso da links.download (assoluto, già
    // risolto dal server con lo schema di routing corretto) e MAI ricostruito a mano: il path
    // costruito manualmente (es. "/podcast/{id}/episode/{id}/media") non coincide con quello
    // realmente pubblico ("/public/podcast/{id}/episode/{id}/download.mp3", shortcode non ID
    // numerico) e viene rifiutato dal server.
    [JsonIgnore]
    public string MediaUrl => Links?.Download ?? string.Empty;

    [JsonIgnore]
    public DateTimeOffset PublishedAt => DateTimeOffset.FromUnixTimeSeconds(PublishAtUnix);

    [JsonIgnore]
    public TimeSpan Duration => Media is not null ? TimeSpan.FromSeconds(Media.LengthSeconds) : TimeSpan.Zero;

    [JsonIgnore]
    public ICommand? PlayCommand { get; set; }
}

public class AzuraCastPodcastMedia
{
    [JsonPropertyName("length")]
    public double LengthSeconds { get; set; }

    [JsonPropertyName("length_text")]
    public string? LengthText { get; set; }
}

public class AzuraCastPodcastEpisodeLinks
{
    [JsonPropertyName("download")]
    public string? Download { get; set; }
}
