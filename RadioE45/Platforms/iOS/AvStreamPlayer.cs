using AVFoundation;
using Foundation;

namespace RadioE45.Services.Audio;

/// <summary>
/// iOS live-stream player on a plain AVPlayer. Unlike the MediaElement it needs no page in the
/// visual tree, so playback works when the app is started by CarPlay and no main window exists.
/// Lock screen / Now Playing metadata is published by IosNowPlayingService, hence the no-op metadata.
/// </summary>
public sealed class AvStreamPlayer : IStreamPlayer
{
    private readonly AVPlayer _player = new();
    private readonly IDisposable _timeControlObserver;
    private IDisposable? _itemStatusObserver;
    private NSObject? _endObserver;
    private StreamPlayerState _state = StreamPlayerState.None;

    public AvStreamPlayer()
    {
        _timeControlObserver = _player.AddObserver("timeControlStatus", NSKeyValueObservingOptions.New, _ => OnTimeControlStatusChanged());
    }

    public bool HasSource => _player.CurrentItem is not null;

    public event EventHandler<StreamPlayerStateChangedEventArgs>? StateChanged;
    public event EventHandler<string?>? Failed;
    public event EventHandler? Ended;

    public void Open(string url)
    {
        ReleaseItem();

        NSUrl? nsUrl = NSUrl.FromString(url);
        if (nsUrl is null)
        {
            Failed?.Invoke(this, $"Invalid stream URL: {url}");
            return;
        }

        AVPlayerItem item = new(nsUrl);
        _itemStatusObserver = item.AddObserver("status", NSKeyValueObservingOptions.New, _ => OnItemStatusChanged(item));
        _endObserver = NSNotificationCenter.DefaultCenter.AddObserver(AVPlayerItem.DidPlayToEndTimeNotification, _ => Ended?.Invoke(this, EventArgs.Empty), item);

        SetState(StreamPlayerState.Opening);
        _player.ReplaceCurrentItemWithPlayerItem(item);
        _player.Play();
    }

    public void Close()
    {
        _player.Pause();
        ReleaseItem();
        SetState(StreamPlayerState.Stopped);
    }

    public void SetVolume(double volume) => _player.Volume = (float)volume;

    public void SetMetadata(string artist, string title, string? artworkUrl)
    {
    }

    public void ClearMetadata()
    {
    }

    private void ReleaseItem()
    {
        _itemStatusObserver?.Dispose();
        _itemStatusObserver = null;

        if (_endObserver is not null)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver(_endObserver);
            _endObserver = null;
        }

        _player.ReplaceCurrentItemWithPlayerItem(null);
    }

    private void OnItemStatusChanged(AVPlayerItem item)
    {
        if (item.Status == AVPlayerItemStatus.Failed)
        {
            SetState(StreamPlayerState.Failed);
            Failed?.Invoke(this, item.Error?.LocalizedDescription);
        }
    }

    private void OnTimeControlStatusChanged()
    {
        // Without an item (after Close) the player reports Paused: that is not a user-visible pause.
        if (_player.CurrentItem is null)
            return;

        StreamPlayerState next = _player.TimeControlStatus switch
        {
            AVPlayerTimeControlStatus.Playing => StreamPlayerState.Playing,
            AVPlayerTimeControlStatus.WaitingToPlayAtSpecifiedRate => StreamPlayerState.Buffering,
            _ => StreamPlayerState.Paused,
        };

        SetState(next);
    }

    private void SetState(StreamPlayerState next)
    {
        StreamPlayerState previous = _state;
        if (previous == next)
            return;

        _state = next;
        StateChanged?.Invoke(this, new StreamPlayerStateChangedEventArgs(previous, next));
    }
}
