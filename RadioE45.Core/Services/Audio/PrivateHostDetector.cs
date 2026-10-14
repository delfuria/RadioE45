using System.Net;
using System.Net.Sockets;
using RadioE45.Models;

namespace RadioE45.Services.Audio;

/// <summary>
/// Purely syntactic check (no DNS) for hosts that live on the local network. On iOS/Mac Catalyst
/// any connection to such a host triggers the Local Network permission prompt, so public stations
/// must not touch them — AzuraCast servers with a misconfigured Base URL return LAN addresses
/// (e.g. listen_url = http://192.168.1.100/...) that would otherwise be probed on every play.
/// Stations that really are on the LAN (a home/studio AzuraCast) are left untouched.
/// </summary>
public static class PrivateHostDetector
{
    private static readonly string[] PrivateSuffixes = [".local", ".lan", ".home.arpa", ".internal"];

    /// <summary>True for private/loopback/link-local IPs, dotless hostnames, localhost and LAN-only suffixes.</summary>
    public static bool IsPrivateHost(string? urlOrHost)
    {
        string? host = ExtractHost(urlOrHost);
        if (string.IsNullOrEmpty(host))
            return false;

        if (IPAddress.TryParse(host, out IPAddress? ip))
            return IsPrivateAddress(ip);

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || !host.Contains('.'))
            return true;

        return PrivateSuffixes.Any(s => host.EndsWith(s, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A station is "on the local network" when its own host is private. UrlBase is the most reliable
    /// signal: it is what the user entered and what every API call goes to, whereas StreamUrl/HlsUrl
    /// come from the AzuraCast Base URL setting (the misconfigured part) and OnAirStreamUrl is just
    /// the last candidate that won. StreamUrlFallback (built from UrlBase) covers an empty UrlBase.
    /// </summary>
    public static bool IsLocalStation(AzuraStation station) =>
        IsPrivateHost(string.IsNullOrWhiteSpace(station.UrlBase) ? station.StreamUrlFallback : station.UrlBase);

    /// <summary>
    /// Drops private-host candidates for a public station, keeping order. LAN stations are returned
    /// unchanged; if nothing public is left the original list is returned (better to try than not play).
    /// </summary>
    public static string[] FilterCandidates(string[] candidates, AzuraStation station)
    {
        if (IsLocalStation(station))
            return candidates;

        string[] publicOnly = candidates.Where(u => !IsPrivateHost(u)).ToArray();
        return publicOnly.Length > 0 ? publicOnly : candidates;
    }

    /// <summary>The URL itself, or null when it points to a private host and the station is public.</summary>
    public static string? FilterUrl(string? url, AzuraStation station) =>
        url is not null && IsPrivateHost(url) && !IsLocalStation(station) ? null : url;

    private static string? ExtractHost(string? urlOrHost)
    {
        if (string.IsNullOrWhiteSpace(urlOrHost))
            return null;

        string value = urlOrHost.Trim();

        // Bare IPv6 literals ("::1", "fe80::1") — "http://::1" would not parse as a URI.
        if (!value.Contains("://", StringComparison.Ordinal)
            && IPAddress.TryParse(value.Trim('[', ']'), out IPAddress? bare)
            && bare.AddressFamily == AddressFamily.InterNetworkV6)
            return value.Trim('[', ']');

        string candidate = value.Contains("://", StringComparison.Ordinal) ? value : $"http://{value}";
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) || string.IsNullOrEmpty(uri.Host))
            return null;

        return uri.Host.Trim('[', ']').TrimEnd('.');
    }

    private static bool IsPrivateAddress(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] b = ip.GetAddressBytes();
            return b[0] == 10
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 169 && b[1] == 254)
                || b[0] == 127;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            byte[] b = ip.GetAddressBytes();
            return ip.IsIPv6LinkLocal          // fe80::/10
                || (b[0] & 0xFE) == 0xFC;      // fc00::/7 (ULA)
        }

        return false;
    }
}
