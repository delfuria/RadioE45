using System.Text.Json.Serialization;
using System.Windows.Input;
using RadioE45.Services.Localization;

namespace RadioE45.Models;

public enum PodcastEpisodeProgressState
{
    NotStarted,
    InProgress,
    Completed,
}

public class AzuraCastPodcastEpisode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("season_number")]
    public int? SeasonNumber { get; set; }

    [JsonPropertyName("episode_number")]
    public int? EpisodeNumber { get; set; }

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

    // Valorizzati da PodcastEpisodesViewModel dopo il fetch, leggendo PodcastEpisodeProgress —
    // non fanno parte del JSON dell'episodio. L'episodio non è un ObservableObject: questi valori
    // riflettono lo stato al momento del caricamento della lista, non si aggiornano in diretta
    // durante la riproduzione (la pagina si ricarica comunque a ogni OnAppearing).
    [JsonIgnore]
    public int ResumePositionSeconds { get; set; }

    [JsonIgnore]
    public PodcastEpisodeProgressState ProgressState { get; set; } = PodcastEpisodeProgressState.NotStarted;

    [JsonIgnore]
    public string SeasonEpisodeText => SeasonNumber is { } season && EpisodeNumber is { } number
        ? LocalizationResourceManager.Instance.Format("Podcast_SeasonEpisode", season, number)
        : string.Empty;

    [JsonIgnore]
    public string DurationText => FormatTime(Duration);

    [JsonIgnore]
    public string ResumePositionText => FormatTime(TimeSpan.FromSeconds(ResumePositionSeconds));

    private static string FormatTime(TimeSpan ts) =>
        ts.Hours > 0 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
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
