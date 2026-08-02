using RadioE45.Services.Data;
using RadioE45.Views;

namespace RadioE45;

public partial class AppShell : Shell
{
    private readonly IRadioRepository _radioRepository;
    private bool _startupCheckDone;

    public AppShell(IRadioRepository radioRepository)
    {
        _radioRepository = radioRepository;
        InitializeComponent();
        Routing.RegisterRoute("AddStationPage", typeof(AddStationPage));
        Routing.RegisterRoute("EditStationPage", typeof(EditStationPage));
        Navigated += OnNavigated;
    }

    private async void OnNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        if (_startupCheckDone) return;
        _startupCheckDone = true;

        bool hasStations = await _radioRepository.HasStationsAsync();
        if (!hasStations)
            await GoToAsync("//RadioListPage");
    }
}
