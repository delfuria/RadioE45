using RadioE45.Services.Audio;
using RadioE45.ViewModels;

namespace RadioE45.Views;

public partial class PodcastEpisodesPage : ContentPage
{
    private readonly PodcastEpisodesViewModel _viewModel;
    private readonly IPodcastPlayerService _podcastPlayerService;
    private bool _isInitialized;
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

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PodcastEpisodesViewModel.PlaybackProgress) && !_isDragging)
            ProgressSlider.Value = _viewModel.PlaybackProgress;
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
