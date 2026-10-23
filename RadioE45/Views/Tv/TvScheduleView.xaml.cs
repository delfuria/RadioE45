using RadioE45.Controls;
using RadioE45.ViewModels;

namespace RadioE45.Views.Tv;

public partial class TvScheduleView : ContentView, ITvSection
{
    private readonly ScheduleViewModel _viewModel;
    private readonly TvHostAccessor _host;

    public TvScheduleView(ScheduleViewModel viewModel, TvHostAccessor host)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _host = host;
        BindingContext = viewModel;
    }

    public Task OnShownAsync() => _viewModel.LoadScheduleCommand.ExecuteAsync(null);

    // The remote starts from the first slot (the list scrolls from there), else from Close.
    public void FocusDefault() => Dispatcher.Dispatch(() =>
    {
        Button? target = ScheduleLayout.Children.OfType<Button>().FirstOrDefault(b => b.IsEnabled) ?? CloseButton;
        TvFocus.Request(target);
    });

    private void OnCloseClicked(object? sender, EventArgs e) => _host.Current?.GoBack();
}
