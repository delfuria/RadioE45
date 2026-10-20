using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using RadioE45.Models;
using RadioE45.Services.Data;
using RadioE45.Services.Legal;
using RadioE45.Services.Shared;
using RadioE45.Services.Updates;
using RadioE45.ViewModels;

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
    private readonly ITermsService _termsService;
    private readonly AppUpdatePrompter _updatePrompter;
    private bool _startupCheckDone;
    private Storyboard? _menuStoryboard;
    private bool _menuOpening;
    private Storyboard? _sheetStoryboard;
    private ScheduleDialog? _sheet;

    public ShellPage(
        UnoNavigationService navigation,
        IRadioRepository radioRepository,
        ITermsService termsService,
        AppUpdatePrompter updatePrompter,
        AppMenuViewModel menuViewModel,
        OnAirViewModel onAirViewModel)
    {
        ViewModel = menuViewModel;
        OnAirViewModel = onAirViewModel;
        InitializeComponent();
        _radioRepository = radioRepository;
        _termsService = termsService;
        _updatePrompter = updatePrompter;
        navigation.Attach(this);
        if (UnoLayout.IsSingleColumn)
        {
            // Web: the app is a centered column as wide as the desktop window, tabs and side menu
            // included. Stretch + MaxWidth centers it at a fixed width; Center would size it to
            // each page.
            MenuHost.MaxWidth = UnoLayout.ColumnMaxWidth;
            MenuHost.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        MenuHost.SizeChanged += OnMenuHostSizeChanged;
        TabRequest.RegisterPropertyChangedCallback(VisibilityProperty, OnTabVisibilityChanged);
        TabPodcasts.RegisterPropertyChangedCallback(VisibilityProperty, OnTabVisibilityChanged);
#if DEBUG
        NavView.FooterMenuItems.Add(new NavigationViewItem { Content = "Debug", Tag = nameof(ShellTab.Debug) });
#endif
        SelectTab(ShellTab.OnAir, clearHistory: true);
        Loaded += OnLoaded;
    }

    /// <summary>Side menu content (MAUI: Shell.FlyoutContent bound to AppMenuViewModel).</summary>
    public AppMenuViewModel ViewModel { get; }

    /// <summary>Visibility of the station tabs (MAUI: AppShell.BindingContext).</summary>
    public OnAirViewModel OnAirViewModel { get; }

    // A station without requests (or podcasts) hides its tab: if it was the current one, go to On Air.
    private void OnTabVisibilityChanged(DependencyObject sender, DependencyProperty property)
    {
        if (sender is NavigationViewItem { Visibility: Visibility.Collapsed } tab && NavView.SelectedItem == tab)
            SelectTab(ShellTab.OnAir, clearHistory: true);
    }

    // Same startup check as AppShell.OnNavigated: terms of use on the first launch, then without
    // stations the app opens Channels, whose ViewModel offers the default stations, then the daily update check.
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_startupCheckDone) return;
        _startupCheckDone = true;

        await _termsService.EnsureAcceptedAsync();
        if (!await _radioRepository.HasStationsAsync())
            Push(typeof(RadioListPage), clearHistory: true);

        // Not awaited: the check must never delay playback. Once a day, silent on errors.
        _ = _updatePrompter.CheckAtStartupAsync();
    }

    // The menu pane slides in from outside the shell: without a clip the animation shows it to
    // the left of the web column before it reaches its place.
    private void OnMenuHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        MenuHost.Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height) };
    }

    public void OpenMenu()
    {
        _menuOpening = true;
        if (MenuPane.Visibility != Visibility.Visible)
        {
            // Off screen before it becomes visible, so the first frame never shows it open.
            _menuStoryboard?.Stop();
            _menuStoryboard = null;
            MenuPaneTransform.X = -MenuPane.Width;
            MenuDismissLayer.Opacity = 0;
        }
        MenuPane.Visibility = Visibility.Visible;
        MenuDismissLayer.Visibility = Visibility.Visible;
        AnimateMenu(open: true);
    }

    public void CloseMenu()
    {
        if (MenuPane.Visibility != Visibility.Visible)
            return;
        _menuOpening = false;
        AnimateMenu(open: false);
    }

    private void AnimateMenu(bool open)
    {
        // Stop() puts back the local values: keep the animated ones, so a close (or a reopen
        // while closing) starts from where the pane is.
        if (_menuStoryboard is not null)
        {
            double x = MenuPaneTransform.X, opacity = MenuDismissLayer.Opacity;
            _menuStoryboard.Stop();
            MenuPaneTransform.X = x;
            MenuDismissLayer.Opacity = opacity;
        }

        var duration = new Duration(TimeSpan.FromMilliseconds(open ? 250 : 200));
        var easing = new CubicEase { EasingMode = open ? EasingMode.EaseOut : EasingMode.EaseIn };
        var slide = new DoubleAnimation { To = open ? 0 : -MenuPane.Width, Duration = duration, EasingFunction = easing };
        var fade = new DoubleAnimation { To = open ? 1 : 0, Duration = duration };
        Storyboard.SetTarget(slide, MenuPaneTransform);
        Storyboard.SetTargetProperty(slide, "X");
        Storyboard.SetTarget(fade, MenuDismissLayer);
        Storyboard.SetTargetProperty(fade, "Opacity");

        var storyboard = new Storyboard { Children = { slide, fade } };
        if (!open)
        {
            storyboard.Completed += (_, _) =>
            {
                if (_menuOpening) return; // reopened while closing
                MenuPane.Visibility = Visibility.Collapsed;
                MenuDismissLayer.Visibility = Visibility.Collapsed;
            };
        }
        _menuStoryboard = storyboard;
        storyboard.Begin();
    }

    private void OnDismissLayerTapped(object sender, TappedRoutedEventArgs e) => CloseMenu();

    private void OnEscapeInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_sheet is not null)
            CloseSheet();
        else if (MenuPane.Visibility == Visibility.Visible)
            CloseMenu();
        else
            return;
        args.Handled = true;
    }

    /// <summary>
    /// Shows the schedule as a sheet sliding up from the bottom (MAUI: SchedulePopup at the
    /// bottom of the window, at most ~70% of its height; above that the list scrolls).
    /// </summary>
    public void ShowSheet(ScheduleDialog sheet)
    {
        if (_sheet is not null)
            return;
        _sheet = sheet;
        sheet.CloseRequested += OnSheetCloseRequested;

        _sheetStoryboard?.Stop();
        _sheetStoryboard = null;
        double hostHeight = MenuHost.ActualHeight;
        SheetPane.MaxHeight = hostHeight * 0.7;
        SheetPane.Child = sheet;
        // Below the shell before it becomes visible, so the first frame never shows it open.
        SheetPaneTransform.Y = hostHeight;
        SheetDismissLayer.Opacity = 0;
        SheetPane.Visibility = Visibility.Visible;
        SheetDismissLayer.Visibility = Visibility.Visible;
        AnimateSheet(open: true);
        sheet.Load();
    }

    public void CloseSheet()
    {
        if (_sheet is not { } sheet)
            return;
        sheet.CloseRequested -= OnSheetCloseRequested;
        _sheet = null;
        AnimateSheet(open: false);
    }

    private void OnSheetCloseRequested(object? sender, EventArgs e) => CloseSheet();

    private void OnSheetDismissLayerTapped(object sender, TappedRoutedEventArgs e) => CloseSheet();

    private void AnimateSheet(bool open)
    {
        // As for the menu: keep the animated values when an animation is interrupted.
        if (_sheetStoryboard is not null)
        {
            double y = SheetPaneTransform.Y, opacity = SheetDismissLayer.Opacity;
            _sheetStoryboard.Stop();
            SheetPaneTransform.Y = y;
            SheetDismissLayer.Opacity = opacity;
        }

        var duration = new Duration(TimeSpan.FromMilliseconds(open ? 250 : 200));
        var easing = new CubicEase { EasingMode = open ? EasingMode.EaseOut : EasingMode.EaseIn };
        var slide = new DoubleAnimation { To = open ? 0 : MenuHost.ActualHeight, Duration = duration, EasingFunction = easing };
        var fade = new DoubleAnimation { To = open ? 1 : 0, Duration = duration };
        Storyboard.SetTarget(slide, SheetPaneTransform);
        Storyboard.SetTargetProperty(slide, "Y");
        Storyboard.SetTarget(fade, SheetDismissLayer);
        Storyboard.SetTargetProperty(fade, "Opacity");

        var storyboard = new Storyboard { Children = { slide, fade } };
        if (!open)
        {
            storyboard.Completed += (_, _) =>
            {
                if (_sheet is not null) return; // another sheet opened while closing
                SheetPane.Visibility = Visibility.Collapsed;
                SheetDismissLayer.Visibility = Visibility.Collapsed;
                SheetPane.Child = null;
            };
        }
        _sheetStoryboard = storyboard;
        storyboard.Begin();
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

    // Item template buttons cannot reach the page ViewModel with x:Bind: the link is in Tag.
    private void OnLinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: StationLink link })
            ViewModel.OpenLinkCommand.Execute(link);
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
