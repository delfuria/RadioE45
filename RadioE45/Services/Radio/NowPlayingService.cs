using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Localization;
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
    private readonly ConcurrentDictionary<string, bool> _requestsProbeCache = new();
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
            string baseUrl = $"{UrlBaseHelper.EnsureScheme(station.UrlBase)}{_nowPlayingApi}";

            HttpClient client = _httpClientFactory.CreateClient("AzuraCast");
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(3);

            IAzuraCastApi api = RestService.For<IAzuraCastApi>(client);
            AzuraCastNowPlayingResponse response = await api.GetNowPlayingAsync(station.StationId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            NowPlayingInfo np = Map(response, station);
            if (response.Station.RequestsEnabled is null)
                np.RequestsEnabled = await ProbeRequestsEnabledAsync(station);
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

    // Le versioni vecchie di AzuraCast non pubblicano station.requests_enabled: si verifica
    // se l'endpoint pubblico delle richieste risponde. Solo gli esiti definitivi sono in cache.
    private async Task<bool> ProbeRequestsEnabledAsync(AzuraStation station)
    {
        string key = $"{station.UrlBase}/{station.StationId}";
        if (_requestsProbeCache.TryGetValue(key, out bool cached))
            return cached;

        try
        {
            HttpClient client = _httpClientFactory.CreateClient("AzuraCast");
            client.Timeout = TimeSpan.FromSeconds(3);
            string url = $"{UrlBaseHelper.EnsureScheme(station.UrlBase)}/api/station/{station.StationId}/requests?per_page=1";

            using HttpResponseMessage resp = await client.GetAsync(url);
            if (resp.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                return _requestsProbeCache[key] = false;
            if (!resp.IsSuccessStatusCode)
                return false;

            using JsonDocument doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            bool hasSongs = doc.RootElement.ValueKind switch
            {
                JsonValueKind.Array => doc.RootElement.GetArrayLength() > 0,
                JsonValueKind.Object when doc.RootElement.TryGetProperty("total", out JsonElement total) => total.GetInt32() > 0,
                _ => false
            };
            return _requestsProbeCache[key] = hasSongs;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Requests probe failed for station {StationId}", station.StationId);
            return false;
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
        var current = response.Live.IsLive
            ? MapLiveSong(response.NowPlaying.Song, response.Live)
            : MapSong(response.NowPlaying.Song, station);

        NextPlayingInfo? next = null;
        if (response.PlayingNext is not null)
        {
            var nextSong = MapSong(response.PlayingNext.Song, station);
            next = new NextPlayingInfo
            {
                Artist = nextSong.Artist,
                Title = nextSong.Title,
                ArtworkUrl = nextSong.ArtworkUrl,
                IsJingle = nextSong.IsJingle,
                DurationSeconds = (int)response.PlayingNext.Duration
            };
        }

        List<PlayedTrackInfo> songHistory = response.SongHistory
            .Take(5)
            .Select(h =>
            {
                var song = MapSong(h.Song, station);
                return new PlayedTrackInfo
                {
                    Artist = song.Artist,
                    Title = song.Title,
                    ArtworkUrl = song.ArtworkUrl,
                    IsJingle = song.IsJingle,
                    PlayedAt = DateTimeOffset.FromUnixTimeSeconds(h.PlayedAt).UtcDateTime
                };
            })
            .ToList();

        return new NowPlayingInfo
        {
            Artist = current.Artist,
            Title = current.Title,
            ArtworkUrl = current.ArtworkUrl,
            IsJingle = current.IsJingle,
            IsLive = response.Live.IsLive,
            RequestsEnabled = response.Station.RequestsEnabled ?? false,
            StreamerName = response.Live.StreamerName,
            ListenerCount = response.Listeners.Current,
            TrackDurationSeconds = response.NowPlaying.Duration,
            TrackElapsedSeconds = response.NowPlaying.Elapsed,
            LastUpdated = DateTime.UtcNow,
            Next = next,
            SongHistory = songHistory
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

    // Durante una diretta il titolo del brano è il metadato inviato dal software del DJ, che spesso
    // contiene solo lo username dell'account streamer: mostriamo invece il nome visualizzato del DJ.
    // Se il DJ invia metadati completi "Artista - Titolo" li teniamo come sottotitolo.
    private static (string Artist, string Title, string? ArtworkUrl, bool IsJingle) MapLiveSong(SongInfo song, LiveInfo live)
    {
        string title = string.IsNullOrWhiteSpace(live.StreamerName) ? song.Title : live.StreamerName;
        string artist = !string.IsNullOrWhiteSpace(song.Artist)
            ? song.Text
            : LocalizationResourceManager.Instance["OnAir_LiveBroadcast"];
        string? art = !string.IsNullOrWhiteSpace(live.ArtUrl) ? live.ArtUrl
            : string.IsNullOrWhiteSpace(song.ArtUrl) ? null : song.ArtUrl;
        return (artist, title, art, false);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
