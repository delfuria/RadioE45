using RadioE45.Services.Platform;

namespace RadioE45.Services.Shared;

/// <summary>
/// Crash reporting is not available on the Uno heads yet (decision S7: desktop and web are
/// decided together in S10). Settings hides the option when <see cref="IsAvailable"/> is false.
/// </summary>
public sealed class UnavailableCrashReportingService : ICrashReportingService
{
    public bool IsAvailable => false;

    public bool IsEnabled() => false;

    public void SaveConsent(bool enabled, bool consentRequested)
    {
    }

    public void CaptureTestException(Exception exception)
    {
    }
}
