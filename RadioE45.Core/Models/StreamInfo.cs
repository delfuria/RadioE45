namespace RadioE45.Models;

/// <summary>
/// What is known about the stream of a station. Every value but the station name and the address
/// can be missing: the AzuraCast API only gives format and bitrate, the rest comes from the
/// Icecast response headers, which the web build may not be allowed to read.
/// </summary>
public sealed record StreamInfo(
    string StationName,
    string Address,
    string? Format = null,
    int? BitrateKbps = null,
    int? SampleRateHz = null,
    int? Channels = null,
    string? Genre = null);
