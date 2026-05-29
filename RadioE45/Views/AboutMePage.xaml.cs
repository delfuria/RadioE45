namespace RadioE45.Views;

public partial class AboutMePage : ContentPage
{
	public AboutMePage()
	{
		InitializeComponent();

	}

    private void Share_Clicked(object sender, EventArgs e)
    {

    }
    private async void SupportMe_Clicked(object sender, EventArgs e)
    {
        string websiteUrl = "https://radioe45.ddns.net/public/radioe45";

        try
        {
            await Launcher.OpenAsync(websiteUrl);
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., no browser available)
            await DisplayAlert("Error", $"Could not open website: {ex.Message}", "OK");
        }
    }
}