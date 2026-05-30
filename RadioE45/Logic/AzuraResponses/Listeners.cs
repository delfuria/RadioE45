using System.Text.Json.Serialization;

namespace RadioE45.Logic.AzuraResponses;

public class Listeners
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("unique")]
    public int Unique { get; set; }

    [JsonPropertyName("current")]
    public int Current { get; set; }
}
