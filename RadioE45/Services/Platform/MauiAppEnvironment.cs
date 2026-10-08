namespace RadioE45.Services.Platform;

public sealed class MauiAppEnvironment : IAppEnvironment
{
    public string VersionString => AppInfo.VersionString;

    public string BuildString => AppInfo.BuildString;

    public string CommitId => ThisAssembly.GitCommitId[..7];

    public string PlatformName => DeviceInfo.Current.Platform.ToString();

#if ANDROID || IOS
    public bool UsesSystemVolume => true;
#else
    public bool UsesSystemVolume => false;
#endif
}
