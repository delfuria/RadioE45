using Microsoft.Maui.Storage;
using RadioE45.Models;

namespace RadioE45.Services.CrashReporting;

internal static class CrashReportingSettings
{
    private const string EnabledKey = "crash_reporting_enabled";
    private const string ConsentRequestedKey = "crash_reporting_consent_requested";
    private const string LegacyEnabledKey = "android_crash_reporting_enabled";
    private const string LegacyConsentRequestedKey = "android_crash_reporting_consent_requested";

    public static bool IsEnabled()
    {
        if (Preferences.Default.ContainsKey(EnabledKey))
            return Preferences.Default.Get(EnabledKey, false);

        return Preferences.Default.Get(LegacyEnabledKey, false);
    }

    public static bool HasRequestedConsent()
    {
        if (Preferences.Default.ContainsKey(ConsentRequestedKey))
            return Preferences.Default.Get(ConsentRequestedKey, false);

        return Preferences.Default.Get(LegacyConsentRequestedKey, false);
    }

    public static void SaveToPreferences(bool enabled, bool consentRequested)
    {
        Preferences.Default.Set(EnabledKey, enabled);
        Preferences.Default.Set(ConsentRequestedKey, consentRequested);
        Preferences.Default.Remove(LegacyEnabledKey);
        Preferences.Default.Remove(LegacyConsentRequestedKey);
    }

    public static void ApplyTo(AppSettings settings, bool enabled, bool consentRequested)
    {
        settings.CrashReportingEnabled = enabled;
        settings.CrashReportingConsentRequested = consentRequested;
    }
}
