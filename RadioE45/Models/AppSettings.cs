using SQLite;

namespace RadioE45.Models;

[Table("AppSettings")]
public class AppSettings
{
    [PrimaryKey]
    public int Id { get; set; } = 1;
    public double Volume { get; set; } = 1.0;
    public string ThemePreference { get; set; } = "Dark";
    public bool MustUpdate { get; set; }
    public decimal SeedVersion { get; set; }
    public bool StartWithFavorite { get; set; }
    public bool CrashReportingEnabled { get; set; }
    public bool CrashReportingConsentRequested { get; set; }
    public string DesktopOrientation { get; set; } = "Portrait";
}
