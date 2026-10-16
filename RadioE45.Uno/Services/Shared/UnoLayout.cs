using RadioE45.Services.Platform;

namespace RadioE45.Services.Shared;

/// <summary>
/// Layout rules that differ between the Uno heads. The desktop window has the fixed MAUI size
/// (portrait or landscape, DesktopWindowSize). The browser window can be any size: there the
/// whole app (tabs included) is a single centered column as wide as the portrait desktop
/// window, and pages always use their portrait layout (a two-column landscape layout in a wide
/// browser was a poor experience).
/// </summary>
public static class UnoLayout
{
    /// <summary>True on the web: portrait layout only, whatever the browser size.</summary>
    public static bool IsSingleColumn => OperatingSystem.IsBrowser();

    /// <summary>Maximum width of the app column on the web.</summary>
    public static double ColumnMaxWidth => DesktopWindowSize.PortraitWidth;
}
