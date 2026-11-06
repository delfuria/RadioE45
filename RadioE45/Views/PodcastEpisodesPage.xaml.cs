using CommunityToolkit.Maui.Views;
using RadioE45.Services.Audio;
using RadioE45.ViewModels;

namespace RadioE45.Views;

// Shell query parameters land on the page and are forwarded to the shared ViewModel, which
// lives in RadioE45.Core and cannot carry MAUI's [QueryProperty].
[QueryProperty(nameof(PodcastId), "podcastId")]
[QueryProperty(nameof(PodcastTitle), "podcastTitle")]
public partial class PodcastEpisodesPage : ContentPage
{
    private readonly PodcastEpisodesViewModel _viewModel;
    private readonly IPodcastPlayerService _podcastPlayerService;
    // Vero mentre l'utente sta trascinando lo Slider: i campioni di posizione live continuano ad
    // arrivare durante il drag (ogni ~200ms) e, se non sospesi qui, sovrascrivono continuamente
    // ProgressSlider.Value con la posizione reale (non ancora spostata), annullando visivamente
    // il trascinamento prima ancora che l'utente rilasci il dito.
    private bool _isDragging;

    public PodcastEpisodesPage(PodcastEpisodesViewModel viewModel, IPodcastPlayerService podcastPlayerService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _podcastPlayerService = podcastPlayerService;
        BindingContext = viewModel;

        // Lo Slider NON è bindato a PlaybackProgress via XAML: sul controllo nativo, un salto a un
        // valore più basso di quello corrente (es. da 0.43 a 0, cambiando episodio) a volte non
        // viene ridisegnato — il valore C# è corretto ma il widget resta fermo al vecchio punto.
        // L'assegnazione diretta qui bypassa qualunque batching interno del binding XAML.
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public string PodcastId
    {
        get => _viewModel.PodcastId;
        set => _viewModel.PodcastId = value;
    }

    public string PodcastTitle
    {
        get => _viewModel.PodcastTitle;
        set => _viewModel.PodcastTitle = value;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PodcastEpisodesViewModel.PlaybackProgress) && !_isDragging)
            ProgressSlider.Value = _viewModel.PlaybackProgress;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Every time, not only the first: the MediaElement is created on demand when an episode
        // starts (see CreatePlayerAsync) and its native player is torn down when the page goes
        // away, so the service gets a fresh one on the next play. Activate re-subscribes the ViewModel.
        (_podcastPlayerService as IMediaElementHost)?.SetPlayerFactory(CreatePlayerAsync);
        _viewModel.Activate();

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
        (_podcastPlayerService as IMediaElementHost)?.SetPlayerFactory(null);
        PlayerHost.Content = null;
    }

    // A MediaElement left in the tree after the page is popped is disposed by MAUI; its native
    // manager deactivates the iOS audio session. So it is created only when playback needs it.
    private async Task<MediaElement> CreatePlayerAsync()
    {
        MediaElement element = new() { IsVisible = false, HeightRequest = 0 };
        TaskCompletionSource connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
        element.HandlerChanged += (_, _) =>
        {
            if (element.Handler is not null)
                connected.TrySetResult();
        };

        PlayerHost.Content = element;
        if (element.Handler is null)
            await Task.WhenAny(connected.Task, Task.Delay(TimeSpan.FromSeconds(2)));

        return element;
    }

    private void OnSeekDragStarted(object? sender, EventArgs e)
    {
        _isDragging = true;
    }

    private async void OnSeekDragCompleted(object? sender, EventArgs e)
    {
        if (sender is Slider slider)
            await _viewModel.SeekCommand.ExecuteAsync(slider.Value);

        _isDragging = false;
    }
}
