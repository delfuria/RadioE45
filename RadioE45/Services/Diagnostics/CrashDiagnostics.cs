using Microsoft.Maui.Storage;

namespace RadioE45.Services.Diagnostics;

// Su MacCatalyst Sentry non è disponibile (vedi MauiProgram.cs, #if !MACCATALYST) e, prima di
// questa classe, l'app non aveva alcun modo di osservare un'eccezione che sfugge da un Task
// "fire-and-forget" mai atteso: Mono la considera non gestita e abortisce l'intero processo
// (SIGABRT) sul thread Finalizer al momento della garbage collection del Task fallito, senza
// lasciare traccia dell'eccezione reale nel crash report nativo.
public static class CrashDiagnostics
{
    private const string LogFileName = "crash-diagnostics.log";
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log("AppDomain.UnhandledException", e.ExceptionObject as Exception, e.IsTerminating);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log("TaskScheduler.UnobservedTaskException", e.Exception, isTerminating: false);

            // Senza questa chiamata, Mono tratta l'eccezione come non gestita e abortisce il
            // processo quando il GC finalizza il Task fallito, anche se riguarda un'operazione
            // in background non critica per l'utente.
            e.SetObserved();
        };
    }

    public static void LogHandledException(string context, Exception ex) =>
        Log(context, ex, isTerminating: false);

    private static void Log(string source, Exception? ex, bool isTerminating)
    {
        try
        {
            string path = Path.Combine(FileSystem.AppDataDirectory, LogFileName);
            string entry =
                $"---- {DateTimeOffset.Now:O} | {source} | IsTerminating={isTerminating} ----{Environment.NewLine}" +
                $"{ex?.ToString() ?? "(nessun oggetto eccezione)"}{Environment.NewLine}{Environment.NewLine}";

            File.AppendAllText(path, entry);
        }
        catch
        {
            // Il logging diagnostico non deve mai essere causa di un crash aggiuntivo.
        }
    }
}
