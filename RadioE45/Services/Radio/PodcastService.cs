using Microsoft.Extensions.Logging;
using RadioE45.Models;
using Refit;

namespace RadioE45.Services.Radio;

public class PodcastService : IPodcastService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PodcastService> _logger;

    public PodcastService(IHttpClientFactory httpClientFactory, ILogger<PodcastService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<List<AzuraCastPodcast>> GetPodcastsAsync(AzuraStation station, CancellationToken ct = default)
    {
        try
        {
            IAzuraCastPodcastApi api = CreateApi(station);
            return await api.GetPodcastsAsync(station.StationId, ct);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "API error fetching podcasts for station {StationId}", station.StationId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching podcasts for station {StationId}", station.StationId);
            throw;
        }
    }

    public async Task<List<AzuraCastPodcastEpisode>> GetEpisodesAsync(AzuraStation station, AzuraCastPodcast podcast, CancellationToken ct = default)
    {
        try
        {
            IAzuraCastPodcastApi api = CreateApi(station);
            List<AzuraCastPodcastEpisode> episodes = await api.GetEpisodesAsync(station.StationId, podcast.Id, ct);

            foreach (AzuraCastPodcastEpisode episode in episodes)
                episode.PodcastId = podcast.Id;

            // Solo episodi con un link di stream/download reale utilizzabile.
            return episodes
                .Where(e => e.HasMedia && !string.IsNullOrEmpty(e.MediaUrl))
                .OrderByDescending(e => e.PublishAtUnix)
                .ToList();
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "API error fetching episodes for podcast {PodcastId}", podcast.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching episodes for podcast {PodcastId}", podcast.Id);
            throw;
        }
    }

    private IAzuraCastPodcastApi CreateApi(AzuraStation station)
    {
        HttpClient client = _httpClientFactory.CreateClient("AzuraCast");
        client.BaseAddress = new Uri($"{UrlBaseHelper.EnsureScheme(station.UrlBase)}/api/station");
        return RestService.For<IAzuraCastPodcastApi>(client);
    }
}
