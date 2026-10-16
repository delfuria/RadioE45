using System.Text.Json.Serialization;
using System.Globalization;
using System.Windows.Input;
using RadioE45.Services.Localization;

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

    // "1 episodio" / "2 episodi"; in polacco anche la forma 2-4 ("odcinki") distinta da 5+ ("odcinków").
    [JsonIgnore]
    public string EpisodeCountText =>
        LocalizationResourceManager.Instance.Format(
            "Podcast_EpisodeCount_" + PluralForm(EpisodeCount, LocalizationResourceManager.Instance.CurrentCulture),
            EpisodeCount);

    public static string PluralForm(int count, CultureInfo culture)
    {
        if (count == 1)
            return "One";

        if (culture.TwoLetterISOLanguageName == "pl")
        {
            int lastTwo = count % 100;
            if (count % 10 is >= 2 and <= 4 && lastTwo is < 12 or > 14)
                return "Few";
        }

        return "Many";
    }
}
