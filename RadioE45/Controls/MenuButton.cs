namespace RadioE45.Controls;

// Pulsante ☰ che apre il menu laterale della Shell. La NavBar è nascosta (niente hamburger
// nativo), quindi ogni pagina con tab lo include nel proprio header.
public sealed class MenuButton : Button
{
    public MenuButton()
    {
        Text = "\uf0c9";
        FontFamily = "FaSolid";
        FontSize = 18;
        WidthRequest = 44;
        HeightRequest = 44;
        CornerRadius = 22;
        Padding = 0;
        BackgroundColor = Colors.Transparent;
        SetDynamicResource(TextColorProperty, "PrimaryText");
        SemanticProperties.SetDescription(this, Services.Localization.LocalizationResourceManager.Instance["Menu_Open"]);
        Clicked += OnClicked;
    }

    private static void OnClicked(object? sender, EventArgs e)
    {
        if (Shell.Current is Shell shell)
            shell.FlyoutIsPresented = true;
    }
}
