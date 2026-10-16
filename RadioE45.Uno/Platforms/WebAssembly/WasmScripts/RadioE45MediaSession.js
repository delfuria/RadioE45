// Media Session API for BrowserMediaSession (RadioE45.Uno/Services/Browser) via JSImport: title,
// artist and cover in the browser and system media controls, play/pause from media keys.
// Next/previous track = next/previous station; no seek (live stream).
globalThis.RadioE45MediaSession = {
    isSupported: () => "mediaSession" in navigator,
    setHandlers: (callback) => {
        if (!("mediaSession" in navigator)) return;
        for (const action of ["play", "pause", "stop", "nexttrack", "previoustrack"]) {
            try {
                navigator.mediaSession.setActionHandler(action, () => callback(action));
            } catch {
                // Action not supported by this browser.
            }
        }
    },
    setMetadata: (title, artist, artworkUrl) => {
        if (!("mediaSession" in navigator)) return;
        navigator.mediaSession.metadata = new MediaMetadata({
            title: title,
            artist: artist,
            artwork: artworkUrl ? [{ src: artworkUrl }] : []
        });
    },
    setPlaybackState: (state) => {
        if ("mediaSession" in navigator) navigator.mediaSession.playbackState = state;
    },
    clear: () => {
        if (!("mediaSession" in navigator)) return;
        navigator.mediaSession.metadata = null;
        navigator.mediaSession.playbackState = "none";
    }
};
