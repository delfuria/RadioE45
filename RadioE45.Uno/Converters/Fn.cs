using System.Collections;
using Microsoft.UI.Xaml.Media.Imaging;
using RadioE45.Models;

namespace RadioE45.Converters;

/// <summary>
/// Static functions for <c>{x:Bind}</c>, the Uno counterparts of the CommunityToolkit.Maui
/// converters used by the MAUI pages (InvertedBool, IsNull/IsNotNull, BoolToObject…), see
/// docs/parity/MAUI-TO-UNO.md. Purely visual: no app logic here.
/// </summary>
public static class Fn
{
    public static bool Not(bool value) => !value;

    public static Visibility Visible(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility Collapsed(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility IsNull(object? value) => Visible(value is null);

    public static Visibility IsNotNull(object? value) => Visible(value is not null);

    public static Visibility IsNullOrEmpty(string? value) => Visible(string.IsNullOrEmpty(value));

    public static Visibility IsNotNullOrEmpty(string? value) => Visible(!string.IsNullOrEmpty(value));

    public static Visibility IsNotEmpty(IEnumerable? items) =>
        Visible(items?.GetEnumerator() is { } e && e.MoveNext());

    /// <summary>Counterpart of <c>CollectionView.EmptyView</c>: visible when there are no items.</summary>
    public static Visibility IsEmpty(IEnumerable? items) =>
        Visible(items?.GetEnumerator() is not { } e || !e.MoveNext());

    /// <summary>Empty view of a list that is loaded asynchronously: not shown while loading.</summary>
    public static Visibility IsEmptyAndIdle(IEnumerable? items, bool isBusy) =>
        isBusy ? Visibility.Collapsed : IsEmpty(items);

    /// <summary>Schedule: accent frame around streamer names (MAUI DataTrigger on IsStreamer).</summary>
    public static Thickness StreamerFrame(bool isStreamer) => new(isStreamer ? 1.5 : 0);

    public static Thickness StreamerPadding(bool isStreamer) => isStreamer ? new Thickness(10, 4, 10, 4) : new Thickness(0);

    /// <summary>MAUI DataTrigger that sets Opacity 0.5 (e.g. a song already requested).</summary>
    public static double DimmedIf(bool value) => value ? 0.5 : 1.0;

    public static double ToDouble(int value) => value;

    /// <summary>MAUI <c>StringFormat='{}{0} s'</c>.</summary>
    public static string Seconds(int value) => $"{value} s";

    public static string Text(object? value) => value?.ToString() ?? string.Empty;

    /// <summary>MAUI <c>StringFormat='{}{0:F2}'</c>.</summary>
    public static string F2(float value) => value.ToString("F2");

    /// <summary>MAUI <c>StringFormat='{0:HH:mm}'</c>.</summary>
    public static string Time(DateTimeOffset value) => value.ToString("HH:mm");

    /// <summary>MAUI <c>StringFormat='{0:dddd d MMMM}'</c> (current culture).</summary>
    public static string DayDate(DateTimeOffset value) => value.ToString("dddd d MMMM");

    /// <summary>MAUI <c>StringFormat='{0:dd MMM yyyy}'</c>.</summary>
    public static string ShortDate(DateTimeOffset value) => value.ToString("dd MMM yyyy");

    /// <summary>
    /// Podcast episode dot (MAUI PodcastProgressStateToColorConverter): green = not started,
    /// yellow = in progress, red = completed.
    /// </summary>
    public static Brush ProgressStateBrush(PodcastEpisodeProgressState state) =>
        (Brush)Application.Current.Resources[state switch
        {
            PodcastEpisodeProgressState.InProgress => "AmberYellowBrush",
            PodcastEpisodeProgressState.Completed => "LiveRedBrush",
            _ => "HlsGreenBrush",
        }];

    /// <summary>Font from its resource key in Fonts.xaml ("FaSolid", "FaBrands": StationLink.FontFamily).</summary>
    public static FontFamily Font(string key) =>
        Application.Current.Resources.TryGetValue(key, out var font) && font is FontFamily family
            ? family
            : FontFamily.XamlAutoFontFamily;

    // Font Awesome Solid glyphs that replace the MAUI emoji/symbols (the web Skia renderer has
    // no font for ▶ ⏸ 🔊 🔇 ▲ ▼). Here and not in XAML: entities inside x:Bind string literals
    // are not decoded.
    public static string PlayPauseGlyph(bool isPlaying) => isPlaying ? "\uf04c" : "\uf04b";

    public static string MuteGlyph(bool isMuted) => isMuted ? "\uf6a9" : "\uf028";

    public static string ChevronGlyph(bool isExpanded) => isExpanded ? "\uf077" : "\uf078";

    /// <summary>Image from a URL; null (no image) when the URL is missing or invalid.</summary>
    public static ImageSource? Image(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? new BitmapImage(uri) : null;
}
