using ObjCRuntime;
using UIKit;

namespace RadioE45;

public class Program
{
    static void Main(string[] args)
    {
        // Route SQLite through /usr/lib/libsqlite3.dylib instead of a bundled dylib — see the
        // SQLitePCLRaw.provider.sqlite3 comment in RadioE45.csproj for why this must happen on
        // MacCatalyst. Must run before any SQLite access, hence first line of Main().
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_sqlite3());

        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}