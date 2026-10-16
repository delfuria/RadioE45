// First fetch listener of radioe45-sw.js, registered before the Uno service worker's one.
// The Uno worker answers every request with fetch() from the worker. For the live streams this
// breaks Safari: it refuses a fetch() to the direct Icecast port (radioe45.ddns.net:8060, see
// S9a) while it plays the same URL in an audio element. Requests to other origins (streams, API,
// artwork) and media requests are left to the browser: no respondWith, the other listeners are
// skipped. Only the app's own files go through the Uno worker (network first, offline cache).
// A new worker takes over at once instead of waiting for every tab and app window to close:
// the Uno worker is network first, so an open page keeps working with the new one (which
// claims the clients on activate). Also replaces a Uno-only worker installed before this fix.
self.addEventListener('install', () => self.skipWaiting());

self.addEventListener('fetch', (event) => {
    const request = event.request;
    const crossOrigin = new URL(request.url).origin !== self.location.origin;
    const media = request.destination === 'audio' || request.destination === 'video'
        || request.headers.has('range');
    if (crossOrigin || media) {
        event.stopImmediatePropagation();
    }
});
