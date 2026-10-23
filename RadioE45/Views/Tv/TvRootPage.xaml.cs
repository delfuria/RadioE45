using RadioE45.Services.Audio;
using RadioE45.Services.Data;
using RadioE45.Services.Legal;
using RadioE45.ViewModels;

namespace RadioE45.Views.Tv;

public partial class TvRootPage : ContentPage, ITvHost
{
    private readonly OnAirViewModel _onAirViewModel;
    private readonly IAudioService _audioService;
    private readonly IRadioRepository _radioRepository;
    private readonly ITermsService _termsService;
    private readonly TvNowPlayingView _nowPlaying;
    private readonly TvStationsView _stations;
    private readonly TvScheduleView _schedule;
    private readonly TvAddStationView _addStation;
    private readonly TvEditStationView _editStation;
    private readonly TvRequestView _request;
    private readonly TvPodcastListView _podcasts;
    private readonly TvPodcastEpisodesView _podcastEpisodes;
    private TvSection _section = TvSection.OnAir;
    private bool _isInitialized;

    public TvRootPage(
        OnAirViewModel onAirViewModel,
        IAudioService audioService,
        IRadioRepository radioRepository,
        ITermsService termsService,
        TvHostAccessor hostAccessor,
        TvNowPlayingView nowPlaying,
        TvStationsView stations,
        TvScheduleView schedule,
        TvAddStationView addStation,
        TvEditStationView editStation,
        TvRequestView request,
        TvPodcastListView podcasts,
        TvPodcastEpisodesView podcastEpisodes)
    {
        InitializeComponent();
        _onAirViewModel = onAirViewModel;
        _audioService = audioService;
        _radioRepository = radioRepository;
        _termsService = termsService;
        _nowPlaying = nowPlaying;
        _stations = stations;
        _schedule = schedule;
        _addStation = addStation;
        _editStation = editStation;
        _request = request;
        _podcasts = podcasts;
        _podcastEpisodes = podcastEpisodes;

        // Request and Podcast appear only when the current station supports them (same properties as the phone's tabs).
        NavRequestBox.BindingContext = onAirViewModel;
        NavPodcastsBox.BindingContext = onAirViewModel;
        onAirViewModel.PropertyChanged += OnOnAirPropertyChanged;

        // Navigation and dialog services find the TV host here (there is no Shell on TV).
        hostAccessor.Current = this;

        // While the dialog is open the rest of the page is disabled, so the remote cannot leave it;
        // when it closes the focus goes back to the section.
        Dialog.Opened += (_, _) => Chrome.IsEnabled = false;
        Dialog.Closed += (_, _) =>
        {
            Chrome.IsEnabled = true;
            CurrentSection.FocusDefault();
        };

        SectionHost.Content = (View)_nowPlaying;
        UpdateNav();
        Loaded += OnLoaded;
    }

    private ITvSection CurrentSection => _section switch
    {
        TvSection.Stations => _stations,
        TvSection.Schedule => _schedule,
        TvSection.AddStation => _addStation,
        TvSection.EditStation => _editStation,
        TvSection.Request => _request,
        TvSection.Podcasts => _podcasts,
        TvSection.PodcastEpisodes => _podcastEpisodes,
        _ => _nowPlaying,
    };

    // The section the Back button (or a finished form) returns to.
    private static TvSection ParentOf(TvSection section) => section switch
    {
        TvSection.AddStation or TvSection.EditStation => TvSection.Stations,
        TvSection.PodcastEpisodes => TvSection.Podcasts,
        _ => TvSection.OnAir,
    };

    private void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        // The remote starts from the main control, not from the first focusable view of the page.
        // Focus() in OnAppearing is too early (the native view is not attached yet).
        CurrentSection.FocusDefault();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_isInitialized)
            return;

        _isInitialized = true;
#if ANDROID
        // As on OnAirPage: the Media3 service owns the player and ignores the MediaElement, but
        // Initialize connects the MediaController. There is no MediaElement on the TV page.
        (_audioService as IMediaElementHost)?.Initialize(null!);
#endif
        await _onAirViewModel.ActivateAsync();

        // First launch, in the same order as AppShell: the Terms of Use (once), then, with no stations,
        // the list that proposes the sample ones. Later launches skip both.
        await _termsService.EnsureAcceptedAsync();

        if (!await _radioRepository.HasStationsAsync())
            ShowSection(TvSection.Stations);
    }

    // --- ITvHost -------------------------------------------------------------------------------

    public void ShowSection(TvSection section) => Dispatcher.Dispatch(async () => await ShowSectionAsync(section));

    public void ShowEditStation(int stationId) => Dispatcher.Dispatch(async () =>
    {
        _editStation.Open(stationId);
        await ShowSectionAsync(TvSection.EditStation);
    });

    public void ShowPodcastEpisodes(string podcastId, string podcastTitle) => Dispatcher.Dispatch(async () =>
    {
        _podcastEpisodes.Open(podcastId, podcastTitle);
        await ShowSectionAsync(TvSection.PodcastEpisodes);
    });

    public void GoBack() => ShowSection(ParentOf(_section));

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        Dispatcher.DispatchAsync(() => Dialog.ShowAsync(title, message, accept, cancel));

    public Task AlertAsync(string title, string message, string ok) =>
        Dispatcher.DispatchAsync(() => Dialog.ShowAsync(title, message, ok, null));

    public bool HandleBack()
    {
        if (Dialog.IsOpen)
        {
            Dialog.Dismiss(false);
            return true;
        }

        if (_section != TvSection.OnAir)
        {
            GoBack();
            return true;
        }

        return false;
    }

    // -------------------------------------------------------------------------------------------

    // What the phone does with its tabs: a station change or a lost capability takes the user out of a
    // section that no longer applies.
    private void OnOnAirPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(OnAirViewModel.CurrentStation) or nameof(OnAirViewModel.HasPodcasts) or nameof(OnAirViewModel.NowPlaying)))
            return;

        TvSection? target = _section switch
        {
            TvSection.PodcastEpisodes when !_onAirViewModel.HasPodcasts => TvSection.OnAir,
            TvSection.PodcastEpisodes when e.PropertyName == nameof(OnAirViewModel.CurrentStation) => TvSection.Podcasts,
            TvSection.Podcasts when !_onAirViewModel.HasPodcasts => TvSection.OnAir,
            TvSection.Request when !_onAirViewModel.NowPlaying.RequestsEnabled => TvSection.OnAir,
            _ => null,
        };

        if (target is { } section)
            ShowSection(section);
    }

    private async Task ShowSectionAsync(TvSection section)
    {
        if (_section != section)
            await CurrentSection.OnHiddenAsync();

        _section = section;
        ITvSection view = CurrentSection;
        SectionHost.Content = (View)view;
        UpdateNav();

        await view.OnShownAsync();

        // The section may have moved on while it loaded (e.g. the default stations were accepted
        // and the host went back to On Air).
        if (_section == section)
            view.FocusDefault();
    }

    private void UpdateNav()
    {
        // Sub-sections keep their parent's button underlined.
        NavOnAirMark.IsVisible = _section is TvSection.OnAir or TvSection.Schedule;
        NavRequestMark.IsVisible = _section is TvSection.Request;
        NavPodcastsMark.IsVisible = _section is TvSection.Podcasts or TvSection.PodcastEpisodes;
        NavStationsMark.IsVisible = _section is TvSection.Stations or TvSection.AddStation or TvSection.EditStation;
    }

    private void OnNavOnAirClicked(object? sender, EventArgs e) => ShowSection(TvSection.OnAir);

    private void OnNavRequestClicked(object? sender, EventArgs e) => ShowSection(TvSection.Request);

    private void OnNavPodcastsClicked(object? sender, EventArgs e) => ShowSection(TvSection.Podcasts);

    private void OnNavStationsClicked(object? sender, EventArgs e) => ShowSection(TvSection.Stations);
}
