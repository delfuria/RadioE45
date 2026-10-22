using AVFoundation;
using Foundation;
using Microsoft.Extensions.Logging;

namespace RadioE45.Services.Audio;

/// <summary>
/// Logs what the iOS audio session does (category, interruptions such as phone calls, route
/// changes such as headphones or CarPlay) so device tests can be read from the log. Diagnostic
/// only: it does not change playback.
/// </summary>
public static class AudioSessionDiagnostics
{
    private static readonly List<NSObject> Observers = [];
    private static ILogger? _logger;

    public static void Start(ILogger logger)
    {
        if (_logger is not null)
            return;

        _logger = logger;
        LogState("startup");

        Observers.Add(AVAudioSession.Notifications.ObserveInterruption((_, e) =>
            _logger?.LogInformation("AudioSession interruption {Type} options={Options} | {State}",
                e.InterruptionType, e.Option, Describe())));

        Observers.Add(AVAudioSession.Notifications.ObserveRouteChange((_, e) =>
            _logger?.LogInformation("AudioSession route change reason={Reason} | {State}",
                e.Reason, Describe())));

        Observers.Add(AVAudioSession.Notifications.ObserveMediaServicesWereReset((_, _) =>
            _logger?.LogWarning("AudioSession media services were reset | {State}", Describe())));
    }

    public static void LogState(string context) =>
        _logger?.LogInformation("AudioSession {Context} | {State}", context, Describe());

    private static string Describe()
    {
        AVAudioSession session = AVAudioSession.SharedInstance();
        string outputs = string.Join(",", session.CurrentRoute.Outputs.Select(o => o.PortType.ToString()));
        return $"category={session.Category} options={session.CategoryOptions} mode={session.Mode} otherAudio={session.OtherAudioPlaying} outputs={outputs}";
    }
}
