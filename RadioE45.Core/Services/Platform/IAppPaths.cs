namespace RadioE45.Services.Platform;

public interface IAppPaths
{
    /// <summary>Private, persistent folder for the SQLite database and diagnostics files.</summary>
    string AppDataDirectory { get; }
}
