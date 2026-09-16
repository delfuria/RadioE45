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

    // AzuraCast risponde 403 sia per richieste disabilitate sia per il cooldown ("aspetta X
    // minuti"): il corpo (Api_Error.message) è l'unico modo per distinguere i due casi in UI.
    private static async Task<string> ExtractMessageAsync(ApiException ex)
    {
        try
        {
            AzuraCastApiError? error = await ex.GetContentAsAsync<AzuraCastApiError>();
            if (!string.IsNullOrWhiteSpace(error?.Message))
                return error.Message;
        }
        catch
        {
            // Corpo non nel formato atteso — usa il messaggio generico sotto.
        }

        return LocalizationResourceManager.Instance["Err_SubmitRequest"];
    }

    private IAzuraCastRequestApi CreateApi(AzuraStation station)
    {
        HttpClient client = _httpClientFactory.CreateClient("AzuraCast");
        client.BaseAddress = new Uri($"{UrlBaseHelper.EnsureScheme(station.UrlBase)}/api/station");
        return RestService.For<IAzuraCastRequestApi>(client);
    }
}
