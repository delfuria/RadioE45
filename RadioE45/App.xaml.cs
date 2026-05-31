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
            Width = 600,
            Height = 1200,
            MinimumWidth = 600,
            MinimumHeight = 1200
        };
    }
}