using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Localization;

namespace RadioE45.ViewModels;

public sealed record StreamInfoRow(string Label, string Value);

public partial class StreamInfoViewModel : BaseViewModel
{
    private readonly OnAirViewModel _onAirViewModel;
    private readonly IStreamInfoProvider _provider;

    [ObservableProperty]
    public partial ObservableCollection<StreamInfoRow> Rows { get; set; } = [];

    public StreamInfoViewModel(OnAirViewModel onAirViewModel, IStreamInfoProvider provider, ILogger<StreamInfoViewModel> logger)
    {
        Logger = logger;
        _onAirViewModel = onAirViewModel;
        _provider = provider;
        Title = LocalizationResourceManager.Instance["StreamInfo_Title"];
    }

    /// <summary>
    /// Shows station and address at once, then adds what the stream server tells (format, bitrate,
    /// sample rate, channels, genre) when it answers.
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        AzuraStation? station = _onAirViewModel.CurrentStation;
        if (station is null)
        {
            Rows = [];
            return;
        }

        Rows = ToRows(new StreamInfo(station.Name, StreamInfoProvider.PlayedUrl(station)));

        await SafeExecuteAsync(async () =>
        {
            StreamInfo info = await _provider.GetAsync(station);
            Rows = ToRows(info);
        }, LocalizationResourceManager.Instance["Err_StreamInfo"]);
    }

    /// <summary>The rows of the info, in display order; the ones without a value are left out.</summary>
    public static ObservableCollection<StreamInfoRow> ToRows(StreamInfo info)
    {
        LocalizationResourceManager text = LocalizationResourceManager.Instance;
        var rows = new ObservableCollection<StreamInfoRow>
        {
            new(text["StreamInfo_Station"], info.StationName),
            new(text["StreamInfo_Address"], info.Address),
        };

        if (!string.IsNullOrEmpty(info.Format))
            rows.Add(new(text["StreamInfo_Format"], info.Format));
        if (info.BitrateKbps is > 0)
            rows.Add(new(text["StreamInfo_Bitrate"], $"{info.BitrateKbps} kb/s"));
        if (info.SampleRateHz is > 0)
            rows.Add(new(text["StreamInfo_SampleRate"], $"{(info.SampleRateHz.Value / 1000.0).ToString("0.#", CultureInfo.CurrentCulture)} kHz"));
        if (info.Channels is > 0)
            rows.Add(new(text["StreamInfo_Channels"], ChannelsText(info.Channels.Value, text)));
        if (!string.IsNullOrEmpty(info.Genre))
            rows.Add(new(text["StreamInfo_Genre"], info.Genre));

        return rows;
    }

    private static string ChannelsText(int channels, LocalizationResourceManager text) => channels switch
    {
        1 => $"1 ({text["StreamInfo_Mono"]})",
        2 => $"2 ({text["StreamInfo_Stereo"]})",
        _ => channels.ToString(CultureInfo.CurrentCulture),
    };
}
