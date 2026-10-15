using RadioE45.Models;

namespace RadioE45.Services.Platform;

/// <summary>
/// Fixed size of the main window on desktop heads (MAUI Mac Catalyst/Windows, Uno desktop), in
/// logical (DPI-independent) units. The orientation comes from the "desktop_orientation"
/// preference; the window is not resizable, so pages are designed for exactly these sizes.
/// </summary>
public static class DesktopWindowSize
{
    public const string OrientationKey = "desktop_orientation";

    public const double PortraitWidth = 500;
    public const double PortraitHeight = 855;
    public const double LandscapeWidth = 900;
    public const double LandscapeHeight = 450;

    /// <summary>Size for a stored orientation value ("Portrait"/"Landscape"); unknown values mean portrait.</summary>
    public static (double Width, double Height) For(string? orientation) =>
        Enum.TryParse(orientation, out DesktopOrientationMode mode) && mode == DesktopOrientationMode.Landscape
            ? (LandscapeWidth, LandscapeHeight)
            : (PortraitWidth, PortraitHeight);

    public static (double Width, double Height) For(ISettingsStore settings) =>
        For(settings.Get(OrientationKey, nameof(DesktopOrientationMode.Portrait)));
}
