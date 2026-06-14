namespace RadioE45.Services.CrashReporting;

internal static class CrashReportingConfiguration
{
    // DSN del progetto Sentry. Il valore è definito in AppSecrets.cs (file locale, non committato).
    public const string Dsn = AppSecrets.SentryDsn;

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(Dsn);
}
