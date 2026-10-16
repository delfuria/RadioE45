// Autoplay state for BrowserUserActivation (RadioE45.Uno/Services/Browser) via JSImport.
// Browsers allow play() without a gesture once the user has interacted with the page (sticky
// user activation); before that it depends on the browser: Chrome allows it on sites with the
// sound permission or a high media engagement, Safari per its Auto-Play setting.
globalThis.RadioE45Autoplay = {
    hasBeenActive: () => navigator.userActivation ? navigator.userActivation.hasBeenActive : false,

    // The Uno media player ignores the promise of play(): a refused autoplay (NotAllowedError)
    // would go unnoticed. The callback receives the URL of the refused source.
    onBlocked: (callback) => {
        const play = HTMLMediaElement.prototype.play;
        HTMLMediaElement.prototype.play = function () {
            const promise = play.call(this);
            const src = this.currentSrc || this.src || "";
            promise.catch(e => { if (e && e.name === "NotAllowedError") callback(src); });
            return promise;
        };
    },

    // One shot: the callback runs at the first click, tap or key press in the page, the moment
    // from which the browser lets the page start audio.
    onFirstGesture: (callback) => {
        const events = ["pointerdown", "keydown"];
        const handler = () => {
            events.forEach(e => document.removeEventListener(e, handler, true));
            callback();
        };
        events.forEach(e => document.addEventListener(e, handler, true));
    },

    // Plays 0.1 s of silence on an audible (not muted) element: resolves true when the browser
    // lets the page start audio on its own.
    probe: async () => {
        try {
            const samples = 800;
            const bytes = new Uint8Array(44 + samples);
            const view = new DataView(bytes.buffer);
            const text = (offset, s) => [...s].forEach((c, i) => bytes[offset + i] = c.charCodeAt(0));
            text(0, "RIFF"); view.setUint32(4, 36 + samples, true); text(8, "WAVEfmt ");
            view.setUint32(16, 16, true); view.setUint16(20, 1, true); view.setUint16(22, 1, true);
            view.setUint32(24, 8000, true); view.setUint32(28, 8000, true);
            view.setUint16(32, 1, true); view.setUint16(34, 8, true);
            text(36, "data"); view.setUint32(40, samples, true);
            bytes.fill(128, 44);
            const url = URL.createObjectURL(new Blob([bytes], { type: "audio/wav" }));
            const audio = new Audio(url);
            audio.volume = 0;
            try {
                await audio.play();
                audio.pause();
                return true;
            } finally {
                URL.revokeObjectURL(url);
            }
        } catch {
            return false;
        }
    }
};
