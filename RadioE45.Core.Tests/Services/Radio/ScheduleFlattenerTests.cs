using RadioE45.Models;
using RadioE45.Services.Radio;

namespace RadioE45.Core.Tests.Services.Radio;

public class ScheduleFlattenerTests
{
    private static readonly DateTimeOffset Day = new(2026, 10, 7, 0, 0, 0, TimeSpan.FromHours(2));

    private static PlaylistSchedule Playlist(string name, int fromHour, int toHour) => new()
    {
        Type = "playlist",
        Name = name,
        Start = Day.AddHours(fromHour),
        End = Day.AddHours(toHour),
    };

    private static PlaylistSchedule Streamer(string name, int fromHour, int toHour) => new()
    {
        Type = "streamer",
        Name = name,
        Start = Day.AddHours(fromHour),
        End = Day.AddHours(toHour),
    };

    [Fact]
    public void Streamer_splits_overlapping_playlist_into_two_fragments()
    {
        List<PlaylistSchedule> result = ScheduleFlattener.Flatten(
            [Playlist("AutoDJ", 8, 20), Streamer("Live", 12, 13)],
            now: Day.AddHours(6));

        Assert.Collection(result,
            p => Assert.Equal(("AutoDJ", Day.AddHours(8), Day.AddHours(12)), (p.Name, p.Start, p.End)),
            s => Assert.Equal(("Live", Day.AddHours(12), Day.AddHours(13)), (s.Name, s.Start, s.End)),
            p => Assert.Equal(("AutoDJ", Day.AddHours(13), Day.AddHours(20)), (p.Name, p.Start, p.End)));
    }

    [Fact]
    public void Playlist_fully_covered_by_streamers_is_dropped()
    {
        List<PlaylistSchedule> result = ScheduleFlattener.Flatten(
            [Playlist("Short", 12, 13), Streamer("Live A", 11, 12), Streamer("Live B", 12, 14)],
            now: Day);

        Assert.All(result, s => Assert.True(s.IsStreamer));
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void IsNow_is_recomputed_on_clipped_ranges()
    {
        // 10:30 falls in the first fragment of the playlist, not in the original 8-20 range only.
        List<PlaylistSchedule> result = ScheduleFlattener.Flatten(
            [Playlist("AutoDJ", 8, 20), Streamer("Live", 12, 13)],
            now: Day.AddHours(10).AddMinutes(30));

        Assert.Single(result, s => s.IsNow);
        Assert.True(result[0].IsNow);
        Assert.False(result[2].IsNow);
    }

    [Fact]
    public void Fragments_shorter_than_minimum_are_filtered()
    {
        List<PlaylistSchedule> result = ScheduleFlattener.Flatten(
            [Playlist("AutoDJ", 11, 14), Streamer("Live", 12, 14)],
            now: Day,
            minPlaylistDuration: TimeSpan.FromHours(2));

        Assert.Single(result);
        Assert.True(result[0].IsStreamer);
    }
}
