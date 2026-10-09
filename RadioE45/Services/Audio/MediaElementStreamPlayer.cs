using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;

namespace RadioE45.Services.Audio;

/// <summary>Adapts the CommunityToolkit MediaElement hosted by OnAirPage to <see cref="IStreamPlayer"/>.</summary>
public sealed class MediaElementStreamPlayer : IStreamPlayer
{
    private readonly MediaElement _mediaElement;

    public MediaElementStreamPlayer(MediaElement mediaElement)
    {
        _mediaElement = mediaElement;
        _mediaElement.ShouldAutoPlay = false;
        _mediaElement.ShouldLoopPlayback = false;
        _mediaElement.ShouldShowPlaybackControls = false;

        _mediaElement.StateChanged += OnStateChanged;
        _mediaElement.MediaFailed += OnMediaFailed;
        _mediaElement.MediaEnded += OnMediaEnded;
    }

    public bool HasSource => _mediaElement.Source is not null;

    public event EventHandler<StreamPlayerStateChangedEventArgs>? StateChanged;
    public event EventHandler<string?>? Failed;
    public event EventHandler? Ended;

    public void Open(string url)
    {
        _mediaElement.ShouldAutoPlay = true;
        _mediaElement.Source = MediaSource.FromUri(url);
    }

    public void Close()
    {
        _mediaElement.Stop();
        _mediaElement.Source = null;
    }

    public void SetVolume(double volume) => _mediaElement.Volume = volume;

    public void SetMetadata(string artist, string title, string? artworkUrl)
    {
        // Guard: avoid setting unchanged values — on Windows, re-setting MetadataArtworkUrl
        // via the CommunityToolkit handler causes a brief Playing→Buffering→Playing cycle
        // even when the URL is identical, because the underlying MediaPlayer reacts to the
        // property change regardless of value equality.
        if (_mediaElement.MetadataTitle != title)
            _mediaElement.MetadataTitle = title;
        if (_mediaElement.MetadataArtist != artist)
            _mediaElement.MetadataArtist = artist;
        string newArtworkUrl = artworkUrl ?? string.Empty;
        if (_mediaElement.MetadataArtworkUrl != newArtworkUrl)
            _mediaElement.MetadataArtworkUrl = newArtworkUrl;
    }

    public void ClearMetadata()
    {
        _mediaElement.MetadataTitle = string.Empty;
        _mediaElement.MetadataArtist = string.Empty;
        _mediaElement.MetadataArtworkUrl = string.Empty;
    }

    /// <summary>Stops forwarding events once the service has moved to a newer element.</summary>
    public void Detach()
    {
        _mediaElement.StateChanged -= OnStateChanged;
        _mediaElement.MediaFailed -= OnMediaFailed;
        _mediaElement.MediaEnded -= OnMediaEnded;
    }

    private void OnStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        // Autoplay only for the open that just happened: a later Play/Pause on the element
        // (e.g. from system controls) must not restart by itself.
        if (e.NewState == MediaElementState.Playing && _mediaElement.ShouldAutoPlay)
            _mediaElement.ShouldAutoPlay = false;

        StateChanged?.Invoke(this, new StreamPlayerStateChangedEventArgs(Map(e.PreviousState), Map(e.NewState)));
    }

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
