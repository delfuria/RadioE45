#if RADIOE45_DESKTOP
using System.Text;
using RadioE45.Services.Audio;
using RadioE45.Services.Platform;
using RadioE45.Services.Shared;
using Tmds.DBus.Protocol;

namespace RadioE45.Services.Desktop;

/// <summary>
/// <see cref="IPlatformNowPlayingService"/> on MPRIS (org.mpris.MediaPlayer2 on the D-Bus session
/// bus): title, artist and cover in the GNOME/KDE media panel, and play/pause from media keys,
/// headsets and the panel itself, next/previous = station (<see cref="SystemMediaCommands"/>). Linux only: on macOS and
/// Windows there is no session bus and the service does nothing, like NullPlatformNowPlayingService.
/// The cover is downloaded to a local file, which every MPRIS client can show.
/// </summary>
public sealed class MprisNowPlayingService : IPlatformNowPlayingService, IPathMethodHandler, IDisposable
{
    private const string BusName = "org.mpris.MediaPlayer2.radioe45";
    private const string ObjectPath = "/org/mpris/MediaPlayer2";
    private const string RootInterface = "org.mpris.MediaPlayer2";
    private const string PlayerInterface = "org.mpris.MediaPlayer2.Player";
    private const string PropertiesInterface = "org.freedesktop.DBus.Properties";

    private static readonly ReadOnlyMemory<byte> RootXml = Encoding.UTF8.GetBytes("""
        <interface name="org.mpris.MediaPlayer2">
          <method name="Raise"/>
          <method name="Quit"/>
          <property name="CanQuit" type="b" access="read"/>
          <property name="CanRaise" type="b" access="read"/>
          <property name="HasTrackList" type="b" access="read"/>
          <property name="Identity" type="s" access="read"/>
          <property name="SupportedUriSchemes" type="as" access="read"/>
          <property name="SupportedMimeTypes" type="as" access="read"/>
        </interface>
        """);

    private static readonly ReadOnlyMemory<byte> PlayerXml = Encoding.UTF8.GetBytes("""
        <interface name="org.mpris.MediaPlayer2.Player">
          <method name="Next"/>
          <method name="Previous"/>
          <method name="Pause"/>
          <method name="PlayPause"/>
          <method name="Stop"/>
          <method name="Play"/>
          <method name="Seek"><arg name="Offset" type="x" direction="in"/></method>
          <method name="SetPosition"><arg name="TrackId" type="o" direction="in"/><arg name="Position" type="x" direction="in"/></method>
          <method name="OpenUri"><arg name="Uri" type="s" direction="in"/></method>
          <signal name="Seeked"><arg name="Position" type="x"/></signal>
          <property name="PlaybackStatus" type="s" access="read"/>
          <property name="Rate" type="d" access="readwrite"/>
          <property name="Metadata" type="a{sv}" access="read"/>
          <property name="Volume" type="d" access="readwrite"/>
          <property name="Position" type="x" access="read"/>
          <property name="MinimumRate" type="d" access="read"/>
          <property name="MaximumRate" type="d" access="read"/>
          <property name="CanGoNext" type="b" access="read"/>
          <property name="CanGoPrevious" type="b" access="read"/>
          <property name="CanPlay" type="b" access="read"/>
          <property name="CanPause" type="b" access="read"/>
          <property name="CanSeek" type="b" access="read"/>
          <property name="CanControl" type="b" access="read"/>
        </interface>
        """);

    private readonly PlatformNowPlayingStateTracker _stateTracker = new();
    private readonly SystemMediaCommands _commands;
    private readonly RemoteArtworkLoader _artworkLoader;
    private readonly IAppPaths _paths;
    private readonly ILogger<MprisNowPlayingService> _logger;
    private readonly object _gate = new();
    private DBusConnection? _connection;
    private string? _artworkFile;
    private int _trackNumber;
    private bool _hasTrack;

    public MprisNowPlayingService(
        SystemMediaCommands commands,
        RemoteArtworkLoader artworkLoader,
        IAppPaths paths,
        ILogger<MprisNowPlayingService> logger)
    {
        _commands = commands;
        _artworkLoader = artworkLoader;
        _paths = paths;
        _logger = logger;

        if (OperatingSystem.IsLinux() && DBusAddress.Session is not null)
            _ = ConnectAsync();
    }

    public string Path => ObjectPath;

    public bool HandlesChildPaths => false;

    public void UpdateMetadata(string artist, string title, string? artworkUrl, int? elapsedSeconds, int? durationSeconds, bool isPlaying)
    {
        var previous = _stateTracker.GetSnapshot();
        var update = _stateTracker.UpdateMetadata(artist, title, artworkUrl, elapsedSeconds, durationSeconds, isPlaying);

        lock (_gate)
        {
            if (previous.Title != title || previous.Artist != artist || !_hasTrack)
                _trackNumber++;
            _hasTrack = true;
            if (update.ArtworkVersion > 0)
                _artworkFile = null;
        }

        EmitPlayerChanged(metadata: true);

        if (update.ArtworkUrlToLoad is { } url)
            _ = LoadArtworkAsync(url, update.ArtworkVersion);
    }

    public void UpdatePlaybackState(bool isPlaying)
    {
        _stateTracker.UpdatePlaybackState(isPlaying);
        EmitPlayerChanged(metadata: false);
    }

    public void Clear()
    {
        _stateTracker.Clear();
        lock (_gate)
        {
            _hasTrack = false;
            _artworkFile = null;
        }

        EmitPlayerChanged(metadata: true);
    }

    public void Dispose() => _connection?.Dispose();

    private async Task ConnectAsync()
    {
        try
        {
            var connection = new DBusConnection(DBusAddress.Session!);
            await connection.ConnectAsync();
            connection.AddMethodHandler(this);
            await connection.RequestNameAsync(BusName);
            _connection = connection;
            _logger.LogInformation("MPRIS: registered {BusName}", BusName);
        }
        catch (Exception ex)
        {
            // No session bus (e.g. a bare X11 session): the app works without media controls.
            _logger.LogWarning(ex, "MPRIS: session bus not available");
        }
    }

    private async Task LoadArtworkAsync(string url, int version)
    {
        try
        {
            var bytes = await _artworkLoader.LoadAsync(url);
            if (!_stateTracker.IsArtworkCurrent(url, version))
                return;

            // A new name for every cover: clients cache the image by its URL.
            var folder = System.IO.Path.Combine(_paths.AppDataDirectory, "mpris");
            Directory.CreateDirectory(folder);
            foreach (var old in Directory.EnumerateFiles(folder))
                File.Delete(old);
            var file = System.IO.Path.Combine(folder, $"cover-{version}");
            await File.WriteAllBytesAsync(file, bytes);

            lock (_gate)
                _artworkFile = file;
            EmitPlayerChanged(metadata: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MPRIS: cover not available from {Url}", url);
        }
    }

    public ValueTask HandleMethodAsync(MethodContext context)
    {
        var request = context.Request;
        var member = request.MemberAsString ?? string.Empty;

        if (context.IsDBusIntrospectRequest)
        {
            context.ReplyIntrospectXml([RootXml, PlayerXml]);
            return ValueTask.CompletedTask;
        }

        switch (request.InterfaceAsString)
        {
            case PropertiesInterface:
                HandleProperties(context, member);
                break;

            case RootInterface when member is "Raise" or "Quit":
                ReplyEmpty(context);
                break;

            case PlayerInterface:
                Task? command = member switch
                {
                    "Play" => _commands.PlayAsync(),
                    "Pause" => _commands.PauseAsync(),
                    "PlayPause" => _commands.TogglePlayPauseAsync(),
                    "Stop" => _commands.StopAsync(),
                    "Next" => _commands.NextStationAsync(),
                    "Previous" => _commands.PreviousStationAsync(),
                    // Live radio: no seek or URI playback (CanSeek is false).
                    "Seek" or "SetPosition" or "OpenUri" => Task.CompletedTask,
                    _ => null,
                };
                if (command is null)
                {
                    context.ReplyUnknownMethodError();
                    break;
                }

                command.ContinueWith(
                    t => _logger.LogWarning(t.Exception, "MPRIS: {Member} failed", member),
                    TaskContinuationOptions.OnlyOnFaulted);
                ReplyEmpty(context);
                break;

            default:
                context.ReplyUnknownMethodError();
                break;
        }

        return ValueTask.CompletedTask;
    }

    private void HandleProperties(MethodContext context, string member)
    {
        var reader = context.Request.GetBodyReader();
        switch (member)
        {
            case "Get":
            {
                var iface = reader.ReadString();
                var name = reader.ReadString();
                if (!GetProperties(iface).TryGetValue(name, out var value))
                {
                    context.ReplyError("org.freedesktop.DBus.Error.UnknownProperty", $"Unknown property {iface}.{name}");
                    return;
                }

                using var writer = context.CreateReplyWriter("v");
                writer.WriteVariant(value);
                context.Reply(writer.CreateMessage());
                return;
            }

            case "GetAll":
            {
                var iface = reader.ReadString();
                using var writer = context.CreateReplyWriter("a{sv}");
                writer.WriteDictionary(GetProperties(iface));
                context.Reply(writer.CreateMessage());
                return;
            }

            case "Set":
                // Rate and Volume are fixed: the app volume stays in the app.
                ReplyEmpty(context);
                return;

            default:
                context.ReplyUnknownMethodError();
                return;
        }
    }

    private static void ReplyEmpty(MethodContext context)
    {
        if (context.NoReplyExpected)
            return;
        using var writer = context.CreateReplyWriter(null!);
        context.Reply(writer.CreateMessage());
    }

    private Dictionary<string, VariantValue> GetProperties(string iface) => iface switch
    {
        RootInterface => new()
        {
            ["CanQuit"] = VariantValue.Bool(false),
            ["CanRaise"] = VariantValue.Bool(false),
            ["HasTrackList"] = VariantValue.Bool(false),
            ["Identity"] = VariantValue.String("Radio E45"),
            ["SupportedUriSchemes"] = VariantValue.Array(Array.Empty<string>()),
            ["SupportedMimeTypes"] = VariantValue.Array(Array.Empty<string>()),
        },
        PlayerInterface => GetPlayerProperties(metadata: true),
        _ => [],
    };

    private Dictionary<string, VariantValue> GetPlayerProperties(bool metadata)
    {
        var snapshot = _stateTracker.GetSnapshot();
        string? artworkFile;
        bool hasTrack;
        int trackNumber;
        lock (_gate)
        {
            artworkFile = _artworkFile;
            hasTrack = _hasTrack;
            trackNumber = _trackNumber;
        }

        var properties = new Dictionary<string, VariantValue>
        {
            ["PlaybackStatus"] = VariantValue.String(!hasTrack ? "Stopped" : snapshot.IsPlaying ? "Playing" : "Paused"),
            ["CanPlay"] = VariantValue.Bool(hasTrack && _commands.CanPlay),
            ["CanPause"] = VariantValue.Bool(hasTrack),
            ["CanControl"] = VariantValue.Bool(true),
            ["CanSeek"] = VariantValue.Bool(false),
            ["CanGoNext"] = VariantValue.Bool(_commands.CanChangeStation),
            ["CanGoPrevious"] = VariantValue.Bool(_commands.CanChangeStation),
            ["Rate"] = VariantValue.Double(1.0),
            ["MinimumRate"] = VariantValue.Double(1.0),
            ["MaximumRate"] = VariantValue.Double(1.0),
            ["Volume"] = VariantValue.Double(1.0),
            ["Position"] = VariantValue.Int64((snapshot.ElapsedSeconds ?? 0) * 1_000_000L),
        };

        if (metadata)
        {
            var values = new Dictionary<string, VariantValue>();
            if (hasTrack)
            {
                values["mpris:trackid"] = VariantValue.ObjectPath(new ObjectPath($"/com/radioe45/track/{trackNumber}"));
                values["xesam:title"] = VariantValue.String(snapshot.Title);
                values["xesam:artist"] = VariantValue.Array(new[] { snapshot.Artist });
                if (snapshot.DurationSeconds is int duration and > 0)
                    values["mpris:length"] = VariantValue.Int64(duration * 1_000_000L);
                if (artworkFile is not null)
                    values["mpris:artUrl"] = VariantValue.String(new Uri(artworkFile).AbsoluteUri);
            }
            else
            {
                values["mpris:trackid"] = VariantValue.ObjectPath(new ObjectPath("/org/mpris/MediaPlayer2/TrackList/NoTrack"));
            }

            properties["Metadata"] = new Dict<string, VariantValue>(values).AsVariantValue();
        }

        return properties;
    }

    private void EmitPlayerChanged(bool metadata)
    {
        var connection = _connection;
        if (connection is null)
            return;

        var changed = GetPlayerProperties(metadata);
        if (!metadata)
        {
            // Only what a play/pause changes.
            changed = changed
                .Where(p => p.Key is "PlaybackStatus" or "CanPlay" or "CanPause" or "CanGoNext" or "CanGoPrevious")
                .ToDictionary();
        }

        try
        {
            using var writer = connection.GetMessageWriter();
            writer.WriteSignalHeader(null, ObjectPath, PropertiesInterface, "PropertiesChanged", "sa{sv}as");
            writer.WriteString(PlayerInterface);
            writer.WriteDictionary(changed);
            writer.WriteArray(Array.Empty<string>());
            connection.TrySendMessage(writer.CreateMessage());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MPRIS: PropertiesChanged not sent");
        }
    }
}
#endif
