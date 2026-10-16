using RadioE45.Services.Platform;

namespace RadioE45.Services.Shared;

/// <summary>
/// <see cref="IAppEnvironment"/> from the NBGV build version (version.json), shared by desktop
/// and web. HasLocalNetworkPrivacy keeps the interface default (true only when running on macOS).
/// </summary>
public sealed class UnoAppEnvironment : IAppEnvironment
{
    private static readonly Version FileVersion = Version.Parse(ThisAssembly.AssemblyFileVersion);

    /// <summary>"0.45" from version.json.</summary>
    public string VersionString => $"{FileVersion.Major}.{FileVersion.Minor}";

    /// <summary>NBGV git height (commits since the version was set).</summary>
    public string BuildString => FileVersion.Build.ToString();

    public string CommitId => ThisAssembly.GitCommitId[..7];

    public string PlatformName =>
        OperatingSystem.IsBrowser() ? "Web"
        : OperatingSystem.IsLinux() ? "Linux"
        : OperatingSystem.IsMacOS() ? "macOS"
        : OperatingSystem.IsWindows() ? "Windows"
        : "Unknown";

    // Desktop and web own the volume (slider, value persisted), like MAUI on Mac/Windows.
    public bool UsesSystemVolume => false;

    // Browsers block play() until the user has interacted with the page (roadmap §5): no radio at
    // launch on the web, but after a click (e.g. back from a podcast) the radio restarts as on
    // desktop. Read at every use, the activation comes later.
#if RADIOE45_BROWSER
    public bool CanAutoplay => Browser.BrowserUserActivation.HasBeenActive();
#else
    public bool CanAutoplay => true;
#endif
}
