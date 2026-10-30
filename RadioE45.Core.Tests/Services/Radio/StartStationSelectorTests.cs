using NSubstitute;
using RadioE45.Models;
using RadioE45.Services.Platform;
using RadioE45.Services.Radio;

namespace RadioE45.Core.Tests.Services.Radio;

public class StartStationSelectorTests
{
    private readonly IAzuraStationCatalog _catalog = Substitute.For<IAzuraStationCatalog>();
    private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();
    private readonly AzuraStation _first = new() { Id = 1, IsOnline = true };
    private readonly AzuraStation _second = new() { Id = 2, IsOnline = true };
    private readonly AzuraStation _favorite = new() { Id = 3, IsOnline = true, IsFavorite = true };

    public StartStationSelectorTests()
    {
        _catalog.Stations.Returns([_first, _second, _favorite]);
        _catalog.GetFirst().Returns(_first);
        _catalog.GetFavorite().Returns(_favorite);
    }

    [Fact]
    public void First_ignores_favorite_and_last()
    {
        _store.Get("last_station_id", 0).Returns(2);

        Assert.Same(_first, StartStationSelector.Select(_catalog, _store, StartStationMode.First));
    }

    [Fact]
    public void Favorite_falls_back_to_first_when_there_is_none()
    {
        Assert.Same(_favorite, StartStationSelector.Select(_catalog, _store, StartStationMode.Favorite));

        _catalog.GetFavorite().Returns((AzuraStation?)null);
        Assert.Same(_first, StartStationSelector.Select(_catalog, _store, StartStationMode.Favorite));
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(99, 1)]
    [InlineData(0, 1)]
    public void Last_uses_the_remembered_station_or_falls_back_to_first(int remembered, int expectedId)
    {
        _store.Get("last_station_id", 0).Returns(remembered);

        Assert.Equal(expectedId, StartStationSelector.Select(_catalog, _store, StartStationMode.Last)!.Id);
    }
}
