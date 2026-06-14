namespace RadioE45.Services.Audio;

public sealed class RemoteArtworkLoader
{
    private readonly IHttpClientFactory _httpClientFactory;

    public RemoteArtworkLoader(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<byte[]> LoadAsync(string artworkUrl, CancellationToken cancellationToken = default)
    {
        HttpClient client = _httpClientFactory.CreateClient("AzuraCast");
        return await client.GetByteArrayAsync(artworkUrl, cancellationToken);
    }
}
