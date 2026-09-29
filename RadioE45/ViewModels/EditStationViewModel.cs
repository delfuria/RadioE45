using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Data;
using RadioE45.Services.Localization;
using RadioE45.Services.Radio;

namespace RadioE45.ViewModels;

[QueryProperty(nameof(StationId), "id")]
public partial class EditStationViewModel : BaseViewModel
{
    private readonly IRadioRepository _radioRepository;
    private readonly IAzuraStationCatalog _catalog;
    private RadioStation? _station;

    [ObservableProperty]
    public partial int StationId { get; set; }

    [ObservableProperty]
    public partial string UrlBase { get; set; } = "";

    [ObservableProperty]
    public partial int AzuraStationId { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial string ShortName { get; set; } = "";

    [ObservableProperty]
    public partial string Description { get; set; } = "";

    [ObservableProperty]
    public partial bool HasCustomInfo { get; set; }

    [ObservableProperty]
    public partial bool OverrideLatencyOffset { get; set; }

    [ObservableProperty]
    public partial int LatencyOffsetSeconds { get; set; } = 3;

    [ObservableProperty]
    public partial string WebsiteUrl { get; set; } = "";

    [ObservableProperty]
    public partial string SocialUrl1 { get; set; } = "";

    [ObservableProperty]
    public partial string SocialUrl2 { get; set; } = "";

    [ObservableProperty]
    public partial string SocialUrl3 { get; set; } = "";

    [ObservableProperty]
    public partial string ContactEmail { get; set; } = "";

    [ObservableProperty]
    public partial string ContactPhone { get; set; } = "";

    public EditStationViewModel(
        IRadioRepository radioRepository,
        IAzuraStationCatalog catalog,
        ILogger<EditStationViewModel> logger)
    {
        Logger = logger;
        _radioRepository = radioRepository;
        _catalog = catalog;
        Title = LocalizationResourceManager.Instance["EditStation_Title"];
    }

    partial void OnStationIdChanged(int value)
    {
        _ = LoadStationAsync(value);
    }

    private async Task LoadStationAsync(int id)
    {
        await SafeExecuteAsync(async () =>
        {
            _station = await _radioRepository.GetByIdAsync(id);
            if (_station is null)
                return;

            UrlBase = _station.UrlBase;
            AzuraStationId = _station.StationId;
            Name = _station.Name;
            ShortName = _station.ShortName;
            Description = _station.Description;
            HasCustomInfo = _station.HasCustomInfo;
            OverrideLatencyOffset = _station.PlaybackLatencyOffsetSeconds.HasValue;
            LatencyOffsetSeconds = _station.PlaybackLatencyOffsetSeconds ?? 3;
            WebsiteUrl = _station.WebsiteUrl ?? "";
            SocialUrl1 = _station.SocialUrl1 ?? "";
            SocialUrl2 = _station.SocialUrl2 ?? "";
            SocialUrl3 = _station.SocialUrl3 ?? "";
            ContactEmail = _station.ContactEmail ?? "";
            ContactPhone = _station.ContactPhone ?? "";
        }, LocalizationResourceManager.Instance["Err_LoadStations"]);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_station is null || IsBusy)
            return;

        if (!AreLinksValid())
        {
            ErrorMessage = LocalizationResourceManager.Instance["EditStation_InvalidLinks"];
            return;
        }

        await SafeExecuteAsync(async () =>
        {
            _station.Name = Name;
            _station.ShortName = ShortName;
            _station.Description = Description;
            _station.HasCustomInfo = HasCustomInfo;
            _station.PlaybackLatencyOffsetSeconds = OverrideLatencyOffset ? LatencyOffsetSeconds : null;
            _station.WebsiteUrl = StationLink.NormalizeUrl(WebsiteUrl);
            _station.SocialUrl1 = StationLink.NormalizeUrl(SocialUrl1);
            _station.SocialUrl2 = StationLink.NormalizeUrl(SocialUrl2);
            _station.SocialUrl3 = StationLink.NormalizeUrl(SocialUrl3);
            _station.ContactEmail = NullIfBlank(ContactEmail);
            _station.ContactPhone = NullIfBlank(ContactPhone);

            await _radioRepository.UpdateAsync(_station);
            _ = _catalog.ReloadAsync();

            await Shell.Current.GoToAsync("..");
        }, LocalizationResourceManager.Instance["Err_SaveStations"]);
    }

    // Un campo vuoto è sempre valido (link assente); uno compilato deve essere riconoscibile.
    private bool AreLinksValid() =>
        IsBlankOr(WebsiteUrl, StationLink.ForWebsite)
        && IsBlankOr(SocialUrl1, StationLink.ForSocial)
        && IsBlankOr(SocialUrl2, StationLink.ForSocial)
        && IsBlankOr(SocialUrl3, StationLink.ForSocial)
        && IsBlankOr(ContactEmail, StationLink.ForEmail)
        && IsBlankOr(ContactPhone, StationLink.ForPhone);

    private static bool IsBlankOr(string value, Func<string, StationLink?> factory)
        => string.IsNullOrWhiteSpace(value) || factory(value) is not null;

    private static string? NullIfBlank(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
