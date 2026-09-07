using RadioE45.Services.Audio;
using RadioE45.ViewModels;

namespace RadioE45.Views;

public partial class PodcastEpisodesPage : ContentPage
{
    private readonly PodcastEpisodesViewModel _viewModel;
    private readonly IPodcastPlayerService _podcastPlayerService;
    private bool _isInitialized;

    public PodcastEpisodesPage(PodcastEpisodesViewModel viewModel, IPodcastPlayerService podcastPlayerService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _podcastPlayerService = podcastPlayerService;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_isInitialized)
        {
            _isInitialized = true;
            _podcastPlayerService.Initialize(PodcastPlayer);
        }

        // La stazione può essere cambiata (frecce OnAir) mentre eravamo su un'altra tab: gli
        // episodi caricati non sono più pertinenti, si torna alla lista podcast della stazione attuale.
        if (_viewModel.IsStaleForCurrentStation())
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        await _viewModel.LoadEpisodesCommand.ExecuteAsync(null);
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();

        // Scope v1: la riproduzione podcast e' legata a questa pagina, niente
        // notifica/lockscreen in background — vedi PODCAST-FEATURE.md.
        await _viewModel.StopPlaybackAsync();
        _viewModel.Cleanup();
    }

    private void OnSeekDragCompleted(object? sender, EventArgs e)
    {
        if (sender is Slider slider)
            _viewModel.SeekCommand.Execute(slider.Value);
    }
}
