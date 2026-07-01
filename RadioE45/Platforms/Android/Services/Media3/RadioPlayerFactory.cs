using Android.Content;
using AndroidX.Media3.Common;
using AndroidX.Media3.DataSource;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.ExoPlayer.Source;

namespace RadioE45;

// Fase 3 (Media3/Android Auto) — vedi docs/carplay/Phase3-Media3-ActionPlan.md §3.2.
// Costruisce l'unico ExoPlayer nativo che vive dentro RadioLibraryService, indipendente
// da qualunque Activity/Page MAUI. La selezione del URL migliore fra i candidati di una
// stazione (OnAirStreamUrl/HlsUrl/StreamUrl/StreamUrlFallback) resta logica di business e
// va fatta a monte, in AndroidMedia3AudioService (fase 3.5), non qui.
internal static class RadioPlayerFactory
{
    // Stessi valori usati oggi in AudioService per il probing (3s) e per il watchdog di
    // buffering (12s): qui configurano il timeout HTTP della DataSource di ExoPlayer,
    // non la logica di probing/riconnessione applicativa.
    private const int ConnectTimeoutMs = 8000;
    private const int ReadTimeoutMs = 8000;

    public static IExoPlayer CreatePlayer(Context context)
    {
        AudioAttributes audioAttributes = new AudioAttributes.Builder()
            .SetUsage(C.UsageMedia)!
            .SetContentType(C.AudioContentTypeMusic)!
            .Build()!;

        DefaultHttpDataSource.Factory httpDataSourceFactory = new DefaultHttpDataSource.Factory()
            .SetConnectTimeoutMs(ConnectTimeoutMs)!
            .SetReadTimeoutMs(ReadTimeoutMs)!
            .SetAllowCrossProtocolRedirects(true)!;

        DefaultMediaSourceFactory mediaSourceFactory = new DefaultMediaSourceFactory(httpDataSourceFactory);

        // ExoPlayer gestisce nativamente focus audio (assorbe fix #9), "becoming noisy"
        // su BT/cuffie disconnesse (assorbe fix #10) e wake lock di rete in background.
        return new ExoPlayerBuilder(context)
            .SetAudioAttributes(audioAttributes, handleAudioFocus: true)!
            .SetHandleAudioBecomingNoisy(true)!
            .SetWakeMode(C.WakeModeNetwork)!
            .SetMediaSourceFactory(mediaSourceFactory)!
            .Build()!;
    }
}
