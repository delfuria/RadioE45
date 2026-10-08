using RadioE45.Services.CrashReporting;
#if !MACCATALYST
using Sentry;
#endif

namespace RadioE45.Services.Platform;

public sealed class MauiCrashReportingService : ICrashReportingService
{
#if MACCATALYST
    // Sentry is excluded from Mac Catalyst (see RadioE45.csproj): no reporter to configure.
    public bool IsAvailable => false;
#else
    public bool IsAvailable => CrashReportingConfiguration.IsConfigured;
#endif

    public bool IsEnabled() => CrashReportingSettings.IsEnabled();

    public void SaveConsent(bool enabled, bool consentRequested) =>
        CrashReportingSettings.SaveToPreferences(enabled, consentRequested);

    public void CaptureTestException(Exception exception)
    {
#if !MACCATALYST
        SentrySdk.CaptureException(exception);
#endif
    }
}
