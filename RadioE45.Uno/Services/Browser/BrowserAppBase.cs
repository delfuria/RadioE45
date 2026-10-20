#if RADIOE45_BROWSER
using System.Runtime.InteropServices.JavaScript;

namespace RadioE45.Services.Browser;

/// <summary>Folder the web app is served from (Platforms/WebAssembly/WasmScripts/RadioE45Update.js).</summary>
public static partial class BrowserAppBase
{
    /// <summary>Absolute URL ending with a slash, e.g. https://delfuria.github.io/RadioE45/.</summary>
    public static Uri Get()
    {
        Uri uri = new(AppBaseJs());
        string path = uri.AbsolutePath.EndsWith('/') ? uri.AbsolutePath : uri.AbsolutePath[..(uri.AbsolutePath.LastIndexOf('/') + 1)];
        return new Uri(uri, path);
    }

    [JSImport("globalThis.RadioE45Update.appBase")]
    private static partial string AppBaseJs();
}
#endif
