using Microsoft.Extensions.Logging;
using RadioE45.Models;
using Refit;

namespace RadioE45.Services.Radio;

public class NowPlayingService : INowPlayingService, IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NowPlayingService> _logger;
    private NowPlayingInfo _current = NowPlayingInfo.Empty;
    private CancellationTokenSource? _cts;
    private Task? _pollingTask;
    private volatile bool _isPaused;
    private readonly string _nowPlayingApi = "/api/nowplaying";
    public NowPlayingInfo Current => _current;

    public event EventHandler<NowPlayingInfo>? NowPlayingUpdated;

    public NowPlayingService(IHttpClientFactory httpClientFactory, ILogger<NowPlayingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task StartPollingAsync(AzuraStation station, CancellationToken ct = default)
    {
        await StopPollingAsync();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        CancellationToken token = _cts.Token;

        _pollingTask = Task.Run(async () =>
        {
            NowPlayingInfo initial = await FetchOnceAsync(station);
            NotifyIfChanged(initial);

            using PeriodicTimer timer = new(TimeSpan.FromSeconds(10));
            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    if (_isPaused)
                        continue;

                    NowPlayingInfo info = await FetchOnceAsync(station);
                    NotifyIfChanged(info);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation — no action needed
            }
        }, token);
    }

    public async Task StopPollingAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            if (_pollingTask is not null)
            {
                try { await _pollingTask; }
                catch (OperationCanceledException) { }
            }
            _cts.Dispose();
            _cts = null;
        }
        _pollingTask = null;
    }

    public void PausePolling() => _isPaused = true;

    public void ResumePolling() => _isPaused = false;

    public async Task<NowPlayingInfo> FetchOnceAsync(AzuraStation station)
    {
        try
        {
            string baseUrl = $"https://{station.UrlBase}{_nowPlayingApi}";

            HttpClient client = _httpClientFactory.CreateClient("AzuraCast");
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(3);

            IAzuraCastApi api = RestService.For<IAzuraCastApi>(client);
            AzuraCastNowPlayingResponse response = await api.GetNowPlayingAsync(station.StationId);
            NowPlayingInfo np = Map(response, station);
            _logger.LogInformation(
                "▶ {Artist} - {Title} |  {Elapsed}/{Duration}s  |  👥 {Listeners}  |  Next: {NextArtist} - {NextTitle}",
                np.Artist, np.Title,
                np.TrackElapsedSeconds, np.TrackDurationSeconds,
                np.ListenerCount,
                np.Next?.Artist ?? "—", np.Next?.Title ?? "—");

            return np;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching now playing");
            return _current;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Timeout fetching now playing");
            return _current;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching now playing");
            return _current;
        }
    }

    private void NotifyIfChanged(NowPlayingInfo info)
    {
        bool changed = !info.Equals(_current);
        _current = info;

        if (changed)
            NowPlayingUpdated?.Invoke(this, info);
    }

    private static NowPlayingInfo Map(AzuraCastNowPlayingResponse response, AzuraStation station)
    {
        var current = MapSong(response.NowPlaying.Song, station);

        NextPlayingInfo? next = null;
        if (response.PlayingNext is not null)
        {
            var nextSong = MapSong(response.PlayingNext.Song, station);
            next = new NextPlayingInfo
            {
                Artist = nextSong.Artist,
                Title = nextSong.Title,
                ArtworkUrl = nextSong.ArtworkUrl,
                IsJingle = nextSong.IsJingle
            };
        }

        return new NowPlayingInfo
        {
            Artist = current.Artist,
            Title = current.Title,
            ArtworkUrl = current.ArtworkUrl,
            IsJingle = current.IsJingle,
            IsLive = response.Live.IsLive,
            StreamerName = response.Live.StreamerName,
            ListenerCount = response.Listeners.Current,
            TrackDurationSeconds = response.NowPlaying.Duration,
            TrackElapsedSeconds = response.NowPlaying.Elapsed,
            LastUpdated = DateTime.UtcNow,
            Next = next
        };
    }

    // Jingles report the station's own name/logo instead of a real track.
    private static (string Artist, string Title, string? ArtworkUrl, bool IsJingle) MapSong(SongInfo song, AzuraStation station)
    {
        bool isJingle = song.Title.Contains("jingle", StringComparison.OrdinalIgnoreCase);
        if (!isJingle)
            return (song.Artist, song.Title, string.IsNullOrWhiteSpace(song.ArtUrl) ? null : song.ArtUrl, false);

        string? logo = string.IsNullOrWhiteSpace(station.LogoUrl) ? null : station.LogoUrl;
        return (station.Description, station.Name, logo, true);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
