#if RADIOE45_BROWSER
using System.Runtime.InteropServices.JavaScript;

namespace RadioE45.Services.Browser;

/// <summary>
/// Whether the browser lets the app start audio on its own
/// (Platforms/WebAssembly/WasmScripts/RadioE45Autoplay.js): true once the user has clicked or
/// typed in the page (<c>navigator.userActivation.hasBeenActive</c>), or when the probe at launch
/// (<see cref="ProbeAsync"/>) found autoplay allowed (Chrome with the site sound permission or a
/// high media engagement, Safari per its Auto-Play setting).
/// </summary>
public static partial class BrowserUserActivation
{
    private static bool _autoplayAllowed;

    public static bool CanAutoplay() => _autoplayAllowed || HasBeenActive();

    /// <summary>Asks the browser once, at launch, before the On Air page starts.</summary>
    public static async Task ProbeAsync()
    {
        try
        {
            _autoplayAllowed = await Probe();
        }
        catch (JSException)
        {
            _autoplayAllowed = false;
        }
    }

    /// <summary>Raised with the source URL when the browser refuses a play() without a gesture.</summary>
    public static event Action<string>? Blocked;

    /// <summary>Called once at launch: from then on refused plays raise <see cref="Blocked"/>.</summary>
    public static void WatchBlockedPlays() => OnBlocked(src => Blocked?.Invoke(src));

    /// <summary>Runs the action once, at the first click, tap or key press in the page.</summary>
    public static void OnFirstGesture(Action action) => OnFirstGestureJs(action);

    [JSImport("globalThis.RadioE45Autoplay.onFirstGesture")]
    private static partial void OnFirstGestureJs([JSMarshalAs<JSType.Function>] Action callback);

    [JSImport("globalThis.RadioE45Autoplay.onBlocked")]
    private static partial void OnBlocked([JSMarshalAs<JSType.Function<JSType.String>>] Action<string> callback);

    [JSImport("globalThis.RadioE45Autoplay.hasBeenActive")]
    private static partial bool HasBeenActive();

    [JSImport("globalThis.RadioE45Autoplay.probe")]
    [return: JSMarshalAs<JSType.Promise<JSType.Boolean>>]
    private static partial Task<bool> Probe();
}
#endif
