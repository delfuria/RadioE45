using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RadioE45.Models;
using RadioE45.Services.Platform;
using RadioE45.Services.Updates;

namespace RadioE45.Core.Tests.Services.Updates;

// The startup check asks only when a newer version exists, never blocks and never shows errors.
public class AppUpdatePrompterTests
{
    private static readonly Uri StoreUrl = new("https://example.org/store");
    private static readonly AppUpdateInfo Newer = new(new AppVersion(0, 47), AppUpdateAction.OpenUrl, StoreUrl);

    private readonly IAppUpdateSource _source = Substitute.For<IAppUpdateSource>();
    private readonly IAppEnvironment _environment = Substitute.For<IAppEnvironment>();
    private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IUrlLauncher _launcher = Substitute.For<IUrlLauncher>();

    public AppUpdatePrompterTests()
    {
        _source.IsSupported.Returns(true);
        _source.GetLatestAsync(TestContext.Current.CancellationToken).ReturnsForAnyArgs(Newer);
        _environment.VersionString.Returns("0.46");
        _store.Get("update.lastCheckDate", "").Returns("");
        _launcher.OpenAsync(default!).ReturnsForAnyArgs(Task.CompletedTask);
    }

    private AppUpdatePrompter Create()
    {
        var coordinator = new AppUpdateCoordinator(_source, _environment, _store, new FakeTimeProvider(),
            NullLogger<AppUpdateCoordinator>.Instance);
        return new AppUpdatePrompter(coordinator, _dialogs, _launcher, NullLogger<AppUpdatePrompter>.Instance);
    }

    private void UserAnswers(bool accepted) =>
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(accepted);

    [Fact]
    public async Task Accepting_the_prompt_opens_the_store_page()
    {
        UserAnswers(true);

        await Create().CheckAtStartupAsync();

        await _launcher.Received(1).OpenAsync(StoreUrl);
    }

    [Fact]
    public async Task Choosing_later_opens_nothing()
    {
        UserAnswers(false);

        await Create().CheckAtStartupAsync();

        await _dialogs.ReceivedWithAnyArgs(1).ConfirmAsync(default!, default!, default!, default!);
        await _launcher.DidNotReceiveWithAnyArgs().OpenAsync(default!);
    }

    [Fact]
    public async Task Up_to_date_shows_nothing()
    {
        _environment.VersionString.Returns("0.47");

        await Create().CheckAtStartupAsync();

        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task A_failing_check_is_swallowed_without_any_dialog()
    {
        _source.GetLatestAsync(TestContext.Current.CancellationToken).ThrowsAsyncForAnyArgs(new HttpRequestException("offline"));

        await Create().CheckAtStartupAsync();

        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
        await _dialogs.DidNotReceiveWithAnyArgs().AlertAsync(default!, default!, default!);
    }

    [Fact]
    public async Task A_launcher_failure_does_not_escape()
    {
        UserAnswers(true);
        _launcher.OpenAsync(default!).ThrowsAsyncForAnyArgs(new InvalidOperationException("no handler"));

        await Create().CheckAtStartupAsync();
    }

    [Fact]
    public async Task An_update_without_a_url_opens_nothing()
    {
        await Create().OpenAsync(new AppUpdateInfo(new AppVersion(0, 47), AppUpdateAction.OpenUrl, null));

        await _launcher.DidNotReceiveWithAnyArgs().OpenAsync(default!);
    }
}
