namespace RadioE45.Services.Legal;

/// <summary>
/// Terms of Use shown once, at first launch (App Store guideline 5.2.3). The user confirms having
/// read them and the dialog never comes back, unless <see cref="TermsService.CurrentTermsVersion"/>
/// is raised after the text changes.
/// </summary>
public interface ITermsService
{
    /// <summary>
    /// Shows the terms if the current version has not been acknowledged yet, and records the
    /// acknowledgement once the user dismisses the dialog. Call it after the first page is on screen.
    /// </summary>
    Task EnsureAcceptedAsync();

    /// <summary>Shows the same terms again on demand (Settings → Info). Records nothing.</summary>
    Task ShowAsync();
}
