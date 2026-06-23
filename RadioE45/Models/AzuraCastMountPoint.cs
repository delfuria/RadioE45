using System.Text.Json.Serialization;

namespace RadioE45.Models;

public class AzuraCastMountPoint
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("is_default")]
    public bool IsDefault { get; set; }
}
