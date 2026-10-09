using NSubstitute;
using RadioE45.Services.Legal;
using RadioE45.Services.Platform;

namespace RadioE45.Core.Tests.Services.Legal;

// The Terms of Use dialog appears once (App Store guideline 5.2.3) and then never again.
public class TermsServiceTests
{
    private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    private TermsService CreateService(int acceptedVersion)
    {
        _store.Get(TermsService.AcceptedVersionKey, 0).Returns(acceptedVersion);
        _dialogs.AlertAsync(default!, default!, default!).ReturnsForAnyArgs(Task.CompletedTask);
        return new TermsService(_store, _dialogs);
    }

    [Fact]
    public async Task First_launch_shows_the_terms_and_records_the_acknowledgement()
    {
        TermsService service = CreateService(acceptedVersion: 0);

        await service.EnsureAcceptedAsync();

        await _dialogs.ReceivedWithAnyArgs(1).AlertAsync(default!, default!, default!);
        _store.Received(1).Set(TermsService.AcceptedVersionKey, TermsService.CurrentTermsVersion);
    }

    [Fact]
    public async Task Already_acknowledged_terms_are_not_shown_again()
    {
        TermsService service = CreateService(acceptedVersion: TermsService.CurrentTermsVersion);

        await service.EnsureAcceptedAsync();

        await _dialogs.DidNotReceiveWithAnyArgs().AlertAsync(default!, default!, default!);
        _store.DidNotReceiveWithAnyArgs().Set(default!, 0);
    }

    [Fact]
    public async Task Showing_the_terms_from_settings_records_nothing()
    {
        TermsService service = CreateService(acceptedVersion: TermsService.CurrentTermsVersion);

        await service.ShowAsync();

        await _dialogs.ReceivedWithAnyArgs(1).AlertAsync(default!, default!, default!);
        _store.DidNotReceiveWithAnyArgs().Set(default!, 0);
    }

    [Fact]
    public async Task The_acknowledgement_is_recorded_only_after_the_dialog_is_dismissed()
    {
        var dismissed = new TaskCompletionSource();
        _store.Get(TermsService.AcceptedVersionKey, 0).Returns(0);
        _dialogs.AlertAsync(default!, default!, default!).ReturnsForAnyArgs(dismissed.Task);
        var service = new TermsService(_store, _dialogs);

        Task pending = service.EnsureAcceptedAsync();

        _store.DidNotReceiveWithAnyArgs().Set(default!, 0);
        dismissed.SetResult();
        await pending;
        _store.Received(1).Set(TermsService.AcceptedVersionKey, TermsService.CurrentTermsVersion);
    }
}
