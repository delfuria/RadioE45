using System.Text.Json.Serialization;

namespace RadioE45.Models;

// Corpo di errore standard delle API AzuraCast (Api_Error): usato per estrarre il messaggio
// reale del server, es. il countdown del cooldown su una richiesta rifiutata (403).
public class AzuraCastApiError
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
