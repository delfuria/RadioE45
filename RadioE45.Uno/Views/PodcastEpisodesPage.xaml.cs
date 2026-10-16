using System.ComponentModel;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using RadioE45.Models;
using RadioE45.ViewModels;

namespace RadioE45.Views;

/// <summary>Navigation parameter of <see cref="PodcastEpisodesPage"/> (MAUI: query properties).</summary>
public sealed record PodcastEpisodesParameter(string PodcastId, string PodcastTitle);

public sealed partial class PodcastEpisodesPage : Page
{
    // True while the user drags the slider: the position samples keep arriving (~200 ms) and
    // would put the thumb back to the real position before the release (as in MAUI).
    private bool _isDragging;
    private bool _isSettingProgress;

    public PodcastEpisodesPage()
    {
        InitializeComponent();
        // handledEventsToo: the Slider handles the pointer events itself.
        ProgressSlider.AddHandler(PointerPressedEvent, new PointerEventHandler(OnSeekPointerPressed), true);
        ProgressSlider.AddHandler(PointerCaptureLostEvent, new PointerEventHandler(OnSeekPointerReleased), true);
    }

    public PodcastEpisodesViewModel ViewModel { get; } = App.Services.GetRequiredService<PodcastEpisodesViewModel>();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is PodcastEpisodesParameter parameter)
        {
            ViewModel.PodcastId = parameter.PodcastId;
            ViewModel.PodcastTitle = parameter.PodcastTitle;
        }

        // Same order as MAUI OnAppearing: subscribe the ViewModel to the player again, leave
        // episodes of another station, then load.
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.Activate();

        if (ViewModel.IsStaleForCurrentStation())
        {
            // After the current navigation has completed.
            DispatcherQueue.TryEnqueue(() =>
            {
                if (Frame.CanGoBack)
                    Frame.GoBack();
            });
            return;
        }

        await ViewModel.LoadEpisodesCommand.ExecuteAsync(null);
    }

    // MAUI OnDisappearing: the episode plays only while its page is open (PODCAST-FEATURE.md).
    protected override async void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        await ViewModel.StopPlaybackAsync();
        ViewModel.Cleanup();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PodcastEpisodesViewModel.PlaybackProgress) && !_isDragging)
        {
            _isSettingProgress = true;
            ProgressSlider.Value = ViewModel.PlaybackProgress;
            _isSettingProgress = false;
        }
    }

    private void OnSeekPointerPressed(object sender, PointerRoutedEventArgs e) => _isDragging = true;

    private async void OnSeekPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
            return;
        await ViewModel.SeekCommand.ExecuteAsync(ProgressSlider.Value);
        _isDragging = false;
    }

    // Keyboard (arrows, Home/End) on the focused slider: seek at once.
    private async void OnSeekValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isSettingProgress || _isDragging)
            return;
        await ViewModel.SeekCommand.ExecuteAsync(e.NewValue);
    }

    // Picker SelectedItem TwoWay in MAUI: x:Bind cannot convert the ComboBox object back.
    private void OnSeasonSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SeasonPicker.SelectedItem is PodcastFilterOption<int> option)
            ViewModel.SelectedSeason = option;
    }

    private void OnEpisodeNumberSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EpisodeNumberPicker.SelectedItem is PodcastFilterOption<int> option)
            ViewModel.SelectedEpisodeNumber = option;
    }

    private void OnStatusSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StatusPicker.SelectedItem is PodcastFilterOption<PodcastEpisodeProgressState> option)
            ViewModel.SelectedStatus = option;
    }

    private async void OnRefreshRequested(RefreshContainer sender, RefreshRequestedEventArgs args)
    {
        using var deferral = args.GetDeferral();
        await ViewModel.LoadEpisodesCommand.ExecuteAsync(null);
    }

    private void OnRefreshAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        Refresher.RequestRefresh();
    }
}
