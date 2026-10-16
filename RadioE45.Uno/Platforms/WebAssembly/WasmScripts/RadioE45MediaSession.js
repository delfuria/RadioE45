// Media Session API for BrowserMediaSession (RadioE45.Uno/Services/Browser) via JSImport: title,
// artist and cover in the browser and system media controls, play/pause from media keys.
// Next/previous track = next/previous station; no seek (live stream).
// The handlers are set again with every metadata and state update: Safari shows in the macOS
// Now Playing only the actions known when the audio started, so handlers set at launch, before
// any audio, gave play/pause only.
globalThis.RadioE45MediaSession = {
    _callback: null,
    _applyHandlers: () => {
        const callback = globalThis.RadioE45MediaSession._callback;
        if (!callback || !("mediaSession" in navigator)) return;
        for (const action of ["play", "pause", "stop", "nexttrack", "previoustrack"]) {
            try {
                navigator.mediaSession.setActionHandler(action, () => callback(action));
            } catch {
                // Action not supported by this browser.
            }
        }
    },
    isSupported: () => "mediaSession" in navigator,
    setHandlers: (callback) => {
        globalThis.RadioE45MediaSession._callback = callback;
        globalThis.RadioE45MediaSession._applyHandlers();
    },
    setMetadata: (title, artist, artworkUrl) => {
        if (!("mediaSession" in navigator)) return;
        navigator.mediaSession.metadata = new MediaMetadata({
            title: title,
            artist: artist,
            artwork: artworkUrl ? [{ src: artworkUrl }] : []
        });
        globalThis.RadioE45MediaSession._applyHandlers();
    },
    setPlaybackState: (state) => {
        if (!("mediaSession" in navigator)) return;
        navigator.mediaSession.playbackState = state;
        if (state === "playing") globalThis.RadioE45MediaSession._applyHandlers();
    },
    clear: () => {
        if (!("mediaSession" in navigator)) return;
        navigator.mediaSession.metadata = null;
        navigator.mediaSession.playbackState = "none";
    }
};
