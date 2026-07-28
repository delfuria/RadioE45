using CommunityToolkit.Maui.Views;
using RadioE45.Services.Audio;
using RadioE45.ViewModels;

namespace RadioE45.Views;

public partial class OnAirPage : ContentPage
{
    private readonly OnAirViewModel _viewModel;
    private readonly IAudioService _audioService;
    private bool _isInitialized;
    private bool _isWideLayout = false;

    public OnAirPage(OnAirViewModel viewModel, IAudioService audioService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _audioService = audioService;
        BindingContext = viewModel;

#if ANDROID
        // On Android playback runs in the Media3 service (single session). Detach the hidden
        // MediaElement before it's rendered so its handler never spins up a second, competing
        // MediaSession — removing it here (pre-render) prevents the handler from being created.
        (AudioPlayer.Parent as Microsoft.Maui.Controls.Layout)?.Remove(AudioPlayer);
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_isInitialized)
        {
            _isInitialized = true;
            _audioService.Initialize(AudioPlayer);
            _audioService.SetVolume(_viewModel.Volume);
            await _viewModel.InitializeAsync();
        }

        // Questo check corre sia al primo avvio (dopo InitializeAsync) sia ai ritorni
        // sulla pagina. Copre il caso in cui OnAirPage è una nuova istanza (transient)
        // ma OnAirViewModel è il singleton sopravvissuto: InitializeAsync ritorna subito
        // perché CurrentStation != null, ma AudioService è stato azzerato dallo swipe.
        if (_audioService.CurrentStation is null && _viewModel.CurrentStation is not null)
            await _viewModel.RestartAsync();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0) return;

        // Size the cover from the space actually available so the whole player (cover, track info,
        // next-track card, the playback controls and the volume row) fits on screen without
        // scrolling. In portrait the cover gets whatever height is left after the fixed elements
        // below it; in landscape the two-column layout puts the cover beside the controls, so it's
        // bound to the shorter side and the volume row costs it nothing.
        _viewModel.ArtworkHeight = width > height
            ? Math.Clamp(height - 220, 140, 300)
            : Math.Clamp(height - 458, 150, 300);

        bool shouldBeWide = width > height;
        if (shouldBeWide == _isWideLayout) return;
        _isWideLayout = shouldBeWide;

        if (shouldBeWide)
        {
            MainGrid.ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            ];
            Grid.SetRow(RightPanel, 0);
            Grid.SetColumn(RightPanel, 1);
        }
        else
        {
            MainGrid.ColumnDefinitions = [new ColumnDefinition(GridLength.Star)];
            Grid.SetRow(RightPanel, 1);
            Grid.SetColumn(RightPanel, 0);
        }

    }

    private void OnVolumeChanged(object sender, ValueChangedEventArgs e)
    {
        _viewModel.SetVolumeCommand.Execute(e.NewValue);
    }
}
