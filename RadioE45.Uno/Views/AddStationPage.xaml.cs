using Microsoft.UI.Xaml.Input;
using RadioE45.ViewModels;
using Windows.System;

namespace RadioE45.Views;

public sealed partial class AddStationPage : Page
{
    public AddStationPage()
    {
        InitializeComponent();
    }

    public AddStationViewModel ViewModel { get; } = App.Services.GetRequiredService<AddStationViewModel>();

    // MAUI Entry.ReturnCommand.
    private async void OnUrlKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
            return;

        e.Handled = true;
        await ViewModel.SearchCommand.ExecuteAsync(null);
    }
}
