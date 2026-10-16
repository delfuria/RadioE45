#if RADIOE45_BROWSER
using System.Runtime.InteropServices.JavaScript;
using RadioE45.Services.Audio;
using RadioE45.Services.Shared;

namespace RadioE45.Services.Browser;

/// <summary>
/// <see cref="IPlatformNowPlayingService"/> on the browser Media Session API
/// (Platforms/WebAssembly/WasmScripts/RadioE45MediaSession.js): title, artist and cover in the
/// browser and system media controls, play/pause from media keys and headsets, next/previous =
/// station
/// (<see cref="SystemMediaCommands"/>). The browser loads the cover itself, without CORS.
/// </summary>
public sealed partial class BrowserMediaSession : IPlatformNowPlayingService
{
    private readonly PlatformNowPlayingStateTracker _stateTracker = new();
    private readonly SystemMediaCommands _commands;
    private readonly ILogger<BrowserMediaSession> _logger;
    private readonly bool _isSupported;

    public BrowserMediaSession(SystemMediaCommands commands, ILogger<BrowserMediaSession> logger)
    {
        _commands = commands;
        _logger = logger;
        _isSupported = IsSupported();
        if (_isSupported)
            SetHandlers(OnAction);
    }

    public void UpdateMetadata(string artist, string title, string? artworkUrl, int? elapsedSeconds, int? durationSeconds, bool isPlaying)
    {
        var update = _stateTracker.UpdateMetadata(artist, title, artworkUrl, elapsedSeconds, durationSeconds, isPlaying);
        if (!_isSupported)
            return;

        var snapshot = update.Snapshot;
        SetMetadata(snapshot.Title, snapshot.Artist, snapshot.ArtworkUrl);
        SetPlaybackState(snapshot.IsPlaying ? "playing" : "paused");
    }

    public void UpdatePlaybackState(bool isPlaying)
    {
        _stateTracker.UpdatePlaybackState(isPlaying);
        if (_isSupported)
            SetPlaybackState(isPlaying ? "playing" : "paused");
    }

    public void Clear()
    {
        _stateTracker.Clear();
        if (_isSupported)
            ClearSession();
    }

    private void OnAction(string action)
    {
        var command = action switch
        {
            "play" => _commands.PlayAsync(),
            "pause" => _commands.PauseAsync(),
            "stop" => _commands.StopAsync(),
            "nexttrack" => _commands.NextStationAsync(),
            "previoustrack" => _commands.PreviousStationAsync(),
            _ => Task.CompletedTask,
        };
        command.ContinueWith(
            t => _logger.LogWarning(t.Exception, "Media Session: {Action} failed", action),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    [JSImport("globalThis.RadioE45MediaSession.isSupported")]
    private static partial bool IsSupported();

    [JSImport("globalThis.RadioE45MediaSession.setHandlers")]
    private static partial void SetHandlers([JSMarshalAs<JSType.Function<JSType.String>>] Action<string> callback);

    [JSImport("globalThis.RadioE45MediaSession.setMetadata")]
    private static partial void SetMetadata(string title, string artist, string? artworkUrl);

    [JSImport("globalThis.RadioE45MediaSession.setPlaybackState")]
    private static partial void SetPlaybackState(string state);

    [JSImport("globalThis.RadioE45MediaSession.clear")]
    private static partial void ClearSession();
}
#endif
