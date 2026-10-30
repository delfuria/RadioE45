using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RadioE45.Services.Audio;
using RadioE45.Services.Data;
using RadioE45.Services.Legal;
using RadioE45.Services.Platform;
using RadioE45.Services.Radio;
using RadioE45.Services.Updates;

namespace RadioE45.DependencyInjection;

public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the platform-independent services shared by every app head. Each head still
    /// registers its own IDatabaseService, audio engine, now-playing integration and ViewModels.
    /// </summary>
    public static IServiceCollection AddRadioE45Core(this IServiceCollection services)
    {
        // HTTP client for AzuraCast (base URL set dynamically per-station)
        services.AddHttpClient("AzuraCast")
            .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(3));

        // Update lookups (App Store, GitHub Releases): silent on failure, so a short timeout.
        services.AddHttpClient(AppleLookupUpdateSource.HttpClientName)
            .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));

        services.AddSingleton<ITermsService, TermsService>();
        services.AddSingleton<IStreamUrlProber, StreamUrlProber>();
        services.AddSingleton<IStreamInfoProvider, StreamInfoProvider>();
        services.AddSingleton<RemoteArtworkLoader>();

        // Heads register the source of their distribution channel; without one there is no update check.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAppUpdateSource, NullUpdateSource>();
        services.AddSingleton<AppUpdateCoordinator>();
        services.AddSingleton<AppUpdatePrompter>();

        services.AddSingleton<INowPlayingService, NowPlayingService>();
        services.AddSingleton<IStationDetailService, StationDetailService>();
        services.AddTransient<IStationListService, StationListService>();
        services.AddTransient<IScheduleService, ScheduleService>();
        services.AddTransient<IPodcastService, PodcastService>();
        services.AddTransient<ISongRequestService, SongRequestService>();

        services.AddSingleton<IRadioRepository, RadioRepository>();
        services.AddSingleton<IDbVersionRepository, DbVersionRepository>();
        services.AddSingleton<IAppSettingsRepository, AppSettingsRepository>();
        services.AddSingleton<ILogRepository, LogRepository>();
        services.AddSingleton<IPodcastProgressRepository, PodcastProgressRepository>();

        return services;
    }
}
