using System.Globalization;
using RadioE45.Services.Platform;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace RadioE45.Services.Shared;

/// <summary>
/// <see cref="ISettingsStore"/> on <see cref="ApplicationData.LocalSettings"/>: a settings file
/// under the app data folder on desktop, localStorage on the web.
/// </summary>
public sealed class UnoSettingsStore : ISettingsStore
{
    private static IPropertySet Values => ApplicationData.Current.LocalSettings.Values;

    public T Get<T>(string key, T defaultValue)
    {
        if (!Values.TryGetValue(key, out var value) || value is null)
            return defaultValue;
        if (value is T typed)
            return typed;

        // Backends may hand back a different primitive type (e.g. a string from localStorage).
        try
        {
            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            return defaultValue;
        }
    }

    public void Set<T>(string key, T value) => Values[key] = value;

    public bool ContainsKey(string key) => Values.ContainsKey(key);

    public void Remove(string key) => Values.Remove(key);
}
