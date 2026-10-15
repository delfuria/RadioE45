using RadioE45.Services.Platform;
using RadioE45.Services.Shared;

namespace RadioE45.Views;

public sealed partial class DebugPage : Page
{
    private const string OpenCountKey = "debug_page_open_count";

    private readonly IUiDispatcher _dispatcher = App.Services.GetRequiredService<IUiDispatcher>();
    private readonly IDialogService _dialogs = App.Services.GetRequiredService<IDialogService>();
    private readonly ISettingsStore _settings = App.Services.GetRequiredService<ISettingsStore>();
    private readonly IAppEnvironment _environment = App.Services.GetRequiredService<IAppEnvironment>();
    private readonly IUrlLauncher _urlLauncher = App.Services.GetRequiredService<IUrlLauncher>();
    private readonly IThemeService _theme = App.Services.GetRequiredService<IThemeService>();
    private IUiTimer? _timer;
    private int _ticks;

    public DebugPage()
    {
        InitializeComponent();

        EnvironmentText.Text =
            $"Versione {_environment.VersionString} (build {_environment.BuildString}, {_environment.CommitId}) · " +
            $"{_environment.PlatformName} · volume di sistema: {_environment.UsesSystemVolume} · " +
            $"Local Network privacy: {_environment.HasLocalNetworkPrivacy}";

        // Persisted counter: after a restart it must continue from the previous value.
        var count = _settings.Get(OpenCountKey, 0) + 1;
        _settings.Set(OpenCountKey, count);
        OpenCountText.Text = $"Pagina aperta {count} volte (ISettingsStore, persiste tra i riavvii)";

        ThemeBox.SelectedItem = _settings.Get(UnoThemeService.PreferenceKey, UnoThemeService.DefaultPreference);

        Loaded += (_, _) =>
        {
            _timer = _dispatcher.CreateTimer(TimeSpan.FromSeconds(1), () => TimerText.Text = $"Timer UI: {++_ticks} s");
            _timer?.Start();
        };
        Unloaded += (_, _) =>
        {
            _timer?.Stop();
            _timer = null;
        };
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeBox.SelectedItem is not string preference)
            return;
        _settings.Set(UnoThemeService.PreferenceKey, preference);
        _theme.Apply(preference);
    }

    private async void OnAlertClick(object sender, RoutedEventArgs e) =>
        await _dialogs.AlertAsync("Alert", "Messaggio di prova di IDialogService.AlertAsync.", "OK");

    private async void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        var accepted = await _dialogs.ConfirmAsync("Conferma", "Confermi?", "Sì", "No");
        ResultText.Text = $"ConfirmAsync → {accepted}";
    }

    private async void OnOpenLinkClick(object sender, RoutedEventArgs e) =>
        await _urlLauncher.OpenAsync(new Uri("https://www.azuracast.com/"));

    private async void OnBackgroundDispatchClick(object sender, RoutedEventArgs e)
    {
        var result = await Task.Run(async () =>
        {
            var before = _dispatcher.IsUiThread;
            var inside = false;
            await _dispatcher.InvokeAsync(() => inside = _dispatcher.IsUiThread);
            return (before, inside);
        });
        ResultText.Text = $"Thread di fondo: IsUiThread={result.before}; dentro InvokeAsync: IsUiThread={result.inside}";
    }
}
