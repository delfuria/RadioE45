using RadioE45.Models;
using RadioE45.ViewModels;

namespace RadioE45.Core.Tests.ViewModels;

public class StreamInfoViewModelTests
{
    [Fact]
    public void Rows_always_have_station_and_address_and_skip_what_is_missing()
    {
        var rows = StreamInfoViewModel.ToRows(new StreamInfo("Demo", "https://demo.example/radio.mp3"));

        Assert.Equal(["Demo", "https://demo.example/radio.mp3"], rows.Select(r => r.Value));
    }

    [Fact]
    public void Rows_follow_the_order_of_the_dialog_and_format_the_values()
    {
        var rows = StreamInfoViewModel.ToRows(new StreamInfo("Demo", "https://demo.example/radio.mp3",
            Format: "mp3", BitrateKbps: 128, SampleRateHz: 44100, Channels: 2, Genre: "Ambient Chill"));

        Assert.Equal(7, rows.Count);
        Assert.Equal("mp3", rows[2].Value);
        Assert.Equal("128 kb/s", rows[3].Value);
        Assert.Contains("44", rows[4].Value);
        Assert.EndsWith(" kHz", rows[4].Value);
        Assert.StartsWith("2 (", rows[5].Value);
        Assert.Equal("Ambient Chill", rows[6].Value);
    }

    [Fact]
    public void Rows_show_a_bare_number_for_more_than_two_channels()
    {
        var rows = StreamInfoViewModel.ToRows(new StreamInfo("Demo", "x", Channels: 6));

        Assert.Equal("6", rows[^1].Value);
    }
}
