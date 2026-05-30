using System.Text.Json.Serialization;

namespace RadioE45.Logic.AzuraResponses;

public class Song
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("art")]
    public string Art { get; set; } = string.Empty;

    [JsonPropertyName("custom_fields")]
    public List<object> CustomFields { get; set; } = [];

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("artist")]
    public string Artist { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("album")]
    public string Album { get; set; } = string.Empty;

    [JsonPropertyName("genre")]
    public string Genre { get; set; } = string.Empty;

    [JsonPropertyName("isrc")]
    public string Isrc { get; set; } = string.Empty;

    [JsonPropertyName("lyrics")]
    public string Lyrics { get; set; } = string.Empty;
}
