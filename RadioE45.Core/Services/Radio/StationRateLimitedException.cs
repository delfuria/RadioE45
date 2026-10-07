namespace RadioE45.Services.Radio;

/// <summary>
/// Lanciata quando l'API AzuraCast risponde con HTTP 429 Too Many Requests.
/// Non indica che la stazione è offline, ma che il client ha superato il rate limit.
/// </summary>
internal sealed class StationRateLimitedException : Exception
{
    public StationRateLimitedException() : base("AzuraCast rate limit exceeded (HTTP 429)") { }
}
