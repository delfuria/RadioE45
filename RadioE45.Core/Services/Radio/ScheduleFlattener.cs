using RadioE45.Models;

namespace RadioE45.Services.Radio;

/// <summary>
/// Trasforma la lista grezza di AzuraCast (playlist e streamer sovrapposti) in una sequenza lineare:
/// lo streamer ha sempre la precedenza e le playlist vengono ritagliate attorno alle sue fasce.
/// </summary>
public static class ScheduleFlattener
{
    public static List<PlaylistSchedule> Flatten(
        IEnumerable<PlaylistSchedule> items,
        DateTimeOffset now,
        TimeSpan? minPlaylistDuration = null)
    {
        List<PlaylistSchedule> source = items.ToList();
        List<PlaylistSchedule> streamers = source.Where(s => s.IsStreamer).OrderBy(s => s.Start).ToList();
        List<(DateTimeOffset Start, DateTimeOffset End)> blocked = MergeRanges(streamers);

        var result = new List<PlaylistSchedule>();

        foreach (PlaylistSchedule streamer in streamers)
            result.Add(streamer.WithRange(streamer.Start, streamer.End, IsNow(streamer.Start, streamer.End, now)));

        foreach (PlaylistSchedule playlist in source.Where(s => !s.IsStreamer))
        {
            foreach ((DateTimeOffset start, DateTimeOffset end) in Subtract(playlist.Start, playlist.End, blocked))
            {
                if (minPlaylistDuration is { } min && end - start < min)
                    continue;

                result.Add(playlist.WithRange(start, end, IsNow(start, end, now)));
            }
        }

        return result.OrderBy(s => s.Start).ThenBy(s => s.IsStreamer ? 0 : 1).ToList();
    }

    private static bool IsNow(DateTimeOffset start, DateTimeOffset end, DateTimeOffset now) => start <= now && now < end;

    /// <summary>Unione degli intervalli degli streamer (sovrapposti o adiacenti).</summary>
    private static List<(DateTimeOffset Start, DateTimeOffset End)> MergeRanges(List<PlaylistSchedule> sortedStreamers)
    {
        var merged = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        foreach (PlaylistSchedule s in sortedStreamers)
        {
            if (merged.Count > 0 && s.Start <= merged[^1].End)
                merged[^1] = (merged[^1].Start, s.End > merged[^1].End ? s.End : merged[^1].End);
            else
                merged.Add((s.Start, s.End));
        }
        return merged;
    }

    /// <summary>Sottrae gli intervalli bloccati (ordinati e disgiunti) da [start, end); restituisce 0..n frammenti.</summary>
    private static IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> Subtract(
        DateTimeOffset start,
        DateTimeOffset end,
        List<(DateTimeOffset Start, DateTimeOffset End)> blocked)
    {
        DateTimeOffset cursor = start;
        foreach ((DateTimeOffset bStart, DateTimeOffset bEnd) in blocked)
        {
            if (bEnd <= cursor || bStart >= end)
                continue;

            if (bStart > cursor)
                yield return (cursor, bStart);

            cursor = bEnd;
            if (cursor >= end)
                yield break;
        }

        if (cursor < end)
            yield return (cursor, end);
    }
}
