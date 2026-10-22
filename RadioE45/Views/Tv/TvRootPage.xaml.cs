using RadioE45.Services.Audio;
using RadioE45.ViewModels;

namespace RadioE45.Views.Tv;

public partial class TvRootPage : ContentPage
{
    private readonly OnAirViewModel _viewModel;
    private readonly IAudioService _audioService;
    private bool _isInitialized;

    public TvRootPage(OnAirViewModel viewModel, IAudioService audioService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _audioService = audioService;
        BindingContext = viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        // The remote starts from the main control, not from the first focusable view of the page.
        // Focus() in OnAppearing is too early (the native view is not attached yet).
        PlayPause.Dispatcher.Dispatch(() => PlayPause.Focus());
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_isInitialized)
        {
            _isInitialized = true;
#if ANDROID
            // As on OnAirPage: the Media3 service owns the player and ignores the MediaElement, but
            // Initialize connects the MediaController. There is no MediaElement on the TV page.
            (_audioService as IMediaElementHost)?.Initialize(null!);
#endif
        }

        await _viewModel.ActivateAsync();
    }
}
