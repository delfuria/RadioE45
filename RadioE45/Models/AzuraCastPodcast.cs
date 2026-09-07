using System.Text.Json.Serialization;
using System.Windows.Input;

namespace RadioE45.Models;

public class AzuraCastPodcast
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("art")]
    public string? ArtworkUrl { get; set; }

    [JsonPropertyName("episodes")]
    public int EpisodeCount { get; set; }

    [JsonIgnore]
    public ICommand? SelectCommand { get; set; }
}
