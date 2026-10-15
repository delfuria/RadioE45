using System.Collections;
using Microsoft.UI.Xaml.Media.Imaging;

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

    public static double ToDouble(int value) => value;

    /// <summary>MAUI <c>StringFormat='{}{0} s'</c>.</summary>
    public static string Seconds(int value) => $"{value} s";

    public static string Text(object? value) => value?.ToString() ?? string.Empty;

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
