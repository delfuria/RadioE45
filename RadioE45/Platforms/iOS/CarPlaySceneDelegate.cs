using CarPlay;
using Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Radio;

namespace RadioE45;

// UISceneDelegateClassName in Platforms/iOS/Info.plist (CPTemplateApplicationSceneSessionRoleApplication)
// must match the Objective-C registered name below.
[Register("CarPlaySceneDelegate")]
public sealed class CarPlaySceneDelegate : CPTemplateApplicationSceneDelegate
{
    private IServiceScope? _scope;
    private IAudioService? _audioService;
    private CarStationList? _stationList;
    private CPListTemplate? _rootTemplate;
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
        _stationList = sp.GetRequiredService<CarStationList>();
        _logger = sp.GetService<ILogger<CarPlaySceneDelegate>>();

        // The catalog may still be loading (cold start from CarPlay): show the placeholder now and
        // redraw whenever the list changes. Changed can be raised on any thread.
        _stationList.Changed += OnStationListChanged;
        SetupRootTemplate();
        _ = _stationList.LoadAsync();
    }

    public override void DidDisconnect(
        CPTemplateApplicationScene templateApplicationScene,
        CPInterfaceController interfaceController)
    {
        if (_stationList is not null)
            _stationList.Changed -= OnStationListChanged;

        // Disposing the scope also disposes the CarStationList (unsubscribes from the catalog).
        _scope?.Dispose();
        _scope = null;
        _controller = null;
        _audioService = null;
        _stationList = null;
        _rootTemplate = null;
    }

    private void SetupRootTemplate()
    {
        try
        {
            if (_stationList is null || _controller is null)
                return;

            _rootTemplate = new CPListTemplate(_stationList.Title, BuildSections());
            _ = SetRootTemplateAsync(_controller, _rootTemplate);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CarPlay] SetupRootTemplate EXCEPTION: {ex}");
            _logger?.LogError(ex, "CarPlay: SetupRootTemplate failed");
        }
    }

    private async Task SetRootTemplateAsync(CPInterfaceController controller, CPListTemplate template)
    {
        (bool ok, NSError? error) = await controller.SetRootTemplateAsync(template, animated: false);
        System.Diagnostics.Debug.WriteLine($"[CarPlay] SetRootTemplateAsync result: {ok} {error?.LocalizedDescription}");
    }

    private void OnStationListChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                _rootTemplate?.UpdateSections(BuildSections());
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "CarPlay: updating the station list failed");
            }
        });
    }

    private CPListSection[] BuildSections()
    {
        // CarPlay requires at least one item; while there are no stations show a placeholder row.
        CPListItem[] items = _stationList is { State: CarStationListState.Ready }
            ? _stationList.Items.Select(BuildStationListItem).ToArray()
            : [new CPListItem(_stationList?.PlaceholderText, null)];

        return [new CPListSection(items.Cast<ICPListTemplateItem>().ToArray(), header: null, sectionIndexTitle: null)];
    }

    private CPListItem BuildStationListItem(CarStationListItem entry)
    {
        CPListItem item = new(entry.Title, entry.Subtitle);
        // 'listItem' (not '_') so that '_' inside the body is a true discard, not an assignment to the param.
        item.Handler = (listItem, completion) =>
        {
            _ = PlayStationAsync(entry.Station);
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
