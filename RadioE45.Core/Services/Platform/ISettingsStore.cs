namespace RadioE45.Services.Platform;

/// <summary>
/// Small key/value preferences (MAUI: Preferences.Default, Uno: ApplicationData.LocalSettings).
/// Structured settings live in SQLite (IAppSettingsRepository).
/// </summary>
public interface ISettingsStore
{
    T Get<T>(string key, T defaultValue);

    void Set<T>(string key, T value);

    bool ContainsKey(string key);

    void Remove(string key);
}
