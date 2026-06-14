using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Radio;

namespace RadioE45.ViewModels;

public partial class ScheduleViewModel : BaseViewModel
{
    private readonly IScheduleService _scheduleService;
    private readonly OnAirViewModel _onAirViewModel;

    [ObservableProperty]
    public partial ObservableCollection<PlaylistSchedule> ScheduleItems { get; set; } = [];

    public ScheduleViewModel(IScheduleService scheduleService, OnAirViewModel onAirViewModel, ILogger<ScheduleViewModel> logger)
    {
        Logger = logger;
        _scheduleService = scheduleService;
        _onAirViewModel = onAirViewModel;
        Title = "Palinsesto";
    }

    [RelayCommand]
    private async Task LoadScheduleAsync()
    {
        AzuraStation? station = _onAirViewModel.CurrentStation;
        if (station is null)
        {
            ScheduleItems = [];
            return;
        }

        await SafeExecuteAsync(async () =>
        {
            List<PlaylistSchedule> items = await _scheduleService.GetScheduleAsync(station);
            ScheduleItems = new ObservableCollection<PlaylistSchedule>(items);
        }, "Caricamento palinsesto");
    }
}
