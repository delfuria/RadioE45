using AVFoundation;
using Foundation;
using Microsoft.Extensions.Logging;

namespace RadioE45.Services.Audio;

/// <summary>
/// iOS live player: HLS (.m3u8) through <see cref="AvStreamPlayer"/>, progressive streams
/// (Icecast MP3/AAC) through <see cref="AudioQueueStreamPlayer"/>, which CarPlay plays without
/// the ~20 s buffered-AirPlay preroll (docs/carplay-ios/carplay-mp3-start-delay.md).
/// </summary>
public sealed class IosStreamPlayer : IStreamPlayer
{
    private readonly AvStreamPlayer _hls;
    private readonly AudioQueueStreamPlayer _progressive;
    private readonly ILogger? _logger;
    private IStreamPlayer? _active;

    public IosStreamPlayer(ILogger? logger = null)
    {
        _logger = logger;
        _hls = new AvStreamPlayer(logger);
        _progressive = new AudioQueueStreamPlayer(logger);

        foreach (IStreamPlayer player in new IStreamPlayer[] { _hls, _progressive })
        {
            IStreamPlayer source = player;
            source.StateChanged += (_, e) => { if (ReferenceEquals(source, _active)) StateChanged?.Invoke(this, e); };
            source.Failed += (_, message) => { if (ReferenceEquals(source, _active)) Failed?.Invoke(this, message); };
            source.Ended += (_, _) => { if (ReferenceEquals(source, _active)) Ended?.Invoke(this, EventArgs.Empty); };
        }
    }

    public bool HasSource => _active?.HasSource ?? false;

    public event EventHandler<StreamPlayerStateChangedEventArgs>? StateChanged;
    public event EventHandler<string?>? Failed;
    public event EventHandler? Ended;

    public void Open(string url)
    {
        IStreamPlayer next = IsHls(url) ? _hls : _progressive;
        if (!ReferenceEquals(next, _active))
            _active?.Close();

        _active = next;

        AVAudioSession.SharedInstance().SetActive(true, out NSError? activationError);
        if (activationError is not null)
            _logger?.LogWarning("AudioSession activation failed: {Error}", activationError.LocalizedDescription);

        _logger?.LogInformation("IosStreamPlayer: {Engine} for {Url}", next == _hls ? "AVPlayer (HLS)" : "AudioQueue", url);
        next.Open(url);
    }

    public void Close() => _active?.Close();

    public void SetVolume(double volume)
    {
        _hls.SetVolume(volume);
        _progressive.SetVolume(volume);
    }

    public void SetMetadata(string artist, string title, string? artworkUrl)
    {
    }

    public void ClearMetadata()
    {
    }

    private static bool IsHls(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            ? uri.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)
            : url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase);
}
