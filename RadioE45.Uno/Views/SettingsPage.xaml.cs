using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using RadioE45.ViewModels;

namespace RadioE45.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; } = App.Services.GetRequiredService<SettingsViewModel>();

    // Slider.Value is a double and the settings are ints: x:Bind cannot convert them back.
    private void OnLatencyChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        ViewModel.PlaybackLatencyOffsetSeconds = (int)Math.Round(e.NewValue);
    }

    private void OnRequestPageSizeChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        ViewModel.RequestPageSize = (int)Math.Round(e.NewValue);
    }

    // ComboBox.SelectedItem is an object: x:Bind cannot write it back to the string preference.
    private void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemePicker.SelectedItem is string preference)
            ViewModel.ThemePreference = preference;
    }

    // Hidden section, as in MAUI: a double tap on its title shows it until the page is left.
    private void OnDiagnosticsDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        DiagnosticsCard.Visibility = Visibility.Visible;
    }
}
