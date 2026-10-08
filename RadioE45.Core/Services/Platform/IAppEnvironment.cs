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
}
