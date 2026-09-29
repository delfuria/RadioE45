namespace RadioE45.Models;

// Link della stazione mostrato come icona nel menu laterale. Glyph è un codepoint di
// Font Awesome (FontFamily "FaBrands" per i social riconosciuti, "FaSolid" per il resto).
public sealed record StationLink(string Glyph, string FontFamily, Uri Uri)
{
    public const string BrandsFont = "FaBrands";
    public const string SolidFont = "FaSolid";

    private const string GlobeGlyph = "\uf0ac";
    private const string LinkGlyph = "\uf0c1";
    private const string EnvelopeGlyph = "\uf0e0";
    private const string PhoneGlyph = "\uf095";

    // Chiave: suffisso dell'host (confronto su "host == chiave" o "host termina con .chiave").
    private static readonly (string Host, string Glyph)[] SocialHosts =
    [
        ("facebook.com", "\uf09a"),
        ("fb.com", "\uf09a"),
        ("instagram.com", "\uf16d"),
        ("x.com", "\ue61b"),
        ("twitter.com", "\ue61b"),
        ("youtube.com", "\uf167"),
        ("youtu.be", "\uf167"),
        ("tiktok.com", "\ue07b"),
        ("t.me", "\uf2c6"),
        ("telegram.me", "\uf2c6"),
        ("wa.me", "\uf232"),
        ("whatsapp.com", "\uf232"),
        ("linkedin.com", "\uf08c"),
        ("spotify.com", "\uf1bc"),
        ("twitch.tv", "\uf1e8"),
        ("soundcloud.com", "\uf1be"),
        ("mixcloud.com", "\uf289"),
        ("threads.net", "\ue618"),
        ("threads.com", "\ue618"),
        ("bsky.app", "\ue671"),
    ];

    public static StationLink? ForWebsite(string? url)
        => TryCreateHttpUri(url) is Uri uri ? new StationLink(GlobeGlyph, SolidFont, uri) : null;

    public static StationLink? ForSocial(string? url)
    {
        if (TryCreateHttpUri(url) is not Uri uri)
            return null;

        string host = uri.Host.ToLowerInvariant();
        foreach ((string socialHost, string glyph) in SocialHosts)
        {
            if (host == socialHost || host.EndsWith("." + socialHost, StringComparison.Ordinal))
                return new StationLink(glyph, BrandsFont, uri);
        }

        return new StationLink(LinkGlyph, SolidFont, uri);
    }

    public static StationLink? ForEmail(string? email)
    {
        string? value = email?.Trim();
        if (string.IsNullOrEmpty(value) || !value.Contains('@'))
            return null;

        return Uri.TryCreate($"mailto:{value}", UriKind.Absolute, out Uri? uri)
            ? new StationLink(EnvelopeGlyph, SolidFont, uri)
            : null;
    }

    public static StationLink? ForPhone(string? phone)
    {
        string digits = new((phone ?? "").Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (digits.Length < 3)
            return null;

        return Uri.TryCreate($"tel:{digits}", UriKind.Absolute, out Uri? uri)
            ? new StationLink(PhoneGlyph, SolidFont, uri)
            : null;
    }

    // Accetta anche indirizzi scritti senza schema ("radioe45.it" → "https://radioe45.it").
    public static string? NormalizeUrl(string? url)
    {
        string? value = url?.Trim();
        if (string.IsNullOrEmpty(value))
            return null;

        return value.Contains("://", StringComparison.Ordinal) ? value : $"https://{value}";
    }

    private static Uri? TryCreateHttpUri(string? url)
    {
        string? normalized = NormalizeUrl(url);
        if (normalized is null)
            return null;

        return Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri)
               && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
               && !string.IsNullOrEmpty(uri.Host)
            ? uri
            : null;
    }
}
