// Podcast episode player for BrowserPodcastPlayer (RadioE45.Uno/Services/Browser) via JSImport.
// Uno's browser MediaPlayer plays through a <video> element, and Safari ends a <video> that
// plays an audio-only MP3 right after "playing" (duration ~0 s); an <audio> element plays it
// fine, so podcast episodes on the web use their own <audio>.
// Events reach C# as onEvent(name, value): "opening", "buffering", "playing", "paused", "ended",
// "error" (value = MediaError code). Events of a replaced or closed element are ignored.
globalThis.RadioE45PodcastAudio = {
    _audio: null,
    _onEvent: null,

    init: (onEvent) => {
        globalThis.RadioE45PodcastAudio._onEvent = onEvent;
    },

    // A new element for every episode: reusing one after its source was cleared is what the
    // other players avoid too.
    open: (url, volume) => {
        const self = globalThis.RadioE45PodcastAudio;
        self.close();

        const audio = new Audio();
        audio.preload = "auto";
        audio.volume = volume;
        self._audio = audio;

        const emit = (name, value) => {
            if (self._audio === audio && self._onEvent)
                self._onEvent(name, value || 0);
        };
        audio.addEventListener("loadstart", () => emit("opening"));
        audio.addEventListener("waiting", () => emit("buffering"));
        audio.addEventListener("playing", () => emit("playing"));
        audio.addEventListener("pause", () => { if (!audio.ended) emit("paused"); });
        audio.addEventListener("ended", () => emit("ended"));
        audio.addEventListener("error", () => emit("error", audio.error ? audio.error.code : 0));

        audio.src = url;
        const promise = audio.play();
        if (promise) {
            promise.catch(e => {
                // AbortError: the source was replaced or closed meanwhile (not a failure).
                if (e && e.name !== "AbortError")
                    emit("error", e.name === "NotAllowedError" ? -1 : 0);
            });
        }
    },

    play: () => {
        const audio = globalThis.RadioE45PodcastAudio._audio;
        if (audio) {
            const promise = audio.play();
            if (promise)
                promise.catch(() => { });
        }
    },

    pause: () => {
        const audio = globalThis.RadioE45PodcastAudio._audio;
        if (audio)
            audio.pause();
    },

    close: () => {
        const self = globalThis.RadioE45PodcastAudio;
        const audio = self._audio;
        self._audio = null; // first: the events below belong to a closed element
        if (audio) {
            audio.pause();
            audio.removeAttribute("src");
            audio.load();
        }
    },

    setVolume: (volume) => {
        const audio = globalThis.RadioE45PodcastAudio._audio;
        if (audio)
            audio.volume = volume;
    },

    position: () => {
        const audio = globalThis.RadioE45PodcastAudio._audio;
        return audio && isFinite(audio.currentTime) ? audio.currentTime : 0;
    },

    duration: () => {
        const audio = globalThis.RadioE45PodcastAudio._audio;
        return audio && isFinite(audio.duration) ? audio.duration : 0;
    },

    // Resolves when the seek is done (or after 3 s, if the browser never reports "seeked").
    seek: (seconds) => new Promise(resolve => {
        const audio = globalThis.RadioE45PodcastAudio._audio;
        if (!audio) {
            resolve();
            return;
        }
        const done = () => {
            audio.removeEventListener("seeked", done);
            clearTimeout(timer);
            resolve();
        };
        const timer = setTimeout(done, 3000);
        audio.addEventListener("seeked", done);
        audio.currentTime = seconds;
    })
};
