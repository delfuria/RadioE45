using System.Runtime.InteropServices;
using Uno.UI.Hosting;

namespace RadioE45;

internal class Program
{
    [DllImport("libdl.so.2", EntryPoint = "dlopen")]
    private static extern IntPtr DlOpen(string fileName, int flags);

    [STAThread]
    public static void Main(string[] args)
    {
        if (OperatingSystem.IsLinux())
            ExposeSystemFreeType();

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseX11()
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();

        host.Run();
    }

    /// <summary>
    /// libSkiaSharp.so (NoDependencies) leaves one FreeType symbol unresolved, FT_Get_BDF_Property,
    /// which Skia calls for bitmap fonts (BDF/PCF). Where such fonts are installed (xfonts-base comes
    /// with Xfce and many X11 setups) the app dies at startup with "symbol lookup error". Loading the
    /// system FreeType into the global symbol scope before Skia is used lets the call resolve.
    /// </summary>
    private static void ExposeSystemFreeType()
    {
        const int RtldNow = 0x2;
        const int RtldGlobal = 0x100;
        try
        {
            _ = DlOpen("libfreetype.so.6", RtldNow | RtldGlobal);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // No libdl or FreeType: nothing to do, the app behaves as before.
        }
    }
}
