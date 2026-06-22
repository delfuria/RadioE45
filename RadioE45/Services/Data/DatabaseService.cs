using RadioE45.Models;
using SQLite;
using Microsoft.Maui.Storage;

namespace RadioE45.Services.Data;

public class DatabaseService : IDatabaseService, IAsyncDisposable
{
    // Incrementa questo valore ogni volta che i dati di default cambiano.
    // Al prossimo avvio dell'app, le tabelle con dati di seed vengono azzerate.
    // Le stazioni vengono ri-popolate solo su consenso esplicito dell'utente.
    public const decimal CurrentDbVersion = 0.72m;

    private const string DbFileName = "radioe45.db";
    private SQLiteAsyncConnection? _connection;
    private readonly Task _initTask;

    public DatabaseService()
    {
        _initTask = InitializeCoreAsync();
    }

    public static string GetDatabasePath() => Path.Combine(FileSystem.AppDataDirectory, DbFileName);

    private async Task InitializeCoreAsync()
    {
        string dbPath = GetDatabasePath();
        var conn = new SQLiteAsyncConnection(dbPath);
        await InitializeAsync(conn);
        _connection = conn;
    }

    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        await _initTask;
        return _connection!;
    }

    private static async Task InitializeAsync(SQLiteAsyncConnection conn)
    {
        await conn.CreateTableAsync<RadioStation>();
        await conn.CreateTableAsync<DbVersion>();
        await conn.CreateTableAsync<AppSettings>();
        await conn.CreateTableAsync<Log>();
        await MigrateAppSettingsSchemaAsync(conn);
        await SeedDefaultAppSettingsAsync(conn);
        await RunSeedMigrationIfNeededAsync(conn);
    }

    private static async Task MigrateAppSettingsSchemaAsync(SQLiteAsyncConnection conn)
    {
        var columns = await conn.GetTableInfoAsync("AppSettings");
        HashSet<string> columnNames = columns
            .Select(static column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!columnNames.Contains(nameof(AppSettings.CrashReportingEnabled)))
        {
            await conn.ExecuteAsync(
                $"ALTER TABLE AppSettings ADD COLUMN {nameof(AppSettings.CrashReportingEnabled)} INTEGER NOT NULL DEFAULT 0");
        }

        if (!columnNames.Contains(nameof(AppSettings.CrashReportingConsentRequested)))
        {
            await conn.ExecuteAsync(
                $"ALTER TABLE AppSettings ADD COLUMN {nameof(AppSettings.CrashReportingConsentRequested)} INTEGER NOT NULL DEFAULT 0");
        }
    }

    private static async Task RunSeedMigrationIfNeededAsync(SQLiteAsyncConnection conn)
    {
        AppSettings? settings = await conn.Table<AppSettings>().FirstOrDefaultAsync();
        if (settings is null || settings.SeedVersion >= CurrentDbVersion)
            return;

        await ApplySeedAndUpdateSettingsAsync(conn);
    }

    public async Task ResetToDefaultsAsync()
    {
        SQLiteAsyncConnection conn = await GetConnectionAsync();
        await ApplySeedAndUpdateSettingsAsync(conn);
    }

    private static async Task ApplySeedAndUpdateSettingsAsync(SQLiteAsyncConnection conn)
    {
        await conn.DropTableAsync<RadioStation>();
        await conn.DropTableAsync<DbVersion>();
        await conn.CreateTableAsync<RadioStation>();
        await conn.CreateTableAsync<DbVersion>();

        AppSettings? settings = await conn.Table<AppSettings>().FirstOrDefaultAsync();
        if (settings is not null)
        {
            settings.SeedVersion = CurrentDbVersion;
            await conn.UpdateAsync(settings);
        }
    }

    public async Task SeedStationsAsync()
    {
        SQLiteAsyncConnection conn = await GetConnectionAsync();
        await SeedDefaultStationsAsync(conn);
        await SeedDefaultDbVersionAsync(conn);
    }

    private static async Task SeedDefaultStationsAsync(SQLiteAsyncConnection conn)
    {
        var stations = new List<RadioStation>
        {
            new()
            {
                StationId = 7,
                Name = "RadioE45",
                Description = "La radio che collega il territorio",
                StreamUrl = ":8060/radio.mp3",
                UrlBase = "radioe45.ddns.net",
                LogoUrl =  "https://radioe45.it/assets/images/image06.png",
                WebsocketUrl = "/api/live/nowplaying/websocket", //wss://radioe45.ddns.net/api/live/nowplaying/websocket
                ShortName = "RadioE45",
                IsTest = false,
                SortOrder = 0
            },
            new()
            {
                StationId = 1,
                Name = "Radio Antani",
                Description = "Il canale alternativo",
                StreamUrl = ":8000/radio.mp3",
                UrlBase = "radioe45.ddns.net",
                LogoUrl = "",
                WebsocketUrl = "/api/live/nowplaying/websocket",
                ShortName = "Radio_Antani",
                IsTest = false,
                SortOrder = 1
            }
            /*,
            new()
            {
                StationId = 1,
                Name = "Radio Test Locale",
                Description = "Il canale alternativo",
                StreamUrl = "/listen/radiotest/radio.mp3",
                UrlBase = "web.azuracast.orb.local",
                LogoUrl = "",
                WebsocketUrl = "/api/live/nowplaying/websocket",
                ShortName = "RadioTest",
                IsTest = true,
                SortOrder = 10
            } */
        };

        foreach (RadioStation station in stations)
            await conn.InsertAsync(station);
    }

    private static async Task SeedDefaultDbVersionAsync(SQLiteAsyncConnection conn)
    {
        await conn.InsertAsync(new DbVersion
        {
            DbVer = CurrentDbVersion,
            LastDbUpdate = DateTime.Now
        });
    }

    private static async Task SeedDefaultAppSettingsAsync(SQLiteAsyncConnection conn)
    {
        int count = await conn.Table<AppSettings>().CountAsync();
        if (count > 0)
            return;

        await conn.InsertAsync(new AppSettings { Id = 1 });
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.CloseAsync();
    }
}
