namespace RadioE45.Services.Platform;

/// <summary>App identity and platform traits that shared code used to read via #if or MAUI Essentials.</summary>
public interface IAppEnvironment
{
    /// <summary>Display version, e.g. "0.44".</summary>
    string VersionString { get; }

    /// <summary>Build number, e.g. "44".</summary>
    string BuildString { get; }

    /// <summary>Short git commit id of the build.</summary>
    string CommitId { get; }

    /// <summary>Platform name for diagnostics, e.g. "Android", "iOS", "Linux".</summary>
    string PlatformName { get; }

    /// <summary>
    /// True on phones (Android/iOS): hardware keys drive the system stream volume, so the player
    /// gain stays at full scale except when muted and no volume slider is shown or persisted.
    /// </summary>
    bool UsesSystemVolume { get; }

    /// <summary>
    /// True on Apple platforms, where the first connection to a LAN host shows the Local Network
    /// alert and the system may fail that connection before the user answers. Default
    /// implementation keeps heads that don't override it (e.g. Uno) compiling and correct.
    /// </summary>
    bool HasLocalNetworkPrivacy =>
        OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst() || OperatingSystem.IsMacOS();

    /// <summary>
    /// False where playback may start only from a user gesture (web: browser autoplay policy,
    /// docs/LINUX-UNO-ROADMAP.md §5). The app then never starts the radio on its own: at launch
    /// the station is selected and its now playing shown, but the user presses Play.
    /// </summary>
    bool CanAutoplay => true;
}
