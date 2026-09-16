using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Localization;
using Refit;

namespace RadioE45.Services.Radio;

public class SongRequestService : ISongRequestService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SongRequestService> _logger;

    public SongRequestService(IHttpClientFactory httpClientFactory, ILogger<SongRequestService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<List<AzuraCastRequestItem>> GetRequestableSongsAsync(AzuraStation station, CancellationToken ct = default)
    {
        try
        {
            IAzuraCastRequestApi api = CreateApi(station);
            return await api.GetRequestableSongsAsync(station.StationId, ct);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "API error fetching requestable songs for station {StationId}", station.StationId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching requestable songs for station {StationId}", station.StationId);
            throw;
        }
    }

    public async Task SubmitRequestAsync(AzuraStation station, string requestId, CancellationToken ct = default)
    {
        try
        {
            IAzuraCastRequestApi api = CreateApi(station);
            await api.SubmitSongRequestAsync(station.StationId, requestId, ct);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "API error submitting request {RequestId} for station {StationId}", requestId, station.StationId);
            throw new SongRequestException(await ExtractMessageAsync(ex));
        }
    }

    // AzuraCast risponde 500 sia per il cooldown ("aspetta X minuti") sia per altri rifiuti (brano
    // già in coda, non richiedibile, IP bloccato...): il corpo (Api_Error.message) è l'unico modo
    // per distinguere i casi in UI. Il server risponde sempre in inglese (nessuna negoziazione
    // lingua sull'API pubblica), quindi qui si riconoscono i testi fissi di questa versione di
    // AzuraCast e si sostituiscono con le stringhe localizzate dell'app.
    private static async Task<string> ExtractMessageAsync(ApiException ex)
    {
        try
        {
            AzuraCastApiError? error = await ex.GetContentAsAsync<AzuraCastApiError>();
            if (!string.IsNullOrWhiteSpace(error?.Message))
                return LocalizeServerMessage(error.Message);
        }
        catch
        {
            // Corpo non nel formato atteso — usa il messaggio generico sotto.
        }

        return LocalizationResourceManager.Instance["Err_SubmitRequest"];
    }

    private static string LocalizeServerMessage(string serverMessage)
    {
        Match minutesMatch = Regex.Match(serverMessage, @"wait\s+([\d.]+)\s+minutes?", RegexOptions.IgnoreCase);
        if (minutesMatch.Success)
            return LocalizationResourceManager.Instance.Format("Request_Err_TooRecent", minutesMatch.Groups[1].Value);

        if (serverMessage.Contains("already requested", StringComparison.OrdinalIgnoreCase))
            return LocalizationResourceManager.Instance["Request_Err_AlreadyPending"];

        if (serverMessage.Contains("played too recently", StringComparison.OrdinalIgnoreCase))
            return LocalizationResourceManager.Instance["Request_Err_PlayedRecently"];

        if (serverMessage.Contains("not requestable", StringComparison.OrdinalIgnoreCase))
            return LocalizationResourceManager.Instance["Request_Err_NotRequestable"];

        if (serverMessage.Contains("not permitted to submit", StringComparison.OrdinalIgnoreCase))
            return LocalizationResourceManager.Instance["Request_Err_NotPermitted"];

        if (serverMessage.Contains("crawlers are not permitted", StringComparison.OrdinalIgnoreCase))
            return LocalizationResourceManager.Instance["Request_Err_Bot"];

        return LocalizationResourceManager.Instance["Err_SubmitRequest"];
    }

    private IAzuraCastRequestApi CreateApi(AzuraStation station)
    {
        HttpClient client = _httpClientFactory.CreateClient("AzuraCast");
        client.BaseAddress = new Uri($"{UrlBaseHelper.EnsureScheme(station.UrlBase)}/api/station");
        return RestService.For<IAzuraCastRequestApi>(client);
    }
}
