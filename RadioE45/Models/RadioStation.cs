using SQLite;

namespace RadioE45.Models;

[Table("RadioStations")]
public class RadioStation
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public string UrlBase { get; set; } = string.Empty;
    public int StationId { get; set; } 
    public string? LogoUrl { get; set; }
    public bool IsFavorite { get; set; }
    public int SortOrder { get; set; }
    public string WebsocketUrl { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public bool IsTest { get; set; }
}
