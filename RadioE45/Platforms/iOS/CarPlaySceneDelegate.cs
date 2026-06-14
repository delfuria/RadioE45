using CarPlay;
using Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Radio;

namespace RadioE45;

// UISceneDelegateClassName in the built Info.plist (injected by inject-carplay-manifest.sh)
// must match the Objective-C registered name below.
[Register("CarPlaySceneDelegate")]
public sealed class CarPlaySceneDelegate : CPTemplateApplicationSceneDelegate
{
    private IServiceScope? _scope;
    private IAudioService? _audioService;
    private INowPlayingService? _nowPlayingService;
    private IAzuraStationCatalog? _catalog;
    private ILogger<CarPlaySceneDelegate>? _logger;
    private CPInterfaceController? _controller;

    public override void DidConnect(
        CPTemplateApplicationScene templateApplicationScene,
        CPInterfaceController interfaceController)
    {
        System.Diagnostics.Debug.WriteLine("[CarPlay] DidConnect called");
        _controller = interfaceController;

        IServiceProvider? services = IPlatformApplication.Current?.Services;
        if (services is null)
        {
            System.Diagnostics.Debug.WriteLine("[CarPlay] DidConnect: IPlatformApplication.Current?.Services is null — aborting");
            return;
        }

        _scope = services.CreateScope();
        IServiceProvider sp = _scope.ServiceProvider;
        _audioService = sp.GetRequiredService<IAudioService>();
        _nowPlayingService = sp.GetRequiredService<INowPlayingService>();
        _catalog = sp.GetRequiredService<IAzuraStationCatalog>();
        _logger = sp.GetService<ILogger<CarPlaySceneDelegate>>();

        System.Diagnostics.Debug.WriteLine($"[CarPlay] DidConnect: stations={_catalog?.Stations?.Count ?? -1}");
        SetupRootTemplate();
    }

    public override void DidDisconnect(
        CPTemplateApplicationScene templateApplicationScene,
        CPInterfaceController interfaceController)
    {
        _scope?.Dispose();
        _scope = null;
        _controller = null;
        _audioService = null;
        _nowPlayingService = null;
        _catalog = null;
    }

    private void SetupRootTemplate()
    {
        try
        {
            var stations = _catalog?.Stations ?? [];
            System.Diagnostics.Debug.WriteLine($"[CarPlay] SetupRootTemplate: building list with {stations.Count} station(s)");

            // CarPlay requires at least one item; show a placeholder while loading.
            CPListItem[] items = stations.Count > 0
                ? stations.Select(BuildStationListItem).ToArray()
                : [new CPListItem("Caricamento stazioni...", null)];

            CPListSection section = new(
                items.Cast<ICPListTemplateItem>().ToArray(),
                header: null,
                sectionIndexTitle: null);

            CPListTemplate listTemplate = new("Stazioni", new[] { section });
            _ = _controller!.SetRootTemplateAsync(listTemplate, animated: false)
                .ContinueWith(t => System.Diagnostics.Debug.WriteLine($"[CarPlay] SetRootTemplateAsync result: {t.Result}"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CarPlay] SetupRootTemplate EXCEPTION: {ex}");
            _logger?.LogError(ex, "CarPlay: SetupRootTemplate failed");
        }
    }

    private CPListItem BuildStationListItem(AzuraStation station)
    {
        bool isCurrent = _audioService?.CurrentStation?.Id == station.Id;
        string? currentTitle = _nowPlayingService?.Current.Title;
        string? subtitle = (isCurrent && !string.IsNullOrEmpty(currentTitle))
            ? currentTitle
            : station.Description;

        CPListItem item = new(station.Name, subtitle);
        // 'listItem' (not '_') so that '_' inside the body is a true discard, not an assignment to the param.
        item.Handler = (listItem, completion) =>
        {
            _ = PlayStationAsync(station);
            completion();
        };
        return item;
    }

    private async Task PlayStationAsync(AzuraStation station)
    {
        if (_audioService is null || _controller is null) return;

        try
        {
            await _audioService.PlayAsync(station);
            // After playback starts, push the Now Playing screen.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_controller is not null)
                    _ = _controller.PushTemplateAsync(CPNowPlayingTemplate.SharedTemplate, animated: true);
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "CarPlay: PlayStationAsync failed for {Station}", station.Name);
        }
    }
}
