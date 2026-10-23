using RadioE45.Controls;
using RadioE45.ViewModels;

namespace RadioE45.Views.Tv;

public partial class TvEditStationView : ContentView, ITvSection
{
    private readonly EditStationViewModel _viewModel;

    public TvEditStationView(EditStationViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    /// <summary>Points the form at a station; the view lives as long as the root page, so it also reloads the same one.</summary>
    public void Open(int stationId)
    {
        if (_viewModel.StationId == stationId)
            _ = _viewModel.ReloadAsync();
        else
            _viewModel.StationId = stationId;
    }

    public Task OnShownAsync() => Task.CompletedTask;

    public void FocusDefault() => Dispatcher.Dispatch(() => TvFocus.Request(NameEntry));
}
