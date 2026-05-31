namespace RadioE45;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage())
        {
            Width = 900,
            Height = 1600,
            MinimumWidth = 900,
            MinimumHeight = 1600
        };
    }
}