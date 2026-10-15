using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;

namespace RadioE45.Services.Audio;

/// <summary>Adapts the podcast MediaElement hosted by PodcastEpisodesPage to <see cref="IPodcastMediaPlayer"/>.</summary>
public sealed class MediaElementPodcastPlayer : IPodcastMediaPlayer
{
    private readonly MediaElement _mediaElement;

    public MediaElementPodcastPlayer(MediaElement mediaElement)
    {
        _mediaElement = mediaElement;
        _mediaElement.ShouldAutoPlay = false;
        _mediaElement.ShouldLoopPlayback = false;
        _mediaElement.ShouldShowPlaybackControls = false;

        _mediaElement.StateChanged += OnStateChanged;
        _mediaElement.MediaEnded += OnMediaEnded;
        _mediaElement.PositionChanged += OnPositionChanged;
        _mediaElement.MediaFailed += OnMediaFailed;
    }

    public StreamPlayerState State => Map(_mediaElement.CurrentState);
    public TimeSpan Position => _mediaElement.Position;
    public TimeSpan Duration => _mediaElement.Duration;

    public double Volume
    {
        get => _mediaElement.Volume;
        set => _mediaElement.Volume = value;
    }

    public event EventHandler<StreamPlayerStateChangedEventArgs>? StateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<string?>? Failed;
    public event EventHandler? Ended;

    public void Open(string url)
    {
        _mediaElement.ShouldAutoPlay = true;
        _mediaElement.Source = MediaSource.FromUri(url);
        _mediaElement.Play();
    }

    public void Play() => _mediaElement.Play();

    public void Pause() => _mediaElement.Pause();

    public void Close()
    {
        _mediaElement.Stop();
        _mediaElement.Source = null;
    }

    public Task SeekAsync(TimeSpan position) => _mediaElement.SeekTo(position, CancellationToken.None);

    /// <summary>Stops forwarding events once the service has moved to a newer element.</summary>
    public void Detach()
    {
        _mediaElement.StateChanged -= OnStateChanged;
        _mediaElement.MediaEnded -= OnMediaEnded;
        _mediaElement.PositionChanged -= OnPositionChanged;
        _mediaElement.MediaFailed -= OnMediaFailed;
    }

    private void OnStateChanged(object? sender, MediaStateChangedEventArgs e) =>
        StateChanged?.Invoke(this, new StreamPlayerStateChangedEventArgs(Map(e.PreviousState), Map(e.NewState)));

    private void OnPositionChanged(object? sender, MediaPositionChangedEventArgs e) => PositionChanged?.Invoke(this, e.Position);

    private void OnMediaFailed(object? sender, MediaFailedEventArgs e) => Failed?.Invoke(this, e.ErrorMessage);

    private void OnMediaEnded(object? sender, EventArgs e) => Ended?.Invoke(this, EventArgs.Empty);

    private static StreamPlayerState Map(MediaElementState state) => state switch
    {
        MediaElementState.Opening => StreamPlayerState.Opening,
        MediaElementState.Buffering => StreamPlayerState.Buffering,
        MediaElementState.Playing => StreamPlayerState.Playing,
        MediaElementState.Paused => StreamPlayerState.Paused,
        MediaElementState.Stopped => StreamPlayerState.Stopped,
        MediaElementState.Failed => StreamPlayerState.Failed,
        _ => StreamPlayerState.None,
    };
}
