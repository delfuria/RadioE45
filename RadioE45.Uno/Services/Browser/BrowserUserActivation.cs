#if RADIOE45_BROWSER
using System.Runtime.InteropServices.JavaScript;

namespace RadioE45.Services.Browser;

/// <summary>
/// <c>navigator.userActivation.hasBeenActive</c> (Platforms/WebAssembly/WasmScripts/RadioE45Autoplay.js):
/// true once the user has clicked or typed in the page, after which the browser lets the app
/// start audio on its own. False where the API is missing.
/// </summary>
public static partial class BrowserUserActivation
{
    [JSImport("globalThis.RadioE45Autoplay.hasBeenActive")]
    public static partial bool HasBeenActive();
}
#endif
