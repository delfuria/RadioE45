using Android.OS;
using AndroidX.Concurrent.Futures;
using AndroidX.Media3.Common;
using AndroidX.Media3.Session;
using Google.Common.Util.Concurrent;
using Java.Interop;
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

    // Allowlist ereditata da RadioMediaBrowserService, estesa in Fase 3.6 con i pacchetti
    // richiesti da CarAppQuality.md/piano originale (Fase 2D). Restano qui come rete di
    // sicurezza esplicita anche se IsAutomotiveController/IsAutoCompanionController
    // (introdotti nel fix OnConnect post-3.5) e controller.IsTrusted dovrebbero già
    // riconoscere buona parte di questi client come fidati: è la stessa prassi difensiva
    // usata dai sample ufficiali Google (il flag "trusted" del sistema non è sempre
    // affidabile su tutte le versioni/skin Android), quindi i due meccanismi restano
    // complementari e non si eliminano a vicenda.
    private static readonly HashSet<string> AllowedCallers = new(StringComparer.Ordinal)
    {
        "com.google.android.projection.gearhead",
        "com.google.android.mediasimulator",
        "com.google.android.carassistant",
        "com.android.bluetooth",
        "com.google.android.wearable.app",
    };

    private static IAzuraStationCatalog? Catalog =>
        IPlatformApplication.Current?.Services?.GetService<IAzuraStationCatalog>();

    private static IStreamUrlProber? Prober =>
        IPlatformApplication.Current?.Services?.GetService<IStreamUrlProber>();

    private static ILogger<RadioLibrarySessionCallback>? Logger =>
        IPlatformApplication.Current?.Services?.GetService<ILogger<RadioLibrarySessionCallback>>();

    public MediaSession.ConnectionResult OnConnect(MediaSession? session, MediaSession.ControllerInfo? controller)
    {
        if (session is null || controller is null)
            return MediaSession.ConnectionResult.Reject()!;

        // Fix (2026-07-02, scoperto testando la 3.5 su emulatore: "Session rejected the
        // connection request", nessun audio): il controller interno che Media3 crea per
        // gestire la notifica (MediaNotificationManager) e il bridge della nostra stessa
        // app (AndroidMedia3AudioService, §3.5, in-process nello stesso pacchetto) NON sono
        // "trusted" per definizione — vanno riconosciuti esplicitamente con le API dedicate
        // di MediaSession, altrimenti sia la notifica di sistema sia l'app stessa restano
        // escluse dal controllo della propria riproduzione. I controller "trusted" (BT,
        // lock screen, ecc.) e l'allowlist di pacchetti esterni (gearhead/mediasimulator)
        // restano com'erano, stesso principio di RadioMediaBrowserService.OnGetRoot.
        bool isSelfOrSystem = session.IsMediaNotificationController(controller) ||
            session.IsAutomotiveController(controller) ||
            session.IsAutoCompanionController(controller) ||
            controller.PackageName == Android.App.Application.Context.PackageName;

        bool isAllowed = isSelfOrSystem ||
            controller.IsTrusted ||
            AllowedCallers.Contains(controller.PackageName ?? string.Empty);

        if (!isAllowed)
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

        // Fase 3.6: content-style hint (equivalente Media3 delle chiavi legacy
        // androidx.media.utils.MediaConstants — pacchetto già presente in albero come
        // dipendenza transitiva di Media3.Session, vedi §3.1/§11). Qualificato per intero
        // (AndroidX.Media.Utils.MediaConstants) perché Media3.Session ha una propria classe
        // omonima MediaConstants con scopo diverso: uno using avrebbe reso l'uso ambiguo
        // (CS0104, verificato in build). Le stazioni sono un elenco piatto (nessuna
        // sotto-cartella), quindi si dichiara stile "lista" (icona + titolo) sia per gli
        // eventuali figli browsable sia per quelli playable, coerente con la convenzione
        // degli altri client media/radio su Android Auto.
        Bundle rootExtras = new();
        rootExtras.PutInt(
            AndroidX.Media.Utils.MediaConstants.DescriptionExtrasKeyContentStyleBrowsable,
            AndroidX.Media.Utils.MediaConstants.DescriptionExtrasValueContentStyleListItem);
        rootExtras.PutInt(
            AndroidX.Media.Utils.MediaConstants.DescriptionExtrasKeyContentStylePlayable,
            AndroidX.Media.Utils.MediaConstants.DescriptionExtrasValueContentStyleListItem);

        // Si preservano gli eventuali flag Offline/Recent/Suggested della richiesta del
        // client (echo, come faceva il codice originale passando libraryParams invariato):
        // qui si aggiungono solo gli extra di content-style, senza scartare il resto.
        LibraryParams.Builder rootParamsBuilder = new LibraryParams.Builder().SetExtras(rootExtras)!;
        if (libraryParams is not null)
        {
            rootParamsBuilder = rootParamsBuilder
                .SetOffline(libraryParams.IsOffline)!
                .SetRecent(libraryParams.IsRecent)!
                .SetSuggested(libraryParams.IsSuggested)!;
        }

        return Immediate(LibraryResult.OfItem(rootItem, rootParamsBuilder.Build()!)!);
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

    // Fase 3.7 (VC-1, comandi vocali Gemini/Google Assistant): Media3 non ha un
    // OnPlayFromSearch — a differenza di quanto ipotizzato inizialmente in questo piano
    // (§3.7 originale). Un comando vocale ("riproduci X su RadioE45") arriva qui come
    // MediaItem con MediaId assente/non risolvibile ma con RequestMetadata.SearchQuery
    // valorizzata: è Media3 stesso a tradurre internamente la chiamata legacy
    // playFromSearch (quella che Assistant/Gemini invocano davvero) in questa forma, prima
    // di passarla a OnAddMediaItems/OnSetMediaItems — cioè qui.
    private static async Task<MediaItem?> TryResolveAsync(MediaItem item)
    {
        IAzuraStationCatalog? catalog = Catalog;

        AzuraStation? station = null;
        if (int.TryParse(item.MediaId, out int stationId))
            station = catalog?.Stations.FirstOrDefault(s => s.Id == stationId);

        if (station is null)
        {
            string? query = TryGetSearchQuery(item);
            // query non-null significa "RequestMetadata presente" (quindi è una vera
            // richiesta di ricerca/vocale), anche se il testo della ricerca è vuoto — vedi
            // TryGetSearchQuery. Se RequestMetadata non c'è affatto, query è null e si
            // mantiene la tolleranza "no-op silenzioso" già esistente (return null sotto).
            if (query is not null)
            {
                if (catalog is not null && catalog.Stations.Count == 0)
                    await catalog.LoadAsync();

                if (query.Length == 0)
                {
                    // Query vocale generica ("riproduci qualcosa"): stessa euristica già
                    // usata da OnPlaybackResumption/dal vecchio AutoMediaCallback.OnPlay —
                    // preferita, altrimenti la prima disponibile.
                    station = catalog?.GetFavorite() ?? catalog?.GetFirst();
                }
                else
                {
                    station = catalog?.Stations.FirstOrDefault(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
                    if (station is null)
                    {
                        Logger?.LogWarning("RadioLibrarySessionCallback: nessuna stazione corrisponde alla ricerca vocale '{Query}', uso la prima disponibile", query);
                        station = catalog?.GetFirst();
                    }
                }
            }
        }

        if (station is null)
            return null;

        MediaItem? resolved = await ResolveStationAsync(station);
        return resolved?.BuildUpon()!.SetMediaId(station.Id.ToString())!.Build();
    }

    // Bug del binding (verificato con `strings` sull'assembly Xamarin.AndroidX.Media3.Common:
    // esiste `get_MediaId`/`get_MediaMetadata` ma NESSUN `get_RequestMetadata`/
    // `getRequestMetadata`, pur essendo presente `MediaItem.Builder.SetRequestMetadata`): il
    // binding C# non espone un getter per il campo pubblico Java `MediaItem.requestMetadata`.
    // Si legge quindi il campo con la riflessione Java standard (Class.GetField + Field.Get)
    // e si effettua il cast al tipo bindato RequestMetadata, i cui accessor (incluso
    // SearchQuery) SONO bindati correttamente. Ritorna null se RequestMetadata non è
    // impostata (nessuna richiesta di ricerca), stringa vuota se impostata ma senza testo
    // (ricerca generica), altrimenti il testo della ricerca.
    private static string? TryGetSearchQuery(MediaItem item)
    {
        try
        {
            Java.Lang.Reflect.Field? field = item.Class?.GetField("requestMetadata");
            Java.Lang.Object? raw = field?.Get(item);
            MediaItem.RequestMetadata? metadata = raw?.JavaCast<MediaItem.RequestMetadata>();
            return metadata?.SearchQuery;
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "RadioLibrarySessionCallback: lettura di RequestMetadata.SearchQuery fallita (workaround binding)");
            return null;
        }
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
