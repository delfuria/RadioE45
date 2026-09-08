using System.Globalization;
using RadioE45.Models;

namespace RadioE45.Converters;

// Verde = mai ascoltato, giallo = iniziato, rosso = terminato. Le chiavi colore sono statiche
// (non swap dark/light, vedi Colors.xaml) — lette a runtime dai resource dictionary dell'app.
public class PodcastProgressStateToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string key = value switch
        {
            PodcastEpisodeProgressState.InProgress => "AmberYellow",
            PodcastEpisodeProgressState.Completed => "LiveRed",
            _ => "HlsGreen",
        };

        return Application.Current?.Resources.TryGetValue(key, out object? color) == true
            ? color
            : Colors.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
