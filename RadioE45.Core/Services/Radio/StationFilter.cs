using System.Globalization;
using System.Text;
using RadioE45.Models;

namespace RadioE45.Services.Radio;

/// <summary>
/// Filters stations by name: the station list box and the Android Auto search use the same rule.
/// </summary>
public static class StationFilter
{
    /// <summary>
    /// The stations whose name contains <paramref name="text"/>, in the order they are given.
    /// An empty text matches every station.
    /// </summary>
    public static IEnumerable<AzuraStation> Filter(IEnumerable<AzuraStation> stations, string? text)
    {
        string filter = Fold((text ?? string.Empty).Trim());
        return filter.Length == 0
            ? stations
            : stations.Where(s => Fold(s.Name).Contains(filter, StringComparison.Ordinal));
    }

    // Case- and accent-insensitive on every platform: it does not depend on the ICU data (the web
    // and some Android builds run with invariant globalization).
    private static string Fold(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(c));
        }
        return builder.ToString();
    }
}
