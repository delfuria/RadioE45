using SQLite;

namespace RadioE45.Models;

[Table("AppSettings")]
public class AppSettings
{
    [PrimaryKey]
    public int Id { get; set; } = 1;
    public string ThemePreference { get; set; } = "Dark";
    public bool MustUpdate { get; set; }
    public decimal SeedVersion { get; set; }
    public bool StartWithFavorite { get; set; }
    public bool CrashReportingEnabled { get; set; }
    public bool CrashReportingConsentRequested { get; set; }
    public string DesktopOrientation { get; set; } = "Portrait";
    public int PlaybackLatencyOffsetSeconds { get; set; } = 3;

    // Default: preferisci il flusso diretto Icecast/MP3 — l'HLS di AzuraCast (segmentazione standard,
    // non low-latency) ha un ritardo strutturale di alcuni secondi rispetto al live edge reale. Vedi
    // GetStreamCandidates/TryOpenStreamAsync per l'uso.
    public bool PreferHlsStream { get; set; }
}
