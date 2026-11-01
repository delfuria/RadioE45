using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Data;
using RadioE45.Services.Localization;
using RadioE45.Services.Radio;

namespace RadioE45.ViewModels;

public partial class SongRequestViewModel : BaseViewModel
{
    private const int DefaultPageSize = 10;
    private static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(300);

    private CancellationTokenSource? _searchCts;

    private readonly ISongRequestService _songRequestService;
    private readonly IAppSettingsRepository _settingsRepo;
    private readonly OnAirViewModel _onAirViewModel;

    // Letto dalle impostazioni a ogni caricamento — vedi LoadSongsAsync.
    private int _pageSize = DefaultPageSize;

    // Catalogo completo scaricato dal server: Songs è la pagina corrente del sottoinsieme
    // filtrato (_filtered), calcolata client-side — l'endpoint AzuraCast non offre search/paginazione.
    private List<AzuraCastRequestItem> _allSongs = [];
    private List<AzuraCastRequestItem> _filtered = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<AzuraCastRequestItem> Songs { get; set; } = [];

    [ObservableProperty]
    public partial int PageNumber { get; set; } = 1;

    [ObservableProperty]
    public partial int TotalPages { get; set; } = 1;

    // Distingue "nessun brano richiedibile" da "il filtro esclude tutti i brani".
    [ObservableProperty]
    public partial string EmptyResultsText { get; set; } = string.Empty;

    public bool CanGoToPreviousPage => PageNumber > 1;
    public bool CanGoToNextPage => PageNumber < TotalPages;

    public SongRequestViewModel(
        ISongRequestService songRequestService,
        IAppSettingsRepository settingsRepo,
        OnAirViewModel onAirViewModel,
        ILogger<SongRequestViewModel> logger)
    {
        Logger = logger;
        _songRequestService = songRequestService;
        _settingsRepo = settingsRepo;
        _onAirViewModel = onAirViewModel;
    }

    [RelayCommand]
    private async Task LoadSongsAsync()
    {
        AppSettings settings = await _settingsRepo.GetAsync();
        _pageSize = settings.RequestPageSize > 0 ? settings.RequestPageSize : DefaultPageSize;

        AzuraStation? station = _onAirViewModel.CurrentStation;
        if (station is null)
        {
            _allSongs = [];
            ApplyFilter();
            return;
        }

        await SafeExecuteAsync(async () =>
        {
            List<AzuraCastRequestItem> items = await _songRequestService.GetRequestableSongsAsync(station);
            foreach (AzuraCastRequestItem item in items)
                item.RequestCommand = RequestSongCommand;

            // I jingle sono brani della libreria ma non vanno proposti tra le richieste.
            _allSongs = items.Where(i => !i.Title.Trim().Equals("jingle", StringComparison.OrdinalIgnoreCase)).ToList();
            // Shuffled at every load so the same songs don't always lead the list
            Random.Shared.Shuffle(CollectionsMarshal.AsSpan(_allSongs));
            PageNumber = 1;
            ApplyFilter();
        }, LocalizationResourceManager.Instance["Err_LoadRequestableSongs"]);

        // Caricamento fallito: la vista vuota deve comunque avere il suo testo.
        if (ErrorMessage is not null)
        {
            _allSongs = [];
            ApplyFilter();
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        // Debounce: digitando non si rigenera l'elenco a ogni tasto (ogni rigenerazione annulla i download delle copertine).
        _searchCts?.Cancel();
        CancellationTokenSource cts = _searchCts = new CancellationTokenSource();
        _ = ApplySearchAsync(cts.Token);
    }

    private async Task ApplySearchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SearchDebounce, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        PageNumber = 1;
        ApplyFilter();
    }

    partial void OnPageNumberChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoToPreviousPage));
        OnPropertyChanged(nameof(CanGoToNextPage));
    }

    // TotalPages cambia da ApplyFilter mentre PageNumber spesso resta invariato (es. 1 -> 1): senza
    // questo, CanGoToNextPage non viene mai ri-notificato dopo il primo caricamento e il bottone
    // "avanti" resta disabilitato anche con più pagine disponibili.
    partial void OnTotalPagesChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoToPreviousPage));
        OnPropertyChanged(nameof(CanGoToNextPage));
    }

    private void ApplyFilter()
    {
        IEnumerable<AzuraCastRequestItem> query = _allSongs;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(s =>
                s.Artist.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                s.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        _filtered = query.ToList();
        TotalPages = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)_pageSize));
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);

        EmptyResultsText = LocalizationResourceManager.Instance[
            _allSongs.Count == 0 ? "Request_NoSongs" : "Request_NoSongsFiltered"];

        ApplyPage();
    }

    private void ApplyPage()
    {
        // La collezione resta la stessa: il CollectionView aggiorna solo le righe che cambiano,
        // senza ricreare le celle (e quindi senza annullare i download delle copertine).
        SyncSongs(_filtered.Skip((PageNumber - 1) * _pageSize).Take(_pageSize).ToList());
    }

    // Porta Songs allo stato di target con il minimo di modifiche (rimuove, sposta, inserisce).
    // Il confronto è per riferimento: gli elementi sono gli stessi oggetti di _allSongs.
    private void SyncSongs(IReadOnlyList<AzuraCastRequestItem> target)
    {
        var keep = new HashSet<AzuraCastRequestItem>(target, ReferenceEqualityComparer.Instance);
        for (int i = Songs.Count - 1; i >= 0; i--)
        {
            if (!keep.Contains(Songs[i]))
                Songs.RemoveAt(i);
        }

        for (int i = 0; i < target.Count; i++)
        {
            AzuraCastRequestItem item = target[i];
            if (i < Songs.Count && ReferenceEquals(Songs[i], item))
                continue;

            int current = -1;
            for (int j = i; j < Songs.Count; j++)
            {
                if (ReferenceEquals(Songs[j], item))
                {
                    current = j;
                    break;
                }
            }

            if (current >= 0)
                Songs.Move(current, i);
            else
                Songs.Insert(i, item);
        }
    }

    [RelayCommand]
    private void DismissError() => ErrorMessage = null;

    [RelayCommand]
    private void PreviousPage()
    {
        if (!CanGoToPreviousPage)
            return;

        PageNumber--;
        ApplyPage();
    }

    [RelayCommand]
    private void NextPage()
    {
        if (!CanGoToNextPage)
            return;

        PageNumber++;
        ApplyPage();
    }

    // AllowConcurrentExecutions: il comando è UNA sola istanza condivisa da tutte le righe della
    // lista (CommandParameter = riga). Col default (false), CommunityToolkit.Mvvm considera il
    // comando "occupato" per l'intera durata di QUALSIASI richiesta in corso e MAUI sincronizza
    // automaticamente Button.IsEnabled sul CanExecute del comando — indipendentemente dal binding
    // esplicito su IsRequested. Risultato: durante/dopo una richiesta (anche fallita) TUTTI i
    // bottoni, incluso quello del brano appena rifiutato, restavano visivamente disabilitati/scuriti
    // come se fossero stati richiesti con successo. Con true, lo stato disabilitato è governato solo
    // dal nostro IsRequested — cambia SOLO quando la richiesta va davvero a buon fine.
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RequestSongAsync(AzuraCastRequestItem item)
    {
        AzuraStation? station = _onAirViewModel.CurrentStation;
        if (station is null || item.IsRequesting || item.IsRequested)
            return;

        item.IsRequesting = true;
        ErrorMessage = null;
        try
        {
            await _songRequestService.SubmitRequestAsync(station, item.RequestId);
            item.IsRequested = true;
        }
        catch (SongRequestException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = LocalizationResourceManager.Instance["Err_SubmitRequest"];
            Logger.LogError(ex, "Unexpected error submitting song request");
        }
        finally
        {
            item.IsRequesting = false;
        }
    }
}
