using System.Text.Json.Serialization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RadioE45.Models;

public partial class AzuraCastRequestItem : ObservableObject
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("song")]
    public SongInfo Song { get; set; } = new();

    [JsonIgnore]
    public string Artist => Song.Artist;

    [JsonIgnore]
    public string Title => Song.Title;

    [JsonIgnore]
    public string? ArtworkUrl => Song.ArtUrl;

    [JsonIgnore]
    public ICommand? RequestCommand { get; set; }

    // Stato per-riga: disabilita/aggiorna il bottone di richiesta senza dover ricaricare l'elenco.
    [JsonIgnore]
    [ObservableProperty]
    public partial bool IsRequesting { get; set; }

    [JsonIgnore]
    [ObservableProperty]
    public partial bool IsRequested { get; set; }
}
