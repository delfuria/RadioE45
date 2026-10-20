using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RadioE45.Models;
using RadioE45.Services.Platform;
using RadioE45.Services.Updates;

namespace RadioE45.Core.Tests.Services.Updates;

// The automatic check runs on the first launch of each local day; the manual one always runs.
public class AppUpdateCoordinatorTests
{
    private static readonly AppUpdateInfo Newer =
        new(new AppVersion(0, 47), AppUpdateAction.OpenUrl, new Uri("https://example.org/store"));

    private readonly IAppUpdateSource _source = Substitute.For<IAppUpdateSource>();
    private readonly IAppEnvironment _environment = Substitute.For<IAppEnvironment>();
    private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero));

    public AppUpdateCoordinatorTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _source.IsSupported.Returns(true);
        _source.GetLatestAsync(TestContext.Current.CancellationToken).ReturnsForAnyArgs(Newer);
        _environment.VersionString.Returns("0.46");
        _store.Get(AppUpdateCoordinator.LastCheckDateKey, "").Returns("");
    }

    private AppUpdateCoordinator Create() =>
        new(_source, _environment, _store, _time, NullLogger<AppUpdateCoordinator>.Instance);

    [Fact]
    public async Task A_newer_version_is_reported_and_the_day_is_recorded()
    {
        AppUpdateCheckResult result = await Create().CheckAsync(manual: false, TestContext.Current.CancellationToken);

        Assert.Equal(AppUpdateStatus.Available, result.Status);
        Assert.Equal(Newer, result.Update);
        _store.Received(1).Set(AppUpdateCoordinator.LastCheckDateKey, "2026-10-20");
    }

    [Fact]
    public async Task The_second_automatic_check_of_the_day_does_nothing()
    {
        _store.Get(AppUpdateCoordinator.LastCheckDateKey, "").Returns("2026-10-20");

        AppUpdateCheckResult result = await Create().CheckAsync(manual: false, TestContext.Current.CancellationToken);

        Assert.Equal(AppUpdateStatus.Skipped, result.Status);
        await _source.DidNotReceiveWithAnyArgs().GetLatestAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task The_next_day_checks_again_even_if_the_user_chose_later()
    {
        _store.Get(AppUpdateCoordinator.LastCheckDateKey, "").Returns("2026-10-19");

        AppUpdateCheckResult result = await Create().CheckAsync(manual: false, TestContext.Current.CancellationToken);

        Assert.Equal(AppUpdateStatus.Available, result.Status);
    }

    [Fact]
    public async Task The_day_follows_the_local_time_zone()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("plus10", TimeSpan.FromHours(10), "plus10", "plus10"));
        _store.Get(AppUpdateCoordinator.LastCheckDateKey, "").Returns("2026-10-20");

        // 09:00 UTC is 19:00 on the 20th locally: still the same day.
        Assert.Equal(AppUpdateStatus.Skipped, (await Create().CheckAsync(manual: false, TestContext.Current.CancellationToken)).Status);

        _time.Advance(TimeSpan.FromHours(6)); // 01:00 on the 21st locally
        Assert.Equal(AppUpdateStatus.Available, (await Create().CheckAsync(manual: false, TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task The_manual_check_ignores_the_daily_limit()
    {
        _store.Get(AppUpdateCoordinator.LastCheckDateKey, "").Returns("2026-10-20");

        AppUpdateCheckResult result = await Create().CheckAsync(manual: true, TestContext.Current.CancellationToken);

        Assert.Equal(AppUpdateStatus.Available, result.Status);
    }

    [Theory]
    [InlineData("0.47")]
    [InlineData("0.46")]
    [InlineData("0.48")]
    public async Task Only_a_higher_major_minor_is_an_update(string installed)
    {
        _environment.VersionString.Returns(installed);

        AppUpdateCheckResult result = await Create().CheckAsync(manual: true, TestContext.Current.CancellationToken);

        Assert.Equal(installed == "0.46" ? AppUpdateStatus.Available : AppUpdateStatus.UpToDate, result.Status);
    }

    [Fact]
    public async Task A_patch_only_release_is_not_an_update()
    {
        _source.GetLatestAsync(TestContext.Current.CancellationToken).ReturnsForAnyArgs(
            new AppUpdateInfo(new AppVersion(0, 46), AppUpdateAction.OpenUrl, null));

        AppUpdateCheckResult result = await Create().CheckAsync(manual: true, TestContext.Current.CancellationToken);

        Assert.Equal(AppUpdateStatus.UpToDate, result.Status);
    }

    [Fact]
    public async Task A_source_without_a_record_means_up_to_date()
    {
        _source.GetLatestAsync(TestContext.Current.CancellationToken).ReturnsForAnyArgs((AppUpdateInfo?)null);

        AppUpdateCheckResult result = await Create().CheckAsync(manual: false, TestContext.Current.CancellationToken);

        Assert.Equal(AppUpdateStatus.UpToDate, result.Status);
        _store.Received(1).Set(AppUpdateCoordinator.LastCheckDateKey, "2026-10-20");
    }

    [Fact]
    public async Task A_network_failure_is_silent_and_does_not_use_up_the_day()
    {
        _source.GetLatestAsync(TestContext.Current.CancellationToken).ThrowsAsyncForAnyArgs(new HttpRequestException("offline"));

        AppUpdateCheckResult result = await Create().CheckAsync(manual: false, TestContext.Current.CancellationToken);

        Assert.Equal(AppUpdateStatus.Failed, result.Status);
        _store.DidNotReceiveWithAnyArgs().Set(default!, "");
    }

    [Fact]
    public async Task An_unreadable_app_version_fails_without_calling_the_source()
    {
        _environment.VersionString.Returns("dev");

        AppUpdateCheckResult result = await Create().CheckAsync(manual: true, TestContext.Current.CancellationToken);

        Assert.Equal(AppUpdateStatus.Failed, result.Status);
        await _source.DidNotReceiveWithAnyArgs().GetLatestAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Cancellation_is_not_swallowed()
    {
        _source.GetLatestAsync(TestContext.Current.CancellationToken).ThrowsAsyncForAnyArgs(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => Create().CheckAsync(manual: false, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task An_unsupported_channel_never_checks_nor_records()
    {
        _source.IsSupported.Returns(false);

        AppUpdateCheckResult result = await Create().CheckAsync(manual: true, TestContext.Current.CancellationToken);

        Assert.Equal(AppUpdateStatus.Unsupported, result.Status);
        await _source.DidNotReceiveWithAnyArgs().GetLatestAsync(TestContext.Current.CancellationToken);
        _store.DidNotReceiveWithAnyArgs().Set(default!, "");
    }
}
