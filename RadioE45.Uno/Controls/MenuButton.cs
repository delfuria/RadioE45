using Microsoft.UI.Xaml.Automation;
using RadioE45.Services.Localization;
using RadioE45.Services.Shared;

namespace RadioE45.Controls;

/// <summary>
/// ☰ button that opens the side menu, the Uno counterpart of RadioE45/Controls/MenuButton.cs.
/// The shell has no hamburger of its own (tabs on top), so pages with tabs include it in their
/// header. Look: the <c>MenuButton</c> style (Styles.xaml).
/// </summary>
public sealed partial class MenuButton : Button
{
    public MenuButton()
    {
        Style = (Style)Application.Current.Resources["MenuButton"];
        AutomationProperties.SetName(this, LocalizationResourceManager.Instance["Menu_Open"]);
        Click += (_, _) => App.Services.GetRequiredService<UnoNavigationService>().OpenMenu();
    }
}
