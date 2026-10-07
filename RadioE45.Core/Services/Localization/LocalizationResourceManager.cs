using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace RadioE45.Services.Localization;

/// <summary>
/// Single source of truth for UI strings. Wraps the embedded <c>AppResources</c> RESX set
/// (neutral = Italian, plus <c>.en</c> and <c>.pl</c> satellites) and exposes them through an
/// indexer so XAML bindings and code can look strings up by key. Raising <see cref="PropertyChanged"/>
/// with a null name refreshes every bound string at once, which is what lets the language switch at
/// runtime. On Android the culture normally follows the device language automatically.
/// </summary>
public sealed class LocalizationResourceManager : INotifyPropertyChanged
{
    public static LocalizationResourceManager Instance { get; } = new();

    private readonly ResourceManager _resourceManager =
        new("RadioE45.Resources.Strings.AppResources", typeof(LocalizationResourceManager).Assembly);

    private CultureInfo _currentCulture = CultureInfo.CurrentUICulture;

    private LocalizationResourceManager() { }

    /// <summary>Localized string for <paramref name="key"/>; returns the key itself if missing.</summary>
    public string this[string key] => _resourceManager.GetString(key, _currentCulture) ?? key;

    /// <summary>Localized string, formatted with <paramref name="args"/> (uses the current culture).</summary>
    public string Format(string key, params object[] args) =>
        string.Format(_currentCulture, this[key], args);

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (Equals(_currentCulture, value))
                return;

            _currentCulture = value;
            CultureInfo.CurrentUICulture = value;
            CultureInfo.CurrentCulture = value;
            // Null/empty name tells every binding on this source to re-read its value.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
