#if RADIOE45_BROWSER
using System.Runtime.InteropServices.JavaScript;
using RadioE45.Services.Audio;

namespace RadioE45.Services.Browser;

/// <summary>
/// <see cref="IPodcastMediaPlayer"/> on a plain <c>&lt;audio&gt;</c> element
/// (Platforms/WebAssembly/WasmScripts/RadioE45PodcastAudio.js). Uno's browser
/// <c>MediaPlayer</c> plays through a <c>&lt;video&gt;</c> element, and Safari ends it right
/// after "playing" (duration ~0 s) when the file is an audio-only MP3: podcast episodes would
/// never start there.
/// - MediaPlayer has no position event on the Uno heads: the position is sampled every 500 ms
///   while playing, like <see cref="Shared.MediaPlayerPodcastPlayer"/>.
/// - States are debounced like the live player (<see cref="StreamStateDebouncer"/>).
/// </summary>
public sealed partial class BrowserPodcastPlayer : IPodcastMediaPlayer, IDisposable
{
    private static readonly TimeSpan StateDebounce = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan PositionInterval = TimeSpan.FromMilliseconds(500);

    private readonly StreamStateDebouncer _states;
    private readonly Timer _positionTimer;
    private readonly ILogger _logger;
    private double _volume = 1;
    private volatile bool _rawPlaying;
    private volatile bool _open;

    public BrowserPodcastPlayer(ILogger logger)
    {
        _logger = logger;
        _states = new StreamStateDebouncer(StateDebounce, TimeProvider.System,
            (previous, current) => StateChanged?.Invoke(this, new StreamPlayerStateChangedEventArgs(previous, current)));
        _positionTimer = new Timer(_ => SamplePosition(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        Init(OnEvent);
    }

    public StreamPlayerState State => _states.ReportedState;
    public TimeSpan Position => _open ? TimeSpan.FromSeconds(GetPosition()) : TimeSpan.Zero;
    public TimeSpan Duration => _open ? TimeSpan.FromSeconds(GetDuration()) : TimeSpan.Zero;

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = value;
            if (_open)
                SetVolume(value);
        }
    }

    public event EventHandler<StreamPlayerStateChangedEventArgs>? StateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<string?>? Failed;
    public event EventHandler? Ended;

    public void Open(string url)
    {
        _open = true;
        SetSampling(false);
        _states.Push(StreamPlayerState.Opening);
        OpenAudio(url, _volume);
    }

    public void Play()
    {
        if (_open)
            PlayAudio();
    }

    public void Pause()
    {
        if (_open)
            PauseAudio();
    }

    public void Close()
    {
        _open = false;
        SetSampling(false);
        CloseAudio();
        _states.ReportNow(StreamPlayerState.Stopped);
    }

    public async Task SeekAsync(TimeSpan position)
    {
        if (!_open)
            return;

        try
        {
            await Seek(position.TotalSeconds);
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "Podcast seek to {Position} failed", position);
        }
    }

    public void Dispose()
    {
        Close();
        _positionTimer.Dispose();
        _states.Dispose();
    }

    private void OnEvent(string name, double value)
    {
        switch (name)
        {
            case "opening":
                _states.Push(StreamPlayerState.Opening);
                break;
            case "buffering":
                SetSampling(false);
                _states.Push(StreamPlayerState.Buffering);
                break;
            case "playing":
                SetSampling(true);
                _states.Push(StreamPlayerState.Playing);
                break;
            case "paused":
                SetSampling(false);
                _states.Push(StreamPlayerState.Paused);
                break;
            case "ended":
                SetSampling(false);
                Ended?.Invoke(this, EventArgs.Empty);
                break;
            case "error":
                SetSampling(false);
                var message = ErrorMessage((int)value);
                _logger.LogWarning("Podcast audio error {Code}: {Message}", (int)value, message);
                _states.ReportNow(StreamPlayerState.Failed);
                Failed?.Invoke(this, message);
                break;
        }
    }

    private static string ErrorMessage(int code) => code switch
    {
        -1 => "Playback blocked by the browser",
        1 => "Playback aborted",
        2 => "Network error while loading the episode",
        3 => "The episode could not be decoded",
        4 => "The episode format is not supported",
        _ => "Playback error",
    };

    private void SetSampling(bool playing)
    {
        if (_rawPlaying == playing)
            return;
        _rawPlaying = playing;
        _positionTimer.Change(playing ? PositionInterval : Timeout.InfiniteTimeSpan, PositionInterval);
    }

    private void SamplePosition()
    {
        if (_rawPlaying && _open)
            PositionChanged?.Invoke(this, TimeSpan.FromSeconds(GetPosition()));
    }

    [JSImport("globalThis.RadioE45PodcastAudio.init")]
    private static partial void Init([JSMarshalAs<JSType.Function<JSType.String, JSType.Number>>] Action<string, double> onEvent);

    [JSImport("globalThis.RadioE45PodcastAudio.open")]
    private static partial void OpenAudio(string url, double volume);

    [JSImport("globalThis.RadioE45PodcastAudio.play")]
    private static partial void PlayAudio();

    [JSImport("globalThis.RadioE45PodcastAudio.pause")]
    private static partial void PauseAudio();

    [JSImport("globalThis.RadioE45PodcastAudio.close")]
    private static partial void CloseAudio();

    [JSImport("globalThis.RadioE45PodcastAudio.setVolume")]
    private static partial void SetVolume(double volume);

    [JSImport("globalThis.RadioE45PodcastAudio.position")]
    private static partial double GetPosition();

    [JSImport("globalThis.RadioE45PodcastAudio.duration")]
    private static partial double GetDuration();

    [JSImport("globalThis.RadioE45PodcastAudio.seek")]
    [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
    private static partial Task Seek(double seconds);
}
#endif
