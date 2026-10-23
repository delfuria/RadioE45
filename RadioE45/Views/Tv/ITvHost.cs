namespace RadioE45.Views.Tv;

/// <summary>Sections of the TV root page (docs/ANDROID-TV-ROADMAP.md §4).</summary>
public enum TvSection
{
    OnAir,
    Stations,
    Schedule,
    AddStation,
    EditStation,
    Request,
    Podcasts,
    PodcastEpisodes,
}

/// <summary>
/// What the TV root page offers to the platform services. On TV there is no Shell, so
/// <c>MauiNavigationService</c> and <c>MauiDialogService</c> go through this host instead of
/// <c>Shell.Current</c>; the ViewModels stay the ones shared with the phone.
/// </summary>
public interface ITvHost
{
    void ShowSection(TvSection section);

    /// <summary>Opens the edit form of a station.</summary>
    void ShowEditStation(int stationId);

    /// <summary>Opens the episodes of a podcast.</summary>
    void ShowPodcastEpisodes(string podcastId, string podcastTitle);

    /// <summary>Leaves a sub-section (schedule, add or edit station, podcast episodes) for the section it was opened from.</summary>
    void GoBack();

    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

    Task AlertAsync(string title, string message, string ok);

    /// <summary>Handles the remote's Back button: closes a dialog or leaves a section. False when there is nothing to go back to.</summary>
    bool HandleBack();
}

/// <summary>A view shown in the section area of the TV root page.</summary>
public interface ITvSection
{
    /// <summary>Called when the section becomes visible (loads data).</summary>
    Task OnShownAsync();

    /// <summary>Called when the section is replaced by another one (stops what it was doing).</summary>
    Task OnHiddenAsync() => Task.CompletedTask;

    /// <summary>Puts the remote's focus on the main control of the section.</summary>
    void FocusDefault();
}

/// <summary>Holder of the live <see cref="ITvHost"/>: set by the TV root page, null on every other device.</summary>
public sealed class TvHostAccessor
{
    public ITvHost? Current { get; set; }
}
