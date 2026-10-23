using RadioE45.Controls;
using RadioE45.Services.Audio;
using RadioE45.ViewModels;

namespace RadioE45.Views.Tv;

public partial class TvPodcastEpisodesView : ContentView, ITvSection
{
    private readonly PodcastEpisodesViewModel _viewModel;
    private readonly IPodcastPlayerService _podcastPlayerService;
    private bool _isLeaving;

    public TvPodcastEpisodesView(PodcastEpisodesViewModel viewModel, IPodcastPlayerService podcastPlayerService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _podcastPlayerService = podcastPlayerService;
        BindingContext = viewModel;

        // As on PodcastEpisodesPage: every time the view is attached it hands its MediaElement to the
        // service (the native player may have been torn down while the view was off screen).
        Loaded += (_, _) => (_podcastPlayerService as IMediaElementHost)?.Initialize(PodcastPlayer);

        // The mini player appears (and disappears) in the layout, and Android drops the focus of the row that
        // started the episode: it goes to the player's controls, and back to the list when the episode ends.
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PodcastEpisodesViewModel.CurrentEpisode) || _isLeaving)
            return;

        if (_viewModel.CurrentEpisode is not null)
            Dispatcher.Dispatch(() => TvFocus.Request(PlayPauseButton));
        else
            FocusDefault();
    }

    // The root page keeps this view alive between visits: the podcast to show is set before it appears.
    public void Open(string podcastId, string podcastTitle)
    {
        _viewModel.PodcastId = podcastId;
        _viewModel.PodcastTitle = podcastTitle;
    }

    public Task OnShownAsync()
    {
        _isLeaving = false;
        _viewModel.Activate();
        return _viewModel.LoadEpisodesCommand.ExecuteAsync(null);
    }

    // Same scope as the phone: listening is tied to this screen, leaving it stops the episode
    // (progress is saved by the player service).
    public async Task OnHiddenAsync()
    {
        _isLeaving = true;
        await _viewModel.StopPlaybackAsync();
        _viewModel.Cleanup();
    }

    // The remote starts from the first episode, else from the first filter.
    public void FocusDefault() => Dispatcher.Dispatch(() =>
    {
        View? target = EpisodesLayout.Children.OfType<Button>().FirstOrDefault();
        TvFocus.Request(target ?? StatusButton);
    });
}
