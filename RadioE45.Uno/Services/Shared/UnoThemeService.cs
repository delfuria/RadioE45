using RadioE45.Services.Platform;

namespace RadioE45.Services.Shared;

/// <summary>
/// <see cref="IThemeService"/>: sets RequestedTheme on the window content. "System" leaves it to
/// the OS/browser theme, which the app follows live (App.xaml sets no RequestedTheme).
/// </summary>
public sealed class UnoThemeService(UnoWindowHost host) : IThemeService
{
    /// <summary>Settings key and default shared with the MAUI app (SettingsViewModel, App).</summary>
    public const string PreferenceKey = "theme_preference";
    public const string DefaultPreference = "Dark";

    public void Apply(string preference)
    {
        if (host.Root is { } root)
        {
            root.RequestedTheme = preference switch
            {
                "Light" => ElementTheme.Light,
                "System" => ElementTheme.Default,
                _ => ElementTheme.Dark,
            };
        }
    }
}
