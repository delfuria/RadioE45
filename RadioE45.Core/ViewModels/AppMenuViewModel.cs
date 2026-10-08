using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Radio;
using RadioE45.Services.Platform;

namespace RadioE45.ViewModels;

// Contenuto del menu laterale (Shell flyout): pagine dell'app (Canali, Impostazioni) e link
// della stazione corrente (sito, social, contatti).
public partial class AppMenuViewModel : BaseViewModel
{
    private readonly OnAirViewModel _onAirViewModel;
    private readonly IAzuraStationCatalog _catalog;
    private readonly INavigationService _navigation;
    private readonly IUrlLauncher _urlLauncher;

    [ObservableProperty]
    public partial string StationName { get; set; } = "";

    [ObservableProperty]
    public partial string? StationLogoUrl { get; set; }

    [ObservableProperty]
    public partial StationLink? Website { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<StationLink> SocialLinks { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<StationLink> ContactLinks { get; set; } = [];

    [ObservableProperty]
    public partial bool HasSocialLinks { get; set; }

    [ObservableProperty]
    public partial bool HasContactLinks { get; set; }

    public string AppVersion { get; }

    public AppMenuViewModel(
        OnAirViewModel onAirViewModel,
        IAzuraStationCatalog catalog,
        INavigationService navigation,
        IUrlLauncher urlLauncher,
        IUiDispatcher dispatcher,
        IAppEnvironment environment,
        ILogger<AppMenuViewModel> logger)
    {
        Logger = logger;
        _onAirViewModel = onAirViewModel;
        _catalog = catalog;
        _navigation = navigation;
        _urlLauncher = urlLauncher;
        AppVersion = $"v{environment.VersionString}";

        _onAirViewModel.PropertyChanged += OnOnAirPropertyChanged;
        // Dopo la modifica di una stazione il catalogo ricrea le istanze: CurrentStation resta
        // quella vecchia, quindi i link vanno riletti dal catalogo.
        _catalog.StationsRefreshed += () => dispatcher.Post(UpdateStationLinks);
        UpdateStationLinks();
    }

    private void OnOnAirPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OnAirViewModel.CurrentStation))
            UpdateStationLinks();
    }

    private void UpdateStationLinks()
    {
        AzuraStation? current = _onAirViewModel.CurrentStation;
        AzuraStation? station = current is null
            ? null
            : _catalog.Stations.FirstOrDefault(s => s.Id == current.Id) ?? current;

        StationName = station?.Name ?? "";
        StationLogoUrl = string.IsNullOrWhiteSpace(station?.LogoUrl) ? null : station.LogoUrl;
        Website = StationLink.ForWebsite(station?.WebsiteUrl ?? station?.PublicUrl);

        SocialLinks = new[] { station?.SocialUrl1, station?.SocialUrl2, station?.SocialUrl3 }
            .Select(StationLink.ForSocial)
            .OfType<StationLink>()
            .ToList();
        HasSocialLinks = SocialLinks.Count > 0;

        ContactLinks = new[] { StationLink.ForEmail(station?.ContactEmail), StationLink.ForPhone(station?.ContactPhone) }
            .OfType<StationLink>()
            .ToList();
        HasContactLinks = ContactLinks.Count > 0;
    }

    [RelayCommand]
    private Task OpenChannelsAsync() => _navigation.OpenFromMenuAsync(MenuPage.Channels);

    [RelayCommand]
    private Task OpenSettingsAsync() => _navigation.OpenFromMenuAsync(MenuPage.Settings);

    [RelayCommand]
    private async Task OpenLinkAsync(StationLink? link)
    {
        if (link is null)
            return;

        _navigation.CloseMenu();
        try
        {
            await _urlLauncher.OpenAsync(link.Uri);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Unable to open {Uri}", link.Uri);
        }
    }
}
