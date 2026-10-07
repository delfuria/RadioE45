namespace RadioE45.Services.Audio;

public interface IStreamUrlProber
{
    /// <summary>
    /// Probes all candidate URLs in parallel and returns the first reachable one,
    /// or null if none respond. Remaining probes are cancelled once a winner is found.
    /// </summary>
    Task<string?> ProbeFirstReachableAsync(string[] urls, CancellationToken ct);
}
