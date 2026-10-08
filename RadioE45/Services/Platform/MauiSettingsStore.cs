namespace RadioE45.Services.Platform;

public sealed class MauiSettingsStore : ISettingsStore
{
    public T Get<T>(string key, T defaultValue) => Preferences.Default.Get(key, defaultValue);

    public void Set<T>(string key, T value) => Preferences.Default.Set(key, value);

    public bool ContainsKey(string key) => Preferences.Default.ContainsKey(key);

    public void Remove(string key) => Preferences.Default.Remove(key);
}
