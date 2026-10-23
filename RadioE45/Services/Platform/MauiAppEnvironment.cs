namespace RadioE45.Services.Platform;

public sealed class MauiAppEnvironment : IAppEnvironment
{
    public string VersionString => AppInfo.VersionString;

    public string BuildString => AppInfo.BuildString;

    public string CommitId => ThisAssembly.GitCommitId[..7];

    // No settings screen on TV: the favorite station always starts (the setting would be unreachable).
    public bool AlwaysStartsWithFavorite => DeviceInfo.Current.Idiom == DeviceIdiom.TV;

    public string PlatformName => DeviceInfo.Current.Platform.ToString();

#if ANDROID || IOS
    public bool UsesSystemVolume => true;
#else
    public bool UsesSystemVolume => false;
#endif

#if IOS || MACCATALYST
    public bool HasLocalNetworkPrivacy => true;
#else
    public bool HasLocalNetworkPrivacy => false;
#endif
}
