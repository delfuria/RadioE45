using CommunityToolkit.Maui.Views;

namespace RadioE45.Services.Audio;

/// <summary>
/// MAUI-only: lets a page hand the MediaElement from its visual tree to the audio engine that
/// drives it. The shared IAudioService/IPodcastPlayerService contracts in RadioE45.Core stay
/// free of MAUI types (the Uno head plays without a visual element).
/// </summary>
public interface IMediaElementHost
{
    /// <summary>Called by a hosting page whose MediaElement is already in its visual tree.</summary>
    void Initialize(MediaElement mediaElement);

    /// <summary>
    /// Alternative to <see cref="Initialize"/> for engines that create the element on demand:
    /// the factory adds a MediaElement to the page and returns it once its handler is connected.
    /// Null when the page goes away. Ignored by engines that do not need it.
    /// </summary>
    void SetPlayerFactory(Func<Task<MediaElement>>? factory)
    {
    }
}
