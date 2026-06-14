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
    private const double WideBreakpoint = 640;
    private static bool IsDesktop =>
        DeviceInfo.Current.Platform is { } p &&
        (p == DevicePlatform.WinUI || p == DevicePlatform.MacCatalyst);

    public OnAirPage(OnAirViewModel viewModel, IAudioService audioService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _audioService = audioService;
        BindingContext = viewModel;
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
        if (width <= 0) return;

        _viewModel.ArtworkHeight = (width > height && height <= 600) ? 150 : 300;

        bool shouldBeWide = IsDesktop ? width >= WideBreakpoint : width > height;
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
