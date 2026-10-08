namespace RadioE45.Services.Platform;

public interface ICrashReportingService
{
    /// <summary>Whether this head ships a configured crash reporter (false e.g. on Mac Catalyst).</summary>
    bool IsAvailable { get; }

    /// <summary>The user's current opt-in, as read at startup to decide whether to init the reporter.</summary>
    bool IsEnabled();

    /// <summary>Persists the opt-in where the next startup reads it (before the database is open).</summary>
    void SaveConsent(bool enabled, bool consentRequested);

    void CaptureTestException(Exception exception);
}
