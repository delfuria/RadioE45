namespace RadioE45.Models;

/// <summary>
/// Google Play reports only the Android versionCode of the latest release. Nerdbank.GitVersioning
/// builds it as <c>Minor &lt;&lt; 16 | commit height</c> while Major stays 0 (0.54.3 gives 3538947),
/// so the Minor is the high 16 bits.
/// </summary>
public static class AndroidVersionCode
{
    public static AppVersion ToAppVersion(int versionCode, int major) =>
        new(major, versionCode >> 16);
}
