using RadioE45.Services.Data;
using RadioE45.Services.Legal;
using RadioE45.ViewModels;
using RadioE45.Views;

namespace RadioE45;

public partial class AppShell : Shell
{
    private readonly IRadioRepository _radioRepository;
    private readonly ITermsService _termsService;
    private bool _startupCheckDone;

    public AppShell(IRadioRepository radioRepository, OnAirViewModel onAirViewModel, AppMenuViewModel appMenuViewModel, ITermsService termsService)
    {
        _radioRepository = radioRepository;
        _termsService = termsService;
        BindingContext = onAirViewModel;
        InitializeComponent();
        MenuRoot.BindingContext = appMenuViewModel;

        Routing.RegisterRoute("RadioListPage", typeof(RadioListPage));
        Routing.RegisterRoute("SettingsPage", typeof(SettingsPage));
        Routing.RegisterRoute("AddStationPage", typeof(AddStationPage));
        Routing.RegisterRoute("EditStationPage", typeof(EditStationPage));
        Routing.RegisterRoute("PodcastEpisodesPage", typeof(PodcastEpisodesPage));
        Navigated += OnNavigated;
#if WINDOWS
        HookWindowsTabIcons();
#endif
    }

#if WINDOWS
    // On Windows, tabs that become visible at runtime (Request/Podcast, IsVisible bound) get a
    // native tab icon with the default (near black) foreground, invisible on the dark tab bar,
    // until a tab is selected. TabIconFixer colors them once the native items exist; the second pass covers
    // slow layouts.
    private void HookWindowsTabIcons()
    {
        foreach (ShellContent tab in new[] { TabOnAir, TabRequest, TabPodcasts })
            tab.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(IsVisible))
                    ScheduleWindowsTabIconFix();
            };

        Loaded += (_, _) => ScheduleWindowsTabIconFix();
    }

    private void ScheduleWindowsTabIconFix()
    {
        foreach (int delayMs in new[] { 150, 600, 1500 })
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(delayMs),
                () => Platforms.Windows.TabIconFixer.Apply(this));
    }
#endif

    private async void OnNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        if (_startupCheckDone) return;
        _startupCheckDone = true;

        // Prima dell'eventuale proposta di stazioni di prova: i Termini d'uso compaiono solo al primo avvio.
        // Il primo Navigated arriva prima che la Shell sia nella finestra: su iOS/Windows l'AlertManager
        // di MAUI scarta la richiesta senza mai completare il Task, e lo startup resterebbe appeso.
        await WaitUntilLoadedAsync();
        await _termsService.EnsureAcceptedAsync();

        bool hasStations = await _radioRepository.HasStationsAsync();
        if (!hasStations)
            await GoToAsync("//OnAirPage/RadioListPage");
    }

    private async Task WaitUntilLoadedAsync()
    {
        if (!IsLoaded)
        {
            TaskCompletionSource loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnLoaded(object? s, EventArgs e)
            {
                Loaded -= OnLoaded;
                loaded.TrySetResult();
            }
            Loaded += OnLoaded;
            await loaded.Task;
        }

        // Un giro di dispatcher in più: la finestra nativa finisce di attivarsi dopo Loaded.
        TaskCompletionSource dispatched = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.Dispatch(() => dispatched.TrySetResult());
        await dispatched.Task;
    }
}
