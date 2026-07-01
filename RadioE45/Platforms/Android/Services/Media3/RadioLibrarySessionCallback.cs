using AndroidX.Concurrent.Futures;
using AndroidX.Media3.Common;
using AndroidX.Media3.Session;
using Google.Common.Util.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Radio;
using static AndroidX.Media3.Session.MediaLibraryService;

namespace RadioE45;

// Fase 3 (Media3/Android Auto) — vedi docs/carplay/Phase3-Media3-ActionPlan.md §3.3.
// Sostituisce RadioMediaBrowserService.AutoMediaCallback (legacy). A differenza dell'API
// legacy, qui non esistono OnPlay/OnPause/OnStop: quei comandi arrivano direttamente al
// Player (ExoPlayer, vedi RadioPlayerFactory) tramite la sessione. Questo callback si
// occupa solo di: chi può connettersi (OnConnect), l'albero di navigazione (OnGetLibraryRoot/
// OnGetChildren) e la risoluzione di un mediaId "logico" in un MediaItem realmente
// riproducibile con URL già verificato (OnAddMediaItems/OnSetMediaItems/OnPlaybackResumption).
internal sealed class RadioLibrarySessionCallback : Java.Lang.Object, MediaLibrarySession.ICallback
{
    private const string RootId = "ROOT";

    // Allowlist di base ereditata da RadioMediaBrowserService. L'estensione con
    // com.google.android.carassistant/com.android.bluetooth/com.google.android.wearable.app
    // è compito della Fase 3.6, non di questa.
    private static readonly HashSet<string> AllowedCallers = new(StringComparer.Ordinal)
    {
        "com.google.android.projection.gearhead",
        "com.google.android.mediasimulator",
    };

    private static IAzuraStationCatalog? Catalog =>
        IPlatformApplication.Current?.Services?.GetService<IAzuraStationCatalog>();

    private static IStreamUrlProber? Prober =>
        IPlatformApplication.Current?.Services?.GetService<IStreamUrlProber>();

    private static ILogger<RadioLibrarySessionCallback>? Logger =>
        IPlatformApplication.Current?.Services?.GetService<ILogger<RadioLibrarySessionCallback>>();

    public MediaSession.ConnectionResult OnConnect(MediaSession? session, MediaSession.ControllerInfo? controller)
    {
        // I controller "trusted" (notifica di sistema, Bluetooth, lock screen) devono sempre
        // passare: solo i client di terze parti non fidati sono soggetti all'allowlist —
        // stesso principio di RadioMediaBrowserService.OnGetRoot, applicato qui a livello di
        // connessione perché Media3 unifica browsing e transport control in un'unica sessione.
        bool isAllowed = controller is not null &&
            (controller.IsTrusted || AllowedCallers.Contains(controller.PackageName ?? string.Empty));
        if (!isAllowed || session is null)
            return MediaSession.ConnectionResult.Reject()!;

        return new MediaSession.ConnectionResult.AcceptedResultBuilder(session).Build()!;
    }

    public IListenableFuture OnGetLibraryRoot(MediaLibrarySession? session, MediaSession.ControllerInfo? browser, LibraryParams? libraryParams)
    {
        // SetFolderType è deprecato in questa versione di Media3: SetMediaType con
        // MediaTypeFolderMixed è la sostituzione indicata dalla libreria stessa.
        MediaMetadata rootMetadata = new MediaMetadata.Builder()
            .SetMediaType(Java.Lang.Integer.ValueOf(MediaMetadata.MediaTypeFolderMixed))!
            .SetIsBrowsable(Java.Lang.Boolean.ValueOf(true))!
            .SetIsPlayable(Java.Lang.Boolean.ValueOf(false))!
            .Build()!;

        MediaItem rootItem = new MediaItem.Builder()
            .SetMediaId(RootId)!
            .SetMediaMetadata(rootMetadata)!
            .Build()!;

        return Immediate(LibraryResult.OfItem(rootItem, libraryParams)!);
    }

    public IListenableFuture OnGetChildren(
        MediaLibrarySession? session, MediaSession.ControllerInfo? browser, string? parentId, int page, int pageSize, LibraryParams? libraryParams)
    {
        if (parentId != RootId)
            return Immediate(LibraryResult.OfItemList(new List<MediaItem>(), libraryParams)!);

        // Nessun supporto a paginazione reale (page/pageSize ignorati): stesso comportamento
        // "lista intera in un colpo" di RadioMediaBrowserService.SendStationsAsync.
        return CallbackToFutureAdapter.GetFuture(new ResolverAdapter(async completer =>
        {
            try
            {
                IAzuraStationCatalog? catalog = Catalog;
                if (catalog is null)
                {
                    completer.Set(LibraryResult.OfItemList(new List<MediaItem>(), libraryParams));
                    return;
                }

                if (catalog.Stations.Count == 0)
                    await catalog.LoadAsync();

                List<MediaItem> items = catalog.Stations.Select(BuildBrowsableStationItem).ToList();
                completer.Set(LibraryResult.OfItemList(items, libraryParams));
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "RadioLibrarySessionCallback: errore in OnGetChildren");
                completer.Set(LibraryResult.OfError(SessionError.ErrorUnknown));
            }
        }))!;
    }

    public IListenableFuture OnAddMediaItems(MediaSession? mediaSession, MediaSession.ControllerInfo? controller, IList<MediaItem>? mediaItems)
        => CallbackToFutureAdapter.GetFuture(new ResolverAdapter(async completer =>
        {
            IList<MediaItem> resolved = await ResolveMediaItemsAsync(mediaItems ?? new List<MediaItem>());

            // Completer.Set(Java.Lang.Object) non marshalla automaticamente una
            // System.Collections.Generic.IList<T>: va passata una collezione Java reale.
            Java.Util.ArrayList javaList = new();
            foreach (MediaItem item in resolved)
                javaList.Add(item);
            completer.Set(javaList);
        }))!;

    public IListenableFuture OnSetMediaItems(
        MediaSession? mediaSession, MediaSession.ControllerInfo? controller, IList<MediaItem>? mediaItems, int startIndex, long startPositionMs)
        => CallbackToFutureAdapter.GetFuture(new ResolverAdapter(async completer =>
        {
            IList<MediaItem> resolved = await ResolveMediaItemsAsync(mediaItems ?? new List<MediaItem>());
            completer.Set(new MediaSession.MediaItemsWithStartPosition(resolved, startIndex, startPositionMs));
        }))!;

    public IListenableFuture OnPlaybackResumption(MediaSession? mediaSession, MediaSession.ControllerInfo? controller)
        => CallbackToFutureAdapter.GetFuture(new ResolverAdapter(async completer =>
        {
            IAzuraStationCatalog? catalog = Catalog;
            if (catalog is not null && catalog.Stations.Count == 0)
                await catalog.LoadAsync();

            // Nessuno storico "ultima stazione riprodotta" a questo livello (arriverà con il
            // bridge della Fase 3.5): stesso fallback già usato da AutoMediaCallback.OnPlay
            // nel codice legacy — preferita, altrimenti la prima disponibile.
            AzuraStation? station = catalog?.GetFavorite() ?? catalog?.GetFirst();
            if (station is null)
            {
                completer.SetException(new Java.Lang.UnsupportedOperationException("Nessuna stazione disponibile per la ripresa"));
                return;
            }

            MediaItem? resolved = await ResolveStationAsync(station);
            if (resolved is null)
            {
                completer.SetException(new Java.Lang.UnsupportedOperationException($"Nessun URL raggiungibile per la stazione {station.Id}"));
                return;
            }

            completer.Set(new MediaSession.MediaItemsWithStartPosition(new List<MediaItem> { resolved }, 0, 0L));
        }))!;

    // MediaItem "leggero" per l'albero di navigazione: solo metadati, nessun URL audio
    // ancora risolto (stesso comportamento di RadioMediaBrowserService.SendStationsAsync).
    private static MediaItem BuildBrowsableStationItem(AzuraStation station)
    {
        MediaMetadata.Builder metadataBuilder = new MediaMetadata.Builder()
            .SetTitle(station.Name)!
            .SetSubtitle(station.Description)!
            .SetStation(station.Name)!
            .SetIsBrowsable(Java.Lang.Boolean.ValueOf(false))!
            .SetIsPlayable(Java.Lang.Boolean.ValueOf(true))!
            .SetMediaType(Java.Lang.Integer.ValueOf(MediaMetadata.MediaTypeRadioStation))!;

        if (!string.IsNullOrEmpty(station.LogoUrl))
            metadataBuilder = metadataBuilder.SetArtworkUri(Android.Net.Uri.Parse(station.LogoUrl))!;

        return new MediaItem.Builder()
            .SetMediaId(station.Id.ToString())!
            .SetMediaMetadata(metadataBuilder.Build())!
            .Build()!;
    }

    // Risolve un elenco di MediaItem "logici" (solo MediaId, es. dal browse tree o da
    // OnPlayFromSearch/voce) in MediaItem realmente riproducibili con un URL verificato.
    // Porta la stessa logica di selezione candidati di AudioService.TryOpenStreamAsync,
    // tramite l'IStreamUrlProber condiviso (vedi §3.2/§11 del piano). Un item che non si
    // riesce a risolvere viene passato invariato: ExoPlayer fallirà quella singola voce
    // senza bloccare le altre, stessa tolleranza del vecchio OnPlayFromMediaId (no-op).
    private static async Task<IList<MediaItem>> ResolveMediaItemsAsync(IList<MediaItem> requested)
    {
        var resolved = new List<MediaItem>(requested.Count);
        foreach (MediaItem item in requested)
        {
            MediaItem? candidate = await TryResolveAsync(item);
            resolved.Add(candidate ?? item);
        }
        return resolved;
    }

    private static async Task<MediaItem?> TryResolveAsync(MediaItem item)
    {
        if (!int.TryParse(item.MediaId, out int stationId))
            return null;

        AzuraStation? station = Catalog?.Stations.FirstOrDefault(s => s.Id == stationId);
        if (station is null)
            return null;

        MediaItem? resolved = await ResolveStationAsync(station);
        return resolved?.BuildUpon()!.SetMediaId(item.MediaId)!.Build();
    }

    private static async Task<MediaItem?> ResolveStationAsync(AzuraStation station)
    {
        IStreamUrlProber? prober = Prober;
        if (prober is null)
            return null;

        string[] candidates = new[] { station.OnAirStreamUrl, station.HlsUrl, station.StreamUrl, station.StreamUrlFallback }
            .Where(u => !string.IsNullOrEmpty(u))
            .Distinct()
            .ToArray()!;

        if (candidates.Length == 0)
            return null;

        string? winner = await prober.ProbeFirstReachableAsync(candidates, CancellationToken.None);
        if (winner is null)
        {
            Logger?.LogWarning("RadioLibrarySessionCallback: nessun URL raggiungibile per la stazione {StationId}", station.Id);
            return null;
        }

        MediaItem browsableItem = BuildBrowsableStationItem(station);
        return browsableItem.BuildUpon()!.SetUri(winner)!.Build();
    }

    // Futures.immediateFuture (Guava) non è esposto in questo binding; ResolvableFuture lo
    // sarebbe ma Google la marca "internal API, use at your own risk" — si usa quindi lo
    // stesso ponte CallbackToFutureAdapter già impiegato per i risultati asincroni, con un
    // resolver che completa subito, in modo sincrono.
    private static IListenableFuture Immediate(Java.Lang.Object value)
        => CallbackToFutureAdapter.GetFuture(new ResolverAdapter(completer =>
        {
            completer.Set(value);
            return Task.CompletedTask;
        }))!;

    // Adatta un delegate C# async a AndroidX.Concurrent.Futures.CallbackToFutureAdapter,
    // il ponte standard AndroidX fra codice asincrono e IListenableFuture.
    private sealed class ResolverAdapter : Java.Lang.Object, CallbackToFutureAdapter.IResolver
    {
        private readonly Func<CallbackToFutureAdapter.Completer, Task> _resolve;

        public ResolverAdapter(Func<CallbackToFutureAdapter.Completer, Task> resolve) => _resolve = resolve;

        public Java.Lang.Object? AttachCompleter(CallbackToFutureAdapter.Completer? completer)
        {
            if (completer is not null)
                _ = RunAsync(completer);
            return null;
        }

        private async Task RunAsync(CallbackToFutureAdapter.Completer completer)
        {
            try
            {
                await _resolve(completer);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "RadioLibrarySessionCallback: errore non gestito nella risoluzione async");
                completer.SetException(new Java.Lang.RuntimeException(ex.Message));
            }
        }
    }
}
