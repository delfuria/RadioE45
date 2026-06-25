using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RadioE45.Models;

public partial class AzuraStation : ObservableObject
{
    // DB — identity and configuration
    public int Id { get; set; }
    public int StationId { get; set; }
    public string UrlBase { get; set; } = "";
    public string? LogoUrl { get; set; }
    public bool IsFavorite { get; set; }
    public int SortOrder { get; set; }
    public bool IsTest { get; set; }
    public string WebsocketUrl { get; set; } = "";

    // API — live data (fallback to DB values if API unavailable)
    public string Name { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string Description { get; set; } = "";
    public string StreamUrl { get; set; } = "";
    public string StreamUrlFallback { get; set; } = "";
    public string OnAirStreamUrl { get; set; } = "";

    // URL pubblico della stazione — da API, per uso futuro
    public string? PublicUrl { get; set; }

    // HLS — da API, per uso futuro
    public bool HlsEnabled { get; set; }
    public bool HlsIsDefault { get; set; }
    public string? HlsUrl { get; set; }

    public bool IsOnline { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    public ICommand? DeleteCommand { get; set; }
}
