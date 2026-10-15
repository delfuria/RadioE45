using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using RadioE45.Services.Data;
using RadioE45.Services.Shared;

namespace RadioE45.Views;

public enum ShellTab
{
    OnAir,
    Request,
    Podcasts,
    Debug,
}

public sealed partial class ShellPage : Page
{
    private readonly IRadioRepository _radioRepository;
    private bool _startupCheckDone;

    public ShellPage(UnoNavigationService navigation, IRadioRepository radioRepository)
    {
        InitializeComponent();
        _radioRepository = radioRepository;
        navigation.Attach(this);
        if (UnoLayout.IsSingleColumn)
        {
            // Web: the app is a centered column as wide as the desktop window, tabs included.
            // Stretch + MaxWidth centers it at a fixed width; Center would size it to each page.
            NavView.MaxWidth = UnoLayout.ColumnMaxWidth;
            NavView.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
#if DEBUG
        NavView.FooterMenuItems.Add(new NavigationViewItem { Content = "Debug", Tag = nameof(ShellTab.Debug) });
#endif
        SelectTab(ShellTab.OnAir, clearHistory: true);
        Loaded += OnLoaded;
    }

    // Same startup check as AppShell.OnNavigated: without stations the app opens Channels, whose
    // ViewModel offers the default stations. (The terms of use come with the menu, S8.3.)
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_startupCheckDone) return;
        _startupCheckDone = true;

        if (!await _radioRepository.HasStationsAsync())
            Push(typeof(RadioListPage), clearHistory: true);
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
            ShellTab.Debug => NavView.SelectedItem, // footer item, already selected by the click
            _ => TabOnAir,
        };
        Navigate(tab);
        if (clearHistory)
            ContentFrame.BackStack.Clear();
        UpdateBackButton();
    }

    /// <summary>
    /// Stacks a page above the current one (MAUI: a pushed Shell route). With
    /// <paramref name="clearHistory"/> it goes above On Air only, like the "//OnAirPage/Route"
    /// routes of the side menu.
    /// </summary>
    public void Push(Type pageType, object? parameter = null, bool clearHistory = false)
    {
        if (clearHistory)
            SelectTab(ShellTab.OnAir, clearHistory: true);
        ContentFrame.Navigate(pageType, parameter, new SuppressNavigationTransitionInfo());
    }

    private void OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        // A tab always shows its root page, also when the tapped tab is already selected and a
        // page is stacked above it.
        if (args.InvokedItemContainer is NavigationViewItem { Tag: string tag } && Enum.TryParse<ShellTab>(tag, out var tab))
            SelectTab(tab, clearHistory: true);
    }

    private void OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args) => GoBack();

    private void OnFrameNavigated(object sender, NavigationEventArgs e) => UpdateBackButton();

    private void UpdateBackButton() => NavView.IsBackEnabled = ContentFrame.CanGoBack;

    private void Navigate(ShellTab tab)
    {
        var pageType = tab switch
        {
            ShellTab.Request => typeof(SongRequestPage),
            ShellTab.Podcasts => typeof(PodcastListPage),
            ShellTab.Debug => typeof(DebugPage),
            _ => typeof(OnAirPage),
        };

        if (ContentFrame.CurrentSourcePageType != pageType)
            ContentFrame.Navigate(pageType, null, new SuppressNavigationTransitionInfo());
    }
}
