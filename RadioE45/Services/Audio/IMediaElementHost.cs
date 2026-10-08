using CommunityToolkit.Maui.Views;

namespace RadioE45.Services.Audio;

/// <summary>
/// MAUI-only: lets a page hand the MediaElement from its visual tree to the audio engine that
/// drives it. The shared IAudioService/IPodcastPlayerService contracts in RadioE45.Core stay
/// free of MAUI types (the Uno head plays without a visual element).
/// </summary>
public interface IMediaElementHost
{
    /// <summary>Called once from the hosting page (OnAirPage / PodcastEpisodesPage).</summary>
    void Initialize(MediaElement mediaElement);
}
