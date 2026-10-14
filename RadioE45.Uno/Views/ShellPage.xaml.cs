using Microsoft.UI.Xaml.Media.Animation;
using RadioE45.Services.Shared;

namespace RadioE45.Views;

public enum ShellTab
{
    OnAir,
    Request,
    Podcasts,
}

public sealed partial class ShellPage : Page
{
    public ShellPage(UnoNavigationService navigation)
    {
        InitializeComponent();
        navigation.Attach(this);
        SelectTab(ShellTab.OnAir, clearHistory: true);
    }

    public void GoBack()
    {
        if (ContentFrame.CanGoBack)
            ContentFrame.GoBack();
    }

    public void SelectTab(ShellTab tab, bool clearHistory)
    {
        NavView.SelectedItem = tab switch
        {
            ShellTab.Request => TabRequest,
            ShellTab.Podcasts => TabPodcasts,
            _ => TabOnAir,
        };
        Navigate(tab);
        if (clearHistory)
            ContentFrame.BackStack.Clear();
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem { Tag: string tag } && Enum.TryParse<ShellTab>(tag, out var tab))
            Navigate(tab);
    }

    private void Navigate(ShellTab tab)
    {
        var pageType = tab switch
        {
            ShellTab.Request => typeof(SongRequestPage),
            ShellTab.Podcasts => typeof(PodcastListPage),
            _ => typeof(OnAirPage),
        };

        if (ContentFrame.CurrentSourcePageType != pageType)
            ContentFrame.Navigate(pageType, null, new SuppressNavigationTransitionInfo());
    }
}
