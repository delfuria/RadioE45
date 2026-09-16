using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Localization;
using RadioE45.Services.Radio;

namespace RadioE45.ViewModels;

public partial class SongRequestViewModel : BaseViewModel
{
    private const int PageSize = 10;

    private readonly ISongRequestService _songRequestService;
    private readonly OnAirViewModel _onAirViewModel;

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
        OnAirViewModel onAirViewModel,
        ILogger<SongRequestViewModel> logger)
    {
        Logger = logger;
        _songRequestService = songRequestService;
        _onAirViewModel = onAirViewModel;
    }

    [RelayCommand]
    private async Task LoadSongsAsync()
    {
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

            _allSongs = items;
            PageNumber = 1;
            ApplyFilter();
        }, LocalizationResourceManager.Instance["Err_LoadRequestableSongs"]);
    }

    partial void OnSearchTextChanged(string value)
    {
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
        TotalPages = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);

        EmptyResultsText = LocalizationResourceManager.Instance[
            _allSongs.Count == 0 ? "Request_NoSongs" : "Request_NoSongsFiltered"];

        ApplyPage();
    }

    private void ApplyPage()
    {
        Songs = new ObservableCollection<AzuraCastRequestItem>(
            _filtered.Skip((PageNumber - 1) * PageSize).Take(PageSize));
    }

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
