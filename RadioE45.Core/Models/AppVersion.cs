using System.Globalization;

namespace RadioE45.Models;

/// <summary>
/// App version reduced to Major.Minor, the only part compared when looking for updates: patch
/// releases never prompt (docs/APP-UPDATE-ROADMAP.md D7). Parsing accepts the formats the
/// channels use: "0.46", "0.46.6", "v0.46.6", "0.46.0.0", "0.46.6-beta".
/// </summary>
public readonly record struct AppVersion(int Major, int Minor) : IComparable<AppVersion>
{
    public static bool TryParse(string? text, out AppVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        ReadOnlySpan<char> span = text.AsSpan().Trim();
        if (span.Length > 0 && span[0] is 'v' or 'V')
            span = span[1..];

        int suffix = span.IndexOfAny('-', '+');
        if (suffix >= 0)
            span = span[..suffix];

        string[] parts = span.ToString().Split('.');
        if (parts.Length > 4 || !TryPart(parts[0], out int major))
            return false;

        int minor = 0;
        if (parts.Length > 1 && !TryPart(parts[1], out minor))
            return false;

        // The remaining components are validated but ignored.
        for (int i = 2; i < parts.Length; i++)
        {
            if (!TryPart(parts[i], out _))
                return false;
        }

        version = new AppVersion(major, minor);
        return true;
    }

    public int CompareTo(AppVersion other) =>
        Major != other.Major ? Major.CompareTo(other.Major) : Minor.CompareTo(other.Minor);

    public static bool operator <(AppVersion left, AppVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(AppVersion left, AppVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(AppVersion left, AppVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(AppVersion left, AppVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{Major}.{Minor}";

    private static bool TryPart(string text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
