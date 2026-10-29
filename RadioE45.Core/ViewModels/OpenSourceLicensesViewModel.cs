using RadioE45.Services.Localization;

namespace RadioE45.ViewModels;

/// <summary>
/// Settings → Info → Open source licenses: the copyright notices and license texts of the
/// third-party software the app ships (THIRD-PARTY-NOTICES.txt, embedded in this assembly).
/// </summary>
public partial class OpenSourceLicensesViewModel : BaseViewModel
{
    internal const string NoticesResourceName = "RadioE45.Legal.THIRD-PARTY-NOTICES.txt";

    public OpenSourceLicensesViewModel()
    {
        Title = LocalizationResourceManager.Instance["OpenSource_Title"];
        IntroText = LocalizationResourceManager.Instance["OpenSource_Intro"];
        NoticesText = LoadNotices();
    }

    public string IntroText { get; }

    public string NoticesText { get; }

    private static string LoadNotices()
    {
        using Stream? stream = typeof(OpenSourceLicensesViewModel).Assembly.GetManifestResourceStream(NoticesResourceName);
        if (stream is null)
            return string.Empty;

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
