namespace RadioE45.Controls;

public partial class TvSplashOverlay : ContentView
{
    public TvSplashOverlay() => InitializeComponent();

    public string Version
    {
        set => VersionLabel.Text = value;
    }

    /// <summary>Fades the splash screen out and removes it from the layout.</summary>
    public async Task HideAsync()
    {
        await this.FadeToAsync(0, 300);
        IsVisible = false;
    }
}
